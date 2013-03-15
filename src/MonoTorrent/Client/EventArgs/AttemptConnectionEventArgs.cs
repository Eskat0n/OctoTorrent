namespace OctoTorrent.Client
{
    using System;

    public class AttemptConnectionEventArgs : EventArgs
    {
        private readonly Peer _peer;

        public AttemptConnectionEventArgs(Peer peer)
        {
            _peer = peer;
        }

        public bool BanPeer { get; set; }

        public Peer Peer
        {
            get { return _peer; }
        }
    }
}