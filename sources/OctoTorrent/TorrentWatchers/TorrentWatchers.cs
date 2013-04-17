namespace OctoTorrent.TorrentWatchers
{
    using Common;
    using TorrentWatcher;

    /// <summary>
    ///     Main controller class for ITorrentWatcher
    /// </summary>
    public class TorrentWatchers : MonoTorrentCollection<ITorrentWatcher>
    {
        #region Constructors

        #endregion

        #region Methods

        /// <summary>
        /// </summary>
        public void StartAll()
        {
            for (int i = 0; i < Count; i++)
                this[i].Start();
        }


        /// <summary>
        /// </summary>
        public void StopAll()
        {
            for (int i = 0; i < Count; i++)
                this[i].Stop();
        }


        /// <summary>
        /// </summary>
        public void ForceScanAll()
        {
            for (int i = 0; i < Count; i++)
                this[i].ForceScan();
        }

        #endregion
    }
}