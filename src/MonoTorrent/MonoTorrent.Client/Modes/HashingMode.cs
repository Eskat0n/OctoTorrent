namespace MonoTorrent.Client
{
    using Common;
    using System.Threading;

	class HashingMode : Mode
	{
		internal readonly ManualResetEvent HashingWaitHandle;

        private readonly bool _autostart;
        private readonly bool _filesExist;
        private int _index = -1;
	    private readonly MainLoopResult _pieceCompleteCallback;

		public override TorrentState State
		{
			get { return TorrentState.Hashing; }
		}

		public HashingMode(TorrentManager manager, bool autostart)
			: base(manager)
		{
			CanAcceptConnections = false;
			HashingWaitHandle = new ManualResetEvent(false);
			_autostart = autostart;
			_filesExist = Manager.HasMetadata && manager.Engine.DiskManager.CheckAnyFilesExist(Manager);
            _pieceCompleteCallback = PieceComplete;
		}

		private void QueueNextHash()
		{
			if (Manager.Mode != this || _index == Manager.Torrent.Pieces.Count)
				HashingComplete();
			else
				Manager.Engine.DiskManager.BeginGetHash(Manager, _index, _pieceCompleteCallback);
		}

		private void PieceComplete(object hash)
		{
			if (Manager.Mode != this)
			{
				HashingComplete();
			}
			else
			{
			    Manager.Bitfield[_index] = hash != null && Manager.Torrent.Pieces.IsValid((byte[]) hash, _index);
				Manager.RaisePieceHashed(new PieceHashedEventArgs(Manager, _index, Manager.Bitfield[_index]));
				_index++;
				QueueNextHash();
			}
		}

		private void HashingComplete()
		{
			Manager.HashChecked = _index == Manager.Torrent.Pieces.Count;

			if (Manager.HasMetadata && !Manager.HashChecked)
			{
				Manager.Bitfield.SetAll(false);
				for (var i = 0; i < Manager.Torrent.Pieces.Count; i++)
					Manager.RaisePieceHashed(new PieceHashedEventArgs(Manager, i, false));
			}

			if (Manager.Engine != null && _filesExist)
				Manager.Engine.DiskManager.CloseFileStreams(Manager);

			HashingWaitHandle.Set();

			if (!Manager.HashChecked)
				return;

			if (_autostart)
			{
				Manager.Start();
			}
			else
			{
				Manager.Mode = new StoppedMode(Manager);
			}
		}

		public override void HandlePeerConnected(PeerId id, Direction direction)
		{
			id.CloseConnection();
		}

		public override void Tick(int counter)
		{
            if (!_filesExist)
            {
                Manager.Bitfield.SetAll(false);
                for (var i = 0; i < Manager.Torrent.Pieces.Count; i++)
                    Manager.RaisePieceHashed(new PieceHashedEventArgs(Manager, i, false));
                _index = Manager.Torrent.Pieces.Count;
                HashingComplete();
            }
            else if (_index == -1)
			{
				_index++;
				QueueNextHash();
			}
			// Do nothing in hashing mode
		}
	}
}
