namespace MonoTorrent.Client
{
    using System;
    using Common;

    public class PeerConnectionFailedEventArgs : TorrentEventArgs
    {
        private readonly Direction _connectionDirection;
        private readonly String _message;
        private readonly Peer _peer;

        /// <summary>
        ///   Create new instance of PeerConnectionFailedEventArgs for peer from given torrent.
        /// </summary>
        /// <param name="manager"> </param>
        /// <param name="peer"> </param>
        /// <param name="direction"> Which direction the connection attempt was </param>
        /// <param name="message"> Message associated with the failure </param>
        public PeerConnectionFailedEventArgs(TorrentManager manager, Peer peer, Direction direction, String message)
            : base(manager)
        {
            _peer = peer;
            _connectionDirection = direction;
            _message = message;
        }

        /// <summary>
        ///   Peer from which this event happened
        /// </summary>
        public Peer Peer
        {
            get { return _peer; }
        }

        /// <summary>
        ///   Direction of event (if our connection failed to them or their connection failed to us)
        /// </summary>
        public Direction ConnectionDirection
        {
            get { return _connectionDirection; }
        }

        /// <summary>
        ///   Any message that might be associated with this event
        /// </summary>
        public String Message
        {
            get { return _message; }
        }
    }
}