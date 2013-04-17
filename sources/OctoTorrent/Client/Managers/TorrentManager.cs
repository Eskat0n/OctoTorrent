//
// TorrentManager.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

namespace OctoTorrent.Client
{
    using System;
    using System.Collections.Generic;    
    using System.Threading;
    using System.IO;
    using Common;
    using Tracker;

    public class TorrentManager : IDisposable, IEquatable<TorrentManager>
    {
        #region Events

        public event EventHandler<PeerConnectionEventArgs> PeerConnected;

        public event EventHandler<PeerConnectionEventArgs> PeerDisconnected;

        internal event EventHandler<PeerConnectionFailedEventArgs> ConnectionAttemptFailed;

        public event EventHandler<PeersAddedEventArgs> PeersFound;

        public event EventHandler<PieceHashedEventArgs> PieceHashed;

        public event EventHandler<TorrentStateChangedEventArgs> TorrentStateChanged;

        internal event EventHandler<PeerAddedEventArgs> OnPeerFound;

        #endregion

        #region Member Variables

        internal Queue<int> FinishedPieces;         // The list of pieces which we should send "have" messages for
        internal IUnchoker ChokeUnchoker;           // Used to choke and unchoke peers
        internal DateTime LastCalledInactivePeerManager = DateTime.Now;

        private BitField _bitfield;              // The bitfield representing the pieces we've downloaded and have to download
        private bool _disposed;
        private ClientEngine _engine;            // The engine that this torrent is registered with
        private Error _error;
        private bool _hashChecked;               // True if the manager has been hash checked
        private int _hashFails;                  // The total number of pieces receieved which failed the hashcheck
        private readonly InfoHash _infohash;
        internal bool isInEndGame;               // Set true when the torrent enters end game processing
        private Mode _mode;
        private ConnectionMonitor _monitor;      // Calculates download/upload speed
        private PeerManager _peers;               // Stores all the peers we know of in a list
        private PieceManager _pieceManager;       // Tracks all the piece requests we've made and decides what pieces we can request off each peer
        private string _savePath;
        private RateLimiterGroup _uploadLimiter;     // Contains the logic to decide how many chunks we can download
        private RateLimiterGroup _downloadLimiter;   // Contains the logic to decide how many chunks we can download
        private readonly TorrentSettings _settings;       // The settings for this torrent
        private DateTime _startTime;             // The time at which the torrent was started at.
        private Torrent _torrent;                // All the information from the physical torrent that was loaded
        private readonly string _torrentSave;             // The path where the .torrent data will be saved when in metadata mode
        private TrackerManager _trackerManager;  // The class used to control all access to the tracker
        private int _uploadingTo;                // The number of peers which we're currently uploading to
        private InactivePeerManager _inactivePeerManager; // Used to identify inactive peers we don't want to connect to
#if !DISABLE_DHT	
		private bool _dhtInitialised;
#endif		
        #endregion Member Variables

        #region Properties

        public BitField Bitfield
        {
            get { return _bitfield; }
            internal set { _bitfield = value; }
        }

        public bool CanUseDht
        {
            get { return _settings.UseDht && (_torrent == null || !_torrent.IsPrivate); }
        }

        public bool Complete
        {
            get { return _bitfield.AllTrue; }
        }

        internal RateLimiterGroup DownloadLimiter
        {
            get { return _downloadLimiter; }
        }

        public ClientEngine Engine
        {
            get { return _engine; }
            internal set { _engine = value; }
        }

        public Error Error
        {
            get { return _error; }
            internal set { _error = value; }
        }

        internal Mode Mode
        {
            get { return _mode; }
            set {
                var oldMode = _mode;
                _mode = value;
                if (oldMode != null)
                    RaiseTorrentStateChanged(new TorrentStateChangedEventArgs(this, oldMode.State, _mode.State));
                _mode.Tick(0);
			}
        }

        public int PeerReviewRoundsComplete
        {
            get
            {
                if (this.ChokeUnchoker is ChokeUnchokeManager)
                    return ((ChokeUnchokeManager)this.ChokeUnchoker).ReviewsExecuted;
                else
                    return 0;
            }
        }


        public bool HashChecked
        {
            get { return _hashChecked; }
            internal set { _hashChecked = value; }
        }

        public int HashFails
        {
            get { return this._hashFails; }
        }

        public bool HasMetadata
        {
            get { return _torrent != null; }
        }

		/// <summary>
		/// True if this torrent has activated special processing for the final few pieces
		/// </summary>
		public bool IsInEndGame
		{
			get { return State == TorrentState.Downloading && isInEndGame; }
		}

        public ConnectionMonitor Monitor
        {
            get { return _monitor; }
        }

        /// <summary>
        /// The number of peers that this torrent instance is connected to
        /// </summary>
        public int OpenConnections
        {
            get { return Peers.ConnectedPeers.Count; }
        }

        public PeerManager Peers
        {
            get { return _peers; }
        }


		/// <summary>
		/// The piecemanager for this TorrentManager
		/// </summary>
		public PieceManager PieceManager
		{
			get { return _pieceManager; }
            internal set { _pieceManager = value; }
		}

		/// <summary>
		/// The inactive peer manager for this TorrentManager
		/// </summary>
		internal InactivePeerManager InactivePeerManager
		{
			get { return _inactivePeerManager; }
		}

        /// <summary>
        /// The current progress of the torrent in percent
        /// </summary>
        public double Progress
        {
            get { return _bitfield.PercentComplete; }
        }

        /// <summary>
        /// The directory to download the files to
        /// </summary>
        public string SavePath
        {
            get { return _savePath; }
        }

        /// <summary>
        /// The settings for with this TorrentManager
        /// </summary>
        public TorrentSettings Settings
        {
            get { return _settings; }
        }

        /// <summary>
        /// The current state of the TorrentManager
        /// </summary>
        public TorrentState State
        {
            get { return _mode.State; }
        }

        /// <summary>
        /// The time the torrent manager was started at
        /// </summary>
        public DateTime StartTime
        {
            get { return _startTime; }
        }

        /// <summary>
        /// The tracker connection associated with this TorrentManager
        /// </summary>
        public TrackerManager TrackerManager
        {
            get { return _trackerManager; }
        }

        /// <summary>
        /// The Torrent contained within this TorrentManager
        /// </summary>
        public Torrent Torrent
        {
            get { return _torrent; }
            internal set { _torrent = value; }
        }

        /// <summary>
        /// The number of peers that we are currently uploading to
        /// </summary>
        public int UploadingTo
        {
            get { return this._uploadingTo; }
            internal set { this._uploadingTo = value; }
        }

        internal RateLimiterGroup UploadLimiter
        {
            get { return _uploadLimiter; }
        }

        public bool IsInitialSeeding
        {
            get { return Mode is InitialSeedingMode; }
        }

		/// <summary>
		/// Number of peers we have inactivated for this torrent
		/// </summary>
		public int InactivePeers
		{
			get { return _inactivePeerManager.InactivePeers; }
		}

        public InfoHash InfoHash
        {
            get { return _infohash; }
        }

		/// <summary>
		/// List of peers we have inactivated for this torrent
		/// </summary>
		public List<Uri> InactivePeerList
		{
			get { return _inactivePeerManager.InactivePeerList; }
		}

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new TorrentManager instance.
        /// </summary>
        /// <param name="torrent">The torrent to load in</param>
        /// <param name="savePath">The directory to save downloaded files to</param>
        /// <param name="settings">The settings to use for controlling connections</param>
        public TorrentManager(Torrent torrent, string savePath, TorrentSettings settings)
            : this(torrent, savePath, settings, torrent.Files.Length == 1 ? "" : torrent.Name)
        {

        }

        /// <summary>
        /// Creates a new TorrentManager instance.
        /// </summary>
        /// <param name="torrent">The torrent to load in</param>
        /// <param name="savePath">The directory to save downloaded files to</param>
        /// <param name="settings">The settings to use for controlling connections</param>
        /// <param name="baseDirectory">In the case of a multi-file torrent, the name of the base directory containing the files. Defaults to Torrent.Name</param>
        public TorrentManager(Torrent torrent, string savePath, TorrentSettings settings, string baseDirectory)
        {
            Check.Torrent(torrent);
            Check.SavePath(savePath);
            Check.Settings(settings);
            Check.BaseDirectory(baseDirectory);

            this._torrent = torrent;
            this._infohash = torrent.infoHash;
            this._settings = settings;

            Initialise(savePath, baseDirectory, torrent.AnnounceUrls);
            ChangePicker(CreateStandardPicker());
        }


        public TorrentManager(InfoHash infoHash, string savePath, TorrentSettings settings, string torrentSave, IList<RawTrackerTier> announces)
        {
            Check.InfoHash(infoHash);
            Check.SavePath(savePath);
            Check.Settings(settings);
            Check.TorrentSave(torrentSave);
            Check.Announces(announces);

            this._infohash = infoHash;
            this._settings = settings;
            this._torrentSave = torrentSave;

            Initialise(savePath, "", announces);
        }

        public TorrentManager(MagnetLink magnetLink, string savePath, TorrentSettings settings, string torrentSave)
        {
            Check.MagnetLink(magnetLink);
            Check.InfoHash(magnetLink.InfoHash);
            Check.SavePath(savePath);
            Check.Settings(settings);
            Check.TorrentSave(torrentSave);

            this._infohash = magnetLink.InfoHash;
            this._settings = settings;
            this._torrentSave = torrentSave;
            IList<RawTrackerTier> announces = new RawTrackerTiers ();
            if (magnetLink.AnnounceUrls != null)
                announces.Add (magnetLink.AnnounceUrls);
            Initialise(savePath, "", announces);
        }

        void Initialise(string savePath, string baseDirectory, IList<RawTrackerTier> announces)
        {
            _bitfield = new BitField(HasMetadata ? _torrent.Pieces.Count : 1);
            this._savePath = Path.Combine(savePath, baseDirectory);
            FinishedPieces = new Queue<int>();
            _monitor = new ConnectionMonitor();
            _inactivePeerManager = new InactivePeerManager(this);
            _peers = new PeerManager();
            _pieceManager = new PieceManager();
            _trackerManager = new TrackerManager(this, InfoHash, announces);

            Mode = new StoppedMode(this);            
            CreateRateLimiters();

            PieceHashed += (o, e) => PieceManager.UnhashedPieces[e.PieceIndex] = false;

            if (HasMetadata) {
                foreach (var file in _torrent.Files)
                    file.FullPath = Path.Combine(SavePath, file.Path);
            }
        }

        void CreateRateLimiters()
        {
            RateLimiter downloader = new RateLimiter();
            _downloadLimiter = new RateLimiterGroup();
            _downloadLimiter.Add(new PauseLimiter(this));
            _downloadLimiter.Add(downloader);

            RateLimiter uploader = new RateLimiter();
            _uploadLimiter = new RateLimiterGroup();
            _uploadLimiter.Add(new PauseLimiter(this));
            _uploadLimiter.Add(uploader);
        }

        #endregion

        #region Public Methods

        public void ChangePicker(PiecePicker picker)
        {
            Check.Picker(picker);

            ClientEngine.MainLoop.QueueWait((MainLoopTask)delegate {
                this._pieceManager.ChangePicker(picker, _bitfield, _torrent.Files);
            });
        }

        public void Dispose()
        {
            _disposed = true;
        }


        /// <summary>
        /// Overrridden. Returns the name of the torrent.
        /// </summary>
        /// <returns></returns>
        public override string ToString( )
        {
            return Torrent == null ? "<Metadata Mode>" : this.Torrent.Name;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            TorrentManager m = obj as TorrentManager;
            return (m == null) ? false : this.Equals(m);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(TorrentManager other)
        {
            return (other == null) ? false : _infohash == other._infohash;
        }

        public List<Piece> GetActiveRequests()
        {
            return (List<Piece>)ClientEngine.MainLoop.QueueWait((MainLoopJob)delegate {
                return PieceManager.Picker.ExportActiveRequests();
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return _infohash.GetHashCode();
        }

        public List<PeerId> GetPeers()
        {
            return (List<PeerId>)ClientEngine.MainLoop.QueueWait((MainLoopJob)delegate {
                return new List<PeerId>(_peers.ConnectedPeers);
            });
        }

        /// <summary>
        /// Starts a hashcheck. If forceFullScan is false, the library will attempt to load fastresume data
        /// before performing a full scan, otherwise fast resume data will be ignored and a full scan will be started
        /// </summary>
        /// <param name="forceFullScan">True if a full hash check should be performed ignoring fast resume data</param>
        public void HashCheck(bool autoStart)
        {
            ClientEngine.MainLoop.QueueWait((MainLoopTask)delegate {
                if (!Mode.CanHashCheck)
                    throw new TorrentException(string.Format("A hashcheck can only be performed when the manager is stopped. State is: {0}", State));

                CheckRegisteredAndDisposed();
                this._startTime = DateTime.Now;
                Mode = new HashingMode(this, autoStart);
                Engine.Start();
            });
        }

        public void MoveFile (TorrentFile file, string path)
        {
            Check.File (file);
            Check.PathNotEmpty (path);
            CheckRegisteredAndDisposed();
            CheckMetadata();

            if (State != TorrentState.Stopped)
                throw new TorrentException("Cannot move files when the torrent is active");

            Engine.DiskManager.MoveFile (this, file, path);
        }

        public void MoveFiles(string newRoot, bool overWriteExisting)
        {
            CheckRegisteredAndDisposed();
            CheckMetadata();

            if (State != TorrentState.Stopped)
                throw new TorrentException("Cannot move files when the torrent is active");

            Engine.DiskManager.MoveFiles(this, newRoot, overWriteExisting);
            _savePath = newRoot;
        }

        /// <summary>
        /// Pauses the TorrentManager
        /// </summary>
        public void Pause()
        {
            ClientEngine.MainLoop.QueueWait((MainLoopTask)delegate {
                CheckRegisteredAndDisposed();
                if (State != TorrentState.Downloading && State != TorrentState.Seeding)
                    return;

                // By setting the state to "paused", peers will not be dequeued from the either the
                // sending or receiving queues, so no traffic will be allowed.
                Mode = new PausedMode(this);
                this.SaveFastResume();
            });
        }


        /// <summary>
        /// Starts the TorrentManager
        /// </summary>
        public void Start()
        {
            ClientEngine.MainLoop.QueueWait((MainLoopTask)delegate {
                CheckRegisteredAndDisposed();

                this._engine.Start();
                // If the torrent was "paused", then just update the state to Downloading and forcefully
                // make sure the peers begin sending/receiving again
                if (this.State == TorrentState.Paused)
                {
                    Mode = new DownloadMode(this);
                    return;
                }

                if (!HasMetadata)
                {
                    Mode = new MetadataMode(this, _torrentSave);
#if !DISABLE_DHT
                    StartDHT();
#endif                    
                    return;
                }

                VerifyHashState ();
                // If the torrent has not been hashed, we start the hashing process then we wait for it to finish
                // before attempting to start again
                if (!_hashChecked)
                {
                    if (State != TorrentState.Hashing)
                        HashCheck(true);
                    return;
                }

                if (State == TorrentState.Seeding || State == TorrentState.Downloading)
                    return;

                if (TrackerManager.CurrentTracker != null)
                {
                    if (this._trackerManager.CurrentTracker.CanScrape)
                        this.TrackerManager.Scrape();
                    this._trackerManager.Announce(TorrentEvent.Started); // Tell server we're starting
                }

                if (this.Complete && this._settings.InitialSeedingEnabled && ClientEngine.SupportsInitialSeed) {
					Mode = new InitialSeedingMode(this);
                }
                else {
                    Mode = new DownloadMode(this);
                }
                _engine.Broadcast(this);

#if !DISABLE_DHT
                StartDHT();
#endif
                this._startTime = DateTime.Now;
                this._pieceManager.Reset();

                ClientEngine.MainLoop.QueueTimeout(TimeSpan.FromSeconds(2), delegate {
                    if (State != TorrentState.Downloading && State != TorrentState.Seeding)
                        return false;
                    _pieceManager.Picker.CancelTimedOutRequests();
                    return true;
                });
            });
        }

#if !DISABLE_DHT
        private void StartDHT()
        {
			if (_dhtInitialised)
				return;
			_dhtInitialised = true;
            _engine.DhtEngine.PeersFound += delegate (object o, PeersFoundEventArgs e) { DhtPeersFound(o, e);};
 
            // First get some peers
            _engine.DhtEngine.GetPeers(InfoHash);

            // Second, get peers every 10 minutes (if we need them)
            ClientEngine.MainLoop.QueueTimeout(TimeSpan.FromMinutes(10), delegate {
                // Torrent is no longer active
                if (!Mode.CanAcceptConnections)
                    return false;

                // Only use DHT if it hasn't been (temporarily?) disabled in settings
                if (CanUseDht && Peers.AvailablePeers.Count < Settings.MaxConnections)
                {
                    _engine.DhtEngine.Announce(InfoHash, _engine.Settings.ListenPort);
                    //announce ever done a get peers task
                    //engine.DhtEngine.GetPeers(InfoHash);
                }
                return true;
            });
        }
#endif

        /// <summary>
        /// Stops the TorrentManager
        /// </summary>
        public void Stop()
        {
            if (State == TorrentState.Error)
            {
                _error = null;
				Mode = new StoppedMode(this);
                return;
            }

			if (Mode is StoppingMode)
                return;

            ClientEngine.MainLoop.QueueWait(delegate {
                if (State != TorrentState.Stopped) {
#if !DISABLE_DHT
                    _engine.DhtEngine.PeersFound -= DhtPeersFound;
#endif
					Mode = new StoppingMode(this);
                }
            });
        }

        #endregion

        #region Internal Methods

        public void AddPeers (Peer peer)
        {
            Check.Peer (peer);
            if (HasMetadata && Torrent.IsPrivate)
                throw new InvalidOperationException ("You cannot add external peers to a private torrent");

            ClientEngine.MainLoop.QueueWait (() => {
                AddPeersCore (peer);
            });
        }

        public void AddPeers (IEnumerable <Peer> peers)
        {
            Check.Peers (peers);
            if (HasMetadata && Torrent.IsPrivate)
                throw new InvalidOperationException ("You cannot add external peers to a private torrent");

            ClientEngine.MainLoop.QueueWait (() => {
                AddPeersCore (peers);
            });
        }

        internal int AddPeersCore(Peer peer)
        {
            if (this._peers.Contains(peer))
                return 0;

            // Ignore peers in the inactive list
            if (this._inactivePeerManager.InactivePeerList.Contains(peer.ConnectionUri))
                return 0;

            this._peers.AvailablePeers.Add(peer);
            if (OnPeerFound != null)
                OnPeerFound(this, new PeerAddedEventArgs(this, peer));
            // When we successfully add a peer we try to connect to the next available peer
            return 1;
        }

        internal int AddPeersCore(IEnumerable<Peer> peers)
        {
            int count = 0;
            foreach (Peer p in peers)
                count += AddPeersCore(p);
            return count;
        }

        internal void HashedPiece(PieceHashedEventArgs pieceHashedEventArgs)
        {
            if (!pieceHashedEventArgs.HashPassed)
                Interlocked.Increment(ref this._hashFails);

            RaisePieceHashed(pieceHashedEventArgs);
        }
        
        internal void RaisePeerConnected(PeerConnectionEventArgs args)
        {
            Toolbox.RaiseAsyncEvent<PeerConnectionEventArgs>(PeerConnected, this, args);
        }
        
        internal void RaisePeerDisconnected(PeerConnectionEventArgs args)
        {
			Mode.HandlePeerDisconnected(args.PeerID);
            Toolbox.RaiseAsyncEvent<PeerConnectionEventArgs>(PeerDisconnected, this, args);
        }

        internal void RaisePeersFound(PeersAddedEventArgs args)
        {
            Toolbox.RaiseAsyncEvent<PeersAddedEventArgs>(PeersFound, this, args);
        }

        internal void RaisePieceHashed(PieceHashedEventArgs args)
        {
            int index = args.PieceIndex;
            TorrentFile[] files = this._torrent.Files;
            
            for (int i = 0; i < files.Length; i++)
                if (index >= files[i].StartPieceIndex && index <= files[i].EndPieceIndex)
                    files[i].BitField[index - files[i].StartPieceIndex] = args.HashPassed;

            if (args.HashPassed)
            {
                List<PeerId> connected = Peers.ConnectedPeers;
                for (int i = 0; i < connected.Count; i++)
                    connected[i].IsAllowedFastPieces.Remove(index);
            }

            Toolbox.RaiseAsyncEvent<PieceHashedEventArgs>(PieceHashed, this, args);
        }

        internal void RaiseTorrentStateChanged(TorrentStateChangedEventArgs e)
        {
            // Whenever we have a state change, we need to make sure that we flush the buffers.
            // For example, Started->Paused, Started->Stopped, Downloading->Seeding etc should all
            // flush to disk.
            Toolbox.RaiseAsyncEvent<TorrentStateChangedEventArgs>(TorrentStateChanged, this, e);
        }

        /// <summary>
        /// Raise the connection attempt failed event
        /// </summary>
        /// <param name="args"></param>
        internal void RaiseConnectionAttemptFailed(PeerConnectionFailedEventArgs args)
        {
            Toolbox.RaiseAsyncEvent<PeerConnectionFailedEventArgs>(this.ConnectionAttemptFailed, this, args);
        }

        internal void UpdateLimiters ()
        {
            DownloadLimiter.UpdateChunks (Settings.MaxDownloadSpeed, Monitor.DownloadSpeed);
            UploadLimiter.UpdateChunks (Settings.MaxUploadSpeed, Monitor.UploadSpeed);
        }
        #endregion Internal Methods

        #region Private Methods

        void CheckMetadata()
        {
            if (!HasMetadata)
                throw new InvalidOperationException("This action cannot be performed until metadata has been retrieved");
        }

        private void CheckRegisteredAndDisposed()
        {
            if (_engine == null)
                throw new TorrentException("This manager has not been registed with an Engine");
            if (_engine.Disposed)
                throw new InvalidOperationException("The registered engine has been disposed");
        }

        internal PiecePicker CreateStandardPicker()
        {
            PiecePicker picker;
            if (ClientEngine.SupportsEndgameMode)
                picker = new EndGameSwitcher(new StandardPicker(), new EndGamePicker(), _torrent.PieceLength / Piece.BlockSize, this);
            else
                picker = new StandardPicker();
            picker = new RandomisedPicker(picker);
            picker = new RarestFirstPicker(picker);
            picker = new PriorityPicker(picker);
            return picker;
        }

#if !DISABLE_DHT
        private void DhtPeersFound(object o, PeersFoundEventArgs e)
        {
            if (InfoHash != e.InfoHash)
                return;
            
            ClientEngine.MainLoop.Queue (delegate {
                int count = AddPeersCore(e.Peers);
                RaisePeersFound(new DhtPeersAdded(this, count, e.Peers.Count));
            });
        }
#endif

        public void LoadFastResume(FastResume data)
        {
            Check.Data(data);
            CheckMetadata();
            if (State != TorrentState.Stopped)
                throw new InvalidOperationException("Can only load FastResume when the torrent is stopped");
            if (InfoHash != data.Infohash || _torrent.Pieces.Count != data.Bitfield.Length)
                throw new ArgumentException("The fast resume data does not match this torrent", "fastResumeData");

            _bitfield.From(data.Bitfield);
            for (var i = 0; i < _torrent.Pieces.Count; i++)
                RaisePieceHashed (new PieceHashedEventArgs (this, i, _bitfield[i]));

            _hashChecked = true;
        }

        public FastResume SaveFastResume()
        {
            CheckMetadata();
            if (!HashChecked)
                throw new InvalidOperationException ("Fast resume data cannot be created when the TorrentManager has not been hash checked");
            return new FastResume(InfoHash, _bitfield);
        }

        void VerifyHashState ()
        {
            // FIXME: I should really just ensure that zero length files always exist on disk. If the first file is
            // a zero length file and someone deletes it after the first piece has been written to disk, it will
            // never be recreated. If the downloaded data requires this file to exist, we have an issue.
            if (HasMetadata) {
                foreach (var file in Torrent.Files)
                    if (!file.BitField.AllFalse && _hashChecked && file.Length > 0)
                        _hashChecked &= Engine.DiskManager.CheckFileExists (this, file);
            }
        }

        #endregion Private Methods

        internal void HandlePeerConnected(PeerId id, Direction direction)
        {
            // The only message sent/received so far is the Handshake message.
            // The current mode decides what additional messages need to be sent.
			Mode.HandlePeerConnected(id, direction);
            RaisePeerConnected(new PeerConnectionEventArgs(this, id, direction));
        }
    }
}
