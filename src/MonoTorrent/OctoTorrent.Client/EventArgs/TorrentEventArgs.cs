namespace MonoTorrent.Client
{
    using System;

    public class TorrentEventArgs : EventArgs
    {
        public TorrentEventArgs(TorrentManager manager)
        {
            TorrentManager = manager;
        }

        public TorrentManager TorrentManager { get; private set; }
    }
}