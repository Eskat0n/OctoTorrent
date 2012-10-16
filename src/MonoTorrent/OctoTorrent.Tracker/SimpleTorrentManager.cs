//
// SimpleTorrentManager.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
//
// Copyright (C) 2006 Gregor Burger
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

namespace OctoTorrent.Tracker
{
    using System;
    using System.Diagnostics;
    using System.Collections.Generic;
    using System.Linq;
    using Common;
    using BEncoding;

    ///<summary>
    ///This class is a TorrentManager which uses .Net Generics datastructures, such 
    ///as Dictionary and List to manage Peers from a Torrent.
    ///</summary>
    public class SimpleTorrentManager
    {
        #region Member Variables

        private readonly IPeerComparer _comparer;
        private List<Peer> _buffer = new List<Peer>();
        private readonly BEncodedNumber _complete;
        private readonly BEncodedNumber _incomplete;
        private readonly BEncodedNumber _downloaded;
        private readonly Dictionary<object, Peer> _peers;
        private readonly Random _random;
        private readonly ITrackable _trackable;
        private readonly Tracker _tracker;

        #endregion Member Variables

        #region Properties

        /// <summary>
        /// The number of active seeds
        /// </summary>
        public long Complete
        {
            get { return _complete.Number; }
        }

        public long Incomplete
        {
            get
            {
                return _incomplete.Number;
            }
        }

        /// <summary>
        /// The total number of peers being tracked
        /// </summary>
        public int Count
        {
            get { return _peers.Count; }
        }

        /// <summary>
        /// The total number of times the torrent has been fully downloaded
        /// </summary>
        public long Downloaded
        {
            get { return _downloaded.Number; }
        }

        /// <summary>
        /// The torrent being tracked
        /// </summary>
        public ITrackable Trackable
        {
            get { return _trackable; }
        }

        #endregion Properties

        #region Constructors

        public SimpleTorrentManager(ITrackable trackable, IPeerComparer comparer, Tracker tracker)
        {
            _comparer = comparer;
            _trackable = trackable;
            _tracker = tracker;
            _complete = new BEncodedNumber(0);
            _downloaded = new BEncodedNumber(0);
            _incomplete = new BEncodedNumber(0);
            _peers = new Dictionary<object, Peer>();
            _random = new Random();
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Adds the peer to the tracker
        /// </summary>
        /// <param name="peer"></param>
        internal void Add(Peer peer)
        {
            if (peer == null)
                throw new ArgumentNullException("peer");

            Debug.WriteLine("Adding: {0}", peer.ClientAddress);
            _peers.Add(peer.DictionaryKey, peer);
            lock (_buffer)
                _buffer.Clear();
            UpdateCounts();
        }

        public List<Peer> GetPeers()
        {
            lock (_buffer)
                return new List<Peer>(_buffer);
        }

        /// <summary>
        /// Retrieves a semi-random list of peers which can be used to fulfill an Announce request
        /// </summary>
        /// <param name="response">The bencoded dictionary to add the peers to</param>
        /// <param name="count">The number of peers to add</param>
        /// <param name="compact">True if the peers should be in compact form</param>
        internal void GetPeers(BEncodedDictionary response, int count, bool compact)
        {
            byte[] compactResponse = null;
            BEncodedList nonCompactResponse = null;

            var total = Math.Min(_peers.Count, count);
            // If we have a compact response, we need to create a single BencodedString
            // Otherwise we need to create a bencoded list of dictionaries
            if (compact)
                compactResponse = new byte[total * 6];
            else
                nonCompactResponse = new BEncodedList(total);

            var start = _random.Next(0, _peers.Count);

            lock (_buffer)
            {
                if (_buffer.Count != _peers.Values.Count)
                    _buffer = new List<Peer>(_peers.Values);
            }
            var p = _buffer;

            while (total > 0)
            {
                var current = p[(start++) % p.Count];
                if (compact)
                {
                    Buffer.BlockCopy(current.CompactEntry, 0, compactResponse, (total - 1) * 6, 6);
                }
                else
                {
                    nonCompactResponse.Add(current.NonCompactEntry);
                }
                total--;
            }

            if (compact)
                response.Add(Tracker.PeersKey, (BEncodedString)compactResponse);
            else
                response.Add(Tracker.PeersKey, nonCompactResponse);
        }

        internal void ClearZombiePeers(DateTime cutoff)
        {
            var removed = false;
            lock (_buffer)
            {
                foreach (var p in _buffer.Where(p => p.LastAnnounceTime <= cutoff))
                {
                    _tracker.RaisePeerTimedOut(new TimedOutEventArgs(p, this));
                    _peers.Remove(p.DictionaryKey);
                    removed = true;
                }

                if (removed)
                    _buffer.Clear();
            }
        }


        /// <summary>
        /// Removes the peer from the tracker
        /// </summary>
        /// <param name="peer">The peer to remove</param>
        internal void Remove(Peer peer)
        {
            if (peer == null)
                throw new ArgumentNullException("peer");

            Debug.WriteLine("Removing: {0}", peer.ClientAddress);
            _peers.Remove(peer.DictionaryKey);
            lock (_buffer)
                _buffer.Clear();
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            var complete = 0;
            var incomplete = 0;

            foreach (var p in _peers.Values)
            {
                if (p.HasCompleted)
                    complete++;
                else
                    incomplete++;
            }

            _complete.InternalNumber = complete;
            _incomplete.InternalNumber = incomplete;
        }

        /// <summary>
        /// Updates the peer in the tracker database based on the announce parameters
        /// </summary>
        /// <param name="par"></param>
        internal void Update(AnnounceParameters par)
        {
            Peer peer;
            var peerKey = _comparer.GetKey(par);
            if (!_peers.TryGetValue(peerKey, out peer))
            {
                peer = new Peer(par, peerKey);
                Add(peer);
            }
            else
            {
                Debug.WriteLine("Updating: {0} with key {1}", peer.ClientAddress, peerKey);
                peer.Update(par);
            }
            switch (par.Event)
            {
                case TorrentEvent.Completed:
                    System.Threading.Interlocked.Increment(ref _downloaded.InternalNumber);
                    break;
                case TorrentEvent.Stopped:
                    Remove(peer);
                    break;
            }

            _tracker.RaisePeerAnnounced(new AnnounceEventArgs(peer, this));
            UpdateCounts();
        }

        #endregion Methods
    }
}
