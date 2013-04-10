namespace OctoTorrent.Client.Tracker
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    public class TrackerTier : IEnumerable<Tracker>
    {
        private readonly List<Tracker> _trackers;

        internal bool SendingStartedEvent { get; set; }

        internal bool SentStartedEvent { get; set; }

        internal List<Tracker> Trackers
        {
            get { return _trackers; }
        }

        internal TrackerTier(IEnumerable<string> trackerUrls)
        {
            var trackerList = new List<Tracker>();

            foreach (var trackerUrl in trackerUrls)
            {
                // FIXME: Debug spew?
                Uri result;
                if (!Uri.TryCreate(trackerUrl, UriKind.Absolute, out result))
                {
                    Logger.Log(null, "TrackerTier - Invalid tracker Url specified: {0}", trackerUrl);
                    continue;
                }

                var tracker = TrackerFactory.Create(result);
                if (tracker != null)
                {
                    trackerList.Add(tracker);
                }
                else
                {
                    Console.Error.WriteLine("Unsupported protocol {0}", result); // FIXME: Debug spew?
                }
            }

            _trackers = trackerList;
        }

        public IEnumerator<Tracker> GetEnumerator()
        {
            return _trackers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal int IndexOf(Tracker tracker)
        {
            return _trackers.IndexOf(tracker);
        }

        public List<Tracker> GetTrackers()
        {
            return new List<Tracker>(_trackers);
        }
    }
}