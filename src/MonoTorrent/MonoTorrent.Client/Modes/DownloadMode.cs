namespace MonoTorrent.Client
{
    using System.Linq;
    using Common;

    internal class DownloadMode : Mode
    {
        private TorrentState _state;

        public DownloadMode(TorrentManager manager)
            : base(manager)
        {
            _state = manager.Complete ? TorrentState.Seeding : TorrentState.Downloading;
        }

        public override TorrentState State
        {
            get { return _state; }
        }

        public override void HandlePeerConnected(PeerId id, Direction direction)
        {
            if (!ShouldConnect(id))
                id.CloseConnection();
            base.HandlePeerConnected(id, direction);
        }

        public override bool ShouldConnect(Peer peer)
        {
            return !(peer.IsSeeder && Manager.HasMetadata && Manager.Complete);
        }

        public override void Tick(int counter)
        {
            //If download is complete, set state to 'Seeding'
            if (Manager.Complete && _state == TorrentState.Downloading)
            {
                _state = TorrentState.Seeding;
                Manager.RaiseTorrentStateChanged(new TorrentStateChangedEventArgs(Manager, TorrentState.Downloading,
                                                                                  TorrentState.Seeding));
                Manager.TrackerManager.Announce(TorrentEvent.Completed);
            }
            var peersToClose = Manager.Peers.ConnectedPeers
                .Where(peer => !ShouldConnect(peer));
            foreach (var peer in peersToClose)
                peer.CloseConnection();
            base.Tick(counter);
        }
    }
}