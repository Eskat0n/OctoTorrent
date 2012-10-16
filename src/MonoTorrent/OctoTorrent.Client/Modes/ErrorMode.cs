namespace OctoTorrent.Client
{
    using System;
    using Common;

    // In the error mode, we just disable all connections
    // Usually we enter this because the HD is full
    public enum Reason
    {
        ReadFailure,
        WriteFailure
    }

    public class Error
    {
        public Error(Reason reason, Exception exception)
        {
            Reason = reason;
            Exception = exception;
        }

        public Exception Exception { get; private set; }

        public Reason Reason { get; private set; }
    }

    internal class ErrorMode : Mode
    {
        public ErrorMode(TorrentManager manager)
            : base(manager)
        {
            CanAcceptConnections = false;
            CloseConnections();
        }

        public override TorrentState State
        {
            get { return TorrentState.Error; }
        }

        public override void Tick(int counter)
        {
            Manager.Monitor.Reset();
            CloseConnections();
        }

        private void CloseConnections()
        {
            foreach (var peer in Manager.Peers.ConnectedPeers)
                peer.CloseConnection();
        }
    }
}