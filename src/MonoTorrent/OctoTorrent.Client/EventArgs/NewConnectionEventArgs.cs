namespace MonoTorrent.Client
{
    using System;
    using Connections;

    public class NewConnectionEventArgs : TorrentEventArgs
    {
        private readonly IConnection _connection;
        private readonly Peer _peer;

        public NewConnectionEventArgs(Peer peer, IConnection connection, TorrentManager manager)
            : base(manager)
        {
            if (!connection.IsIncoming && manager == null)
                throw new InvalidOperationException(
                    "An outgoing connection must specify the torrent manager it belongs to");

            _connection = connection;
            _peer = peer;
        }

        public IConnection Connection
        {
            get { return _connection; }
        }

        public Peer Peer
        {
            get { return _peer; }
        }
    }
}