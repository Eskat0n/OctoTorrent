//
// TrackerManager.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

namespace OctoTorrent.Client.Tracker
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using Common;
    using Encryption;

    /// <summary>
    ///   Represents the connection to a tracker that an TorrentManager has
    /// </summary>
    public class TrackerManager : IEnumerable<TrackerTier>
    {
        #region Member Variables

        /// <summary>
        ///   The infohash for the torrent
        /// </summary>
        private readonly InfoHash _infoHash;

        private readonly TorrentManager _manager;
        private readonly IList<TrackerTier> _tierList;
        private readonly List<TrackerTier> _trackerTiers;
        private DateTime _lastUpdated;
        private bool _updateSucceeded;

        /// <summary>
        ///   Returns the tracker that is current in use by the engine
        /// </summary>
        public Tracker CurrentTracker
        {
            get
            {
                if (_trackerTiers.Count == 0 || _trackerTiers[0].Trackers.Count == 0)
                    return null;

                return _trackerTiers[0].Trackers[0];
            }
        }

        /// <summary>
        ///   True if the last update succeeded
        /// </summary>
        public bool UpdateSucceeded
        {
            get { return _updateSucceeded; }
        }

        /// <summary>
        ///   The time the last tracker update was sent to any tracker
        /// </summary>
        public DateTime LastUpdated
        {
            get { return _lastUpdated; }
        }

        /// <summary>
        ///   The trackers available
        /// </summary>
        public IList<TrackerTier> TrackerTiers
        {
            get { return _tierList; }
        }

        #endregion

        #region Constructors

        /// <summary>
        ///   Creates a new TrackerConnection for the supplied torrent file
        /// </summary>
        public TrackerManager(TorrentManager manager, InfoHash infoHash, IEnumerable<RawTrackerTier> announces)
        {
            _manager = manager;
            _infoHash = infoHash;

            // Check if this tracker supports scraping
            _trackerTiers = new List<TrackerTier>();
            foreach (var tier in announces)
                _trackerTiers.Add(new TrackerTier(tier));

            _trackerTiers.RemoveAll(t => t.Trackers.Count == 0);
            foreach (var tracker in _trackerTiers.SelectMany(x => x))
            {
                tracker.AnnounceComplete += (o, e) => ClientEngine.MainLoop.Queue(() => OnAnnounceComplete(o, e));
                tracker.ScrapeComplete += (o, e) => ClientEngine.MainLoop.Queue(() => OnScrapeComplete(o, e));
            }

            _tierList = new ReadOnlyCollection<TrackerTier>(_trackerTiers);
        }

        #endregion

        #region Methods

        public WaitHandle Announce()
        {
            return CurrentTracker == null
                       ? new ManualResetEvent(true)
                       : Announce(_trackerTiers[0].SentStartedEvent
                                      ? TorrentEvent.None
                                      : TorrentEvent.Started);
        }

        public WaitHandle Announce(Tracker tracker)
        {
            Check.Tracker(tracker);
            var tier = _trackerTiers.Find(t => t.Trackers.Contains(tracker));
            if (tier == null)
                throw new ArgumentException("Tracker has not been registered with the manager", "tracker");

            var tevent = tier.SentStartedEvent ? TorrentEvent.None : TorrentEvent.Started;
            return Announce(tracker, tevent, false, new ManualResetEvent(false));
        }

        internal WaitHandle Announce(TorrentEvent clientEvent)
        {
            return CurrentTracker == null
                       ? new ManualResetEvent(true)
                       : Announce(CurrentTracker, clientEvent, true, new ManualResetEvent(false));
        }

        private WaitHandle Announce(Tracker tracker, TorrentEvent clientEvent, bool trySubsequent,
                                    ManualResetEvent waitHandle)
        {
            var engine = _manager.Engine;

            // If the engine is null, we have been unregistered
            if (engine == null)
            {
                waitHandle.Set();
                return waitHandle;
            }

            _updateSucceeded = true;
            _lastUpdated = DateTime.Now;

            var e = engine.Settings.AllowedEncryption;
            var requireEncryption = !Toolbox.HasEncryption(e, EncryptionTypes.PlainText);
            var supportsEncryption = Toolbox.HasEncryption(e, EncryptionTypes.RC4Full) ||
                                     Toolbox.HasEncryption(e, EncryptionTypes.RC4Header);

            requireEncryption = requireEncryption && ClientEngine.SupportsEncryption;
            supportsEncryption = supportsEncryption && ClientEngine.SupportsEncryption;

            var reportedAddress = engine.Settings.ReportedAddress;
            var ip = reportedAddress == null ? null : reportedAddress.Address.ToString();
            var port = reportedAddress == null ? engine.Listener.Endpoint.Port : reportedAddress.Port;

            // FIXME: In metadata mode we need to pretend we need to download data otherwise
            // tracker optimisations might result in no peers being sent back.
            long bytesLeft = 1000;
            if (_manager.HasMetadata)
                bytesLeft = (long) ((1 - _manager.Bitfield.PercentComplete/100.0)*_manager.Torrent.Size);
            var parameters = new AnnounceParameters(_manager.Monitor.DataBytesDownloaded,
                                           _manager.Monitor.DataBytesUploaded,
                                           bytesLeft,
                                           clientEvent, _infoHash, requireEncryption, _manager.Engine.PeerId,
                                           ip, port) {SupportsEncryption = supportsEncryption};
            var id = new TrackerConnectionID(tracker, trySubsequent, clientEvent, waitHandle);
            tracker.Announce(parameters, id);
            return waitHandle;
        }

        private bool GetNextTracker(Tracker tracker, out TrackerTier trackerTier, out Tracker trackerReturn)
        {
            for (var i = 0; i < _trackerTiers.Count; i++)
            {
                for (var j = 0; j < _trackerTiers[i].Trackers.Count; j++)
                {
                    if (_trackerTiers[i].Trackers[j] != tracker)
                        continue;

                    // If we are on the last tracker of this tier, check to see if there are more tiers
                    if (j == (_trackerTiers[i].Trackers.Count - 1))
                    {
                        if (i == (_trackerTiers.Count - 1))
                        {
                            trackerTier = null;
                            trackerReturn = null;
                            return false;
                        }

                        trackerTier = _trackerTiers[i + 1];
                        trackerReturn = trackerTier.Trackers[0];
                        return true;
                    }

                    trackerTier = _trackerTiers[i];
                    trackerReturn = trackerTier.Trackers[j + 1];
                    return true;
                }
            }

            trackerTier = null;
            trackerReturn = null;
            return false;
        }

        private static void OnScrapeComplete(object sender, TrackerResponseEventArgs e)
        {
            e.Id.WaitHandle.Set();
        }

        private void OnAnnounceComplete(object sender, AnnounceResponseEventArgs e)
        {
            _updateSucceeded = e.Successful;
            if (_manager.Engine == null)
            {
                e.Id.WaitHandle.Set();
                return;
            }

            if (e.Successful)
            {
                _manager.Peers.BusyPeers.Clear();
                var count = _manager.AddPeersCore(e.Peers);
                _manager.RaisePeersFound(new TrackerPeersAdded(_manager, count, e.Peers.Count, e.Tracker));

                var tier = _trackerTiers.Find(t => t.Trackers.Contains(e.Tracker));
                if (tier != null)
                {
                    Toolbox.Switch(tier.Trackers, 0, tier.IndexOf(e.Tracker));
                    Toolbox.Switch(_trackerTiers, 0, _trackerTiers.IndexOf(tier));
                }
                e.Id.WaitHandle.Set();
            }
            else
            {
                TrackerTier tier;
                Tracker tracker;

                if (!e.Id.TrySubsequent || !GetNextTracker(e.Tracker, out tier, out tracker))
                    e.Id.WaitHandle.Set();
                else
                    Announce(tracker, e.Id.TorrentEvent, true, e.Id.WaitHandle);
            }
        }

        public WaitHandle Scrape()
        {
            return CurrentTracker == null 
                ? new ManualResetEvent(true) 
                : Scrape(CurrentTracker, false);
        }

        public WaitHandle Scrape(Tracker tracker)
        {
            var tier = _trackerTiers.Find(t => t.Trackers.Contains(tracker));
            return tier == null
                       ? new ManualResetEvent(true)
                       : Scrape(tracker, false);
        }

        private WaitHandle Scrape(Tracker tracker, bool trySubsequent)
        {
            if (tracker == null)
                throw new ArgumentNullException("tracker");

            if (!tracker.CanScrape)
                throw new TorrentException("This tracker does not support scraping");

            var id = new TrackerConnectionID(tracker, trySubsequent, TorrentEvent.None, new ManualResetEvent(false));
            tracker.Scrape(new ScrapeParameters(_infoHash), id);
            return id.WaitHandle;
        }

        #endregion

        #region IEnumerable<TrackerTier> Members

        public IEnumerator<TrackerTier> GetEnumerator()
        {
            return _trackerTiers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}