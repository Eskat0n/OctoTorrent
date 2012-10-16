namespace MonoTorrent.Client
{
    using System.Linq;
    using Common;

	class StoppingMode : Mode
	{
	    readonly WaitHandleGroup _handle = new WaitHandleGroup();

		public override TorrentState State
		{
			get { return TorrentState.Stopping; }
		}

		public StoppingMode(TorrentManager manager)
			: base(manager)
		{
			CanAcceptConnections = false;
			var engine = manager.Engine;

		    var hashingMode = manager.Mode as HashingMode;
		    if (hashingMode != null)
		        _handle.AddHandle(hashingMode.HashingWaitHandle, "Hashing");

			if (manager.TrackerManager.CurrentTracker != null)
				_handle.AddHandle(manager.TrackerManager.Announce(TorrentEvent.Stopped), "Announcing");

			foreach (var id in manager.Peers.ConnectedPeers.Where(id => id.Connection != null))
			    id.Connection.Dispose();

			manager.Peers.ClearAll();

			_handle.AddHandle(engine.DiskManager.CloseFileStreams(manager), "DiskManager");

			manager.Monitor.Reset();
			manager.PieceManager.Reset();
			engine.ConnectionManager.CancelPendingConnects(manager);
			engine.Stop();
		}

		public override void HandlePeerConnected(PeerId id, Direction direction)
		{
			id.CloseConnection();
		}

		public override void Tick(int counter)
		{
		    if (_handle.WaitOne(0, true) == false) 
                return;

		    _handle.Close();
		    Manager.Mode = new StoppedMode(Manager);
		}
	}
}
