namespace OctoTorrent.Client.Tracker
{
    using System.Collections.Generic;

    public class AnnounceResponseEventArgs : TrackerResponseEventArgs
    {
        private readonly List<Peer> _peers;

        public AnnounceResponseEventArgs(Tracker tracker, object state, bool successful)
            : this(tracker, state, successful, new List<Peer>())
        {
        }

        public AnnounceResponseEventArgs(Tracker tracker, object state, bool successful, List<Peer> peers)
            : base(tracker, state, successful)
        {
            _peers = peers;
        }

        public List<Peer> Peers
        {
            get { return _peers; }
        }
    }
}