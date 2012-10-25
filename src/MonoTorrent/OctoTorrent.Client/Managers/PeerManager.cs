namespace OctoTorrent.Client
{
    using System.Collections.Generic;
    using System.Linq;

    public class PeerManager
    {
        #region Member Variables

        private readonly List<Peer> _bannedPeers;

        internal readonly List<Peer> ActivePeers;
        internal readonly List<Peer> AvailablePeers;
        internal readonly List<Peer> BusyPeers;
        internal readonly List<Peer> ConnectingToPeers = new List<Peer>();
        internal readonly List<PeerId> ConnectedPeers = new List<PeerId>();

        #endregion Member Variables

        #region Properties

        public IEnumerable<PeerId> Connected
        {
            get { return ConnectedPeers.AsReadOnly(); }
        }

        /// <summary>
        ///   Returns the iterator for all peers (available, active, banned and busy)
        /// </summary>
        public IEnumerable<Peer> All
        {
            get
            {
                foreach (var peer in AvailablePeers)
                    yield return peer;

                foreach (var peer in ActivePeers)
                    yield return peer;

                foreach (var peer in _bannedPeers)
                    yield return peer;

                foreach (var peer in BusyPeers)
                    yield return peer;
            }
        }

        /// <summary>
        ///   Returns the number of peers available to connect
        /// </summary>
        public int AvailableCount
        {
            get { return AvailablePeers.Count; }
        }

        /// <summary>
        ///   Returns the number of Leechs we are currently connected to
        /// </summary>
        public int LeechsCount
        {
            get { return (int) ClientEngine.MainLoop.QueueWait(() => ActivePeers.Count(p => !p.IsSeeder)); }
        }

        /// <summary>
        ///   Returns the number of Seeds we are currently connected to
        /// </summary>
        public int SeedsCount
        {
            get { return (int) ClientEngine.MainLoop.QueueWait(() => ActivePeers.Count(p => p.IsSeeder)); }
        }

        #endregion

        #region Constructors

        public PeerManager()
        {
            _bannedPeers = new List<Peer>();
            ActivePeers = new List<Peer>();
            AvailablePeers = new List<Peer>();
            BusyPeers = new List<Peer>();
        }

        #endregion Constructors

        #region Methods

        internal void ClearAll()
        {
            ActivePeers.Clear();
            AvailablePeers.Clear();
            _bannedPeers.Clear();
            BusyPeers.Clear();
        }

        internal bool Contains(Peer peer)
        {
            return All.Any(peer.Equals);
        }

        #endregion Methods
    }
}