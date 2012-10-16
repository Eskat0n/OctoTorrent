namespace OctoTorrent.Client.Tracker
{
    using System;

    public abstract class TrackerResponseEventArgs : EventArgs
    {
        private readonly TrackerConnectionID _id;

        protected TrackerResponseEventArgs(Tracker tracker, object state, bool successful)
        {
            if (tracker == null)
                throw new ArgumentNullException("tracker");
            var trackerConnectionID = state as TrackerConnectionID;
            if (trackerConnectionID == null)
                throw new ArgumentException("The state object must be the same object as in the call to Announce", "state");

            _id = trackerConnectionID;
            Successful = successful;
            Tracker = tracker;
        }

        internal TrackerConnectionID Id
        {
            get { return _id; }
        }

        public object State
        {
            get { return _id; }
        }

        /// <summary>
        ///   True if the request completed successfully
        /// </summary>
        public bool Successful { get; set; }

        /// <summary>
        ///   The tracker which the request was sent to
        /// </summary>
        public Tracker Tracker { get; protected set; }
    }
}