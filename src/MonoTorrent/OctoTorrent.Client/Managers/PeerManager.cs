namespace MonoTorrent.Client
{
    using System.Collections.Generic;
    using System.Linq;

    public class PeerManager
    {
        #region Member Variables

        internal readonly List<Peer> ActivePeers;
        internal readonly List<Peer> AvailablePeers;
        internal readonly List<Peer> BannedPeers;
        internal readonly List<Peer> BusyPeers;
        internal readonly List<PeerId> ConnectedPeers = new List<PeerId>();
        internal readonly List<Peer> ConnectingToPeers = new List<Peer>();

        #endregion Member Variables

        #region Properties

        public int Available
        {
            get { return AvailablePeers.Count; }
        }

        /// <summary>
        ///   Returns the number of Leechs we are currently connected to
        /// </summary>
        /// <returns> </returns>
        public int Leechs
        {
            get { return (int) ClientEngine.MainLoop.QueueWait(() => ActivePeers.Count(p => !p.IsSeeder)); }
        }

        /// <summary>
        ///   Returns the number of Seeds we are currently connected to
        /// </summary>
        /// <returns> </returns>
        public int Seeds
        {
            get { return (int) ClientEngine.MainLoop.QueueWait(() => ActivePeers.Count(p => p.IsSeeder)); }
        }

        #endregion

        #region Constructors

        public PeerManager()
        {
            ActivePeers = new List<Peer>();
            AvailablePeers = new List<Peer>();
            BannedPeers = new List<Peer>();
            BusyPeers = new List<Peer>();
        }

        #endregion Constructors

        #region Methods

        internal IEnumerable<Peer> AllPeers()
        {
            foreach (var peer in AvailablePeers)
                yield return peer;

            foreach (var peer in ActivePeers)
                yield return peer;

            foreach (var peer in BannedPeers)
                yield return peer;

            foreach (var peer in BusyPeers)
                yield return peer;
        }

        internal void ClearAll()
        {
            ActivePeers.Clear();
            AvailablePeers.Clear();
            BannedPeers.Clear();
            BusyPeers.Clear();
        }

        internal bool Contains(Peer peer)
        {
            return AllPeers().Any(peer.Equals);
        }

        #endregion Methods
    }
}