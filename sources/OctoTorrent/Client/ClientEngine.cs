//
// ClientEngine.cs
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
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using BEncoding;
    using Common;
    using PieceWriter;
    using PieceWriters;

    /// <summary>
    ///   The Engine that contains the TorrentManagers
    /// </summary>
    public class ClientEngine : IDisposable
    {
        internal static readonly MainLoop MainLoop = new MainLoop("Client Engine Loop");

        private static readonly Random Random = new Random();

        #region Global Constants

        // To support this I need to ensure that the transition from
        // InitialSeeding -> Regular seeding either closes all existing
        // connections or sends HaveAll messages, or sends HaveMessages.
        public const bool SupportsInitialSeed = true;
        public const bool SupportsLocalPeerDiscovery = true;
        public const bool SupportsWebSeed = true;
        public const bool SupportsExtended = true;
        public const bool SupportsFastPeer = true;
        public const bool SupportsEncryption = true;
        public const bool SupportsEndgameMode = true;
#if !DISABLE_DHT
        public const bool SupportsDht = true;
#else          
        public const bool SupportsDht = false;
#endif
        internal const int TickLength = 500; // A logic tick will be performed every TickLength miliseconds

        #endregion

        #region Events

        public event EventHandler<StatsUpdateEventArgs> StatsUpdate;
        public event EventHandler<CriticalExceptionEventArgs> CriticalException;

        public event EventHandler<TorrentEventArgs> TorrentRegistered;
        public event EventHandler<TorrentEventArgs> TorrentUnregistered;

        #endregion

        #region Member Variables

        internal static readonly BufferManager BufferManager = new BufferManager();
        private readonly ConnectionManager connectionManager;

        private IDhtEngine dhtEngine;
        private readonly DiskManager diskManager;
        private bool disposed;
        private bool isRunning;
        private readonly PeerListener listener;

        private readonly ListenManager listenManager;
                                       // Listens for incoming connections and passes them off to the correct TorrentManager

        private readonly LocalPeerManager localPeerManager;
        private readonly LocalPeerListener localPeerListener;
        private readonly string peerId;
        private readonly EngineSettings settings;
        private int tickCount;
        private readonly List<TorrentManager> torrents;
        private readonly ReadOnlyCollection<TorrentManager> torrentsReadonly;
        private RateLimiterGroup uploadLimiter;
        private RateLimiterGroup downloadLimiter;
        private readonly IEnumerable<FastResume> _fastResume;

        #endregion

        #region Properties

        public ConnectionManager ConnectionManager
        {
            get { return connectionManager; }
        }

#if !DISABLE_DHT
        public IDhtEngine DhtEngine
        {
            get { return dhtEngine; }
        }
#endif

        public DiskManager DiskManager
        {
            get { return diskManager; }
        }

        public bool Disposed
        {
            get { return disposed; }
        }

        public PeerListener Listener
        {
            get { return listener; }
        }

        public bool LocalPeerSearchEnabled
        {
            get { return localPeerListener.Status != ListenerStatus.NotListening; }
            set
            {
                if (value && !LocalPeerSearchEnabled)
                    localPeerListener.Start();
                else if (!value && LocalPeerSearchEnabled)
                    localPeerListener.Stop();
            }
        }

        public bool IsRunning
        {
            get { return isRunning; }
        }

        public string PeerId
        {
            get { return peerId; }
        }

        public EngineSettings Settings
        {
            get { return settings; }
        }

        public IList<TorrentManager> Torrents
        {
            get { return torrentsReadonly; }
        }

        #endregion

        #region Constructors

        public ClientEngine(EngineSettings settings, string peerId = null)
            : this(settings, new DiskWriter(), peerId)
        {
        }

        public ClientEngine(EngineSettings settings, PieceWriter writer, string peerId = null)
            : this(settings, new SocketListener(new IPEndPoint(IPAddress.Any, 0)), writer, peerId)

        {
        }

        public ClientEngine(EngineSettings settings, PeerListener listener, string peerId = null)
            : this(settings, listener, new DiskWriter(), peerId)
        {
        }

        public ClientEngine(EngineSettings settings, PeerListener listener, PieceWriter writer, string peerId = null)
        {
            Check.Settings(settings);
            Check.Listener(listener);
            Check.Writer(writer);

            this.listener = listener;
            this.settings = settings;

            if (settings.FastResumePath != null && File.Exists(settings.FastResumePath))
            {
                var encodedListData = File.ReadAllBytes(settings.FastResumePath);
                var encodedList = (BEncodedList) BEncodedValue.Decode(encodedListData);

                _fastResume = encodedList.Cast<BEncodedDictionary>()
                    .Select(x => new FastResume(x));
            }

            connectionManager = new ConnectionManager(this);
            RegisterDht(new NullDhtEngine());
            diskManager = new DiskManager(this, writer);
            listenManager = new ListenManager(this);
            MainLoop.QueueTimeout(TimeSpan.FromMilliseconds(TickLength), () =>
                                                                             {
                                                                                 if (IsRunning && !disposed)
                                                                                     LogicTick();
                                                                                 return !disposed;
                                                                             });
            torrents = new List<TorrentManager>();
            torrentsReadonly = new ReadOnlyCollection<TorrentManager>(torrents);
            CreateRateLimiters();
            this.peerId = peerId ?? GeneratePeerId();

            localPeerListener = new LocalPeerListener(this);
            localPeerManager = new LocalPeerManager();
            LocalPeerSearchEnabled = SupportsLocalPeerDiscovery;
            listenManager.Register(listener);
            // This means we created the listener in the constructor
            if (listener.Endpoint.Port == 0)
                listener.ChangeEndpoint(new IPEndPoint(IPAddress.Any, settings.ListenPort));
        }

        private void CreateRateLimiters()
        {
            var downloader = new RateLimiter();
            downloadLimiter = new RateLimiterGroup();
            downloadLimiter.Add(new DiskWriterLimiter(DiskManager));
            downloadLimiter.Add(downloader);

            var uploader = new RateLimiter();
            uploadLimiter = new RateLimiterGroup();
            downloadLimiter.Add(new DiskWriterLimiter(DiskManager));
            uploadLimiter.Add(uploader);

            MainLoop.QueueTimeout(TimeSpan.FromSeconds(1), () =>
                                                               {
                                                                   downloader.UpdateChunks(
                                                                       Settings.GlobalMaxDownloadSpeed,
                                                                       TotalDownloadSpeed);
                                                                   uploader.UpdateChunks(Settings.GlobalMaxUploadSpeed,
                                                                                         TotalUploadSpeed);
                                                                   return !disposed;
                                                               });
        }

        #endregion

        #region Methods

        public void ChangeListenEndpoint(IPEndPoint endpoint)
        {
            Check.Endpoint(endpoint);

            Settings.ListenPort = endpoint.Port;
            listener.ChangeEndpoint(endpoint);
        }

        private void CheckDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        public bool Contains(InfoHash infoHash)
        {
            CheckDisposed();
            if (infoHash == null)
                return false;

            return torrents.Exists(m => m.InfoHash.Equals(infoHash));
        }

        public bool Contains(Torrent torrent)
        {
            CheckDisposed();
            if (torrent == null)
                return false;

            return Contains(torrent.InfoHash);
        }

        public bool Contains(TorrentManager manager)
        {
            CheckDisposed();
            if (manager == null)
                return false;

            return Contains(manager.Torrent);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            MainLoop.QueueWait(() =>
                                   {                                      
                                       dhtEngine.Dispose();
                                       diskManager.Dispose();
                                       listenManager.Dispose();
                                       localPeerListener.Stop();
                                       localPeerManager.Dispose();
                                       MainLoop.Dispose();
                                   });
        }

        private static string GeneratePeerId()
        {
            var sb = new StringBuilder(20);

            sb.Append(VersionInfo.ClientVersion);
            lock (Random)
                while (sb.Length < 20)
                    sb.Append(Random.Next(0, 9));

            return sb.ToString();
        }

        public void PauseAll()
        {
            CheckDisposed();
            MainLoop.QueueWait(() =>
                                   {
                                       foreach (var manager in torrents)
                                           manager.Pause();
                                   });
        }

        public void SaveFastResume()
        {
            if (string.IsNullOrWhiteSpace(settings.FastResumePath)) 
                return;

            var encodedList = new BEncodedList();
            var fastResumeData = torrentsReadonly
                .Where(x => x.HashChecked)
                .Select(tm => tm.SaveFastResume().Encode());
            foreach (var data in fastResumeData)
                encodedList.Add(data);

            File.WriteAllBytes(settings.FastResumePath, encodedList.Encode());
        }

        public void Register(TorrentManager manager)
        {
            CheckDisposed();
            Check.Manager(manager);

            MainLoop.QueueWait(() =>
                                   {
                                       if (manager.Engine != null)
                                           throw new TorrentException("This manager has already been registered");

                                       if (Contains(manager.Torrent))
                                           throw new TorrentException(
                                               "A manager for this torrent has already been registered");
                                       torrents.Add(manager);

                                       manager.PieceHashed += PieceHashed;
                                       manager.Engine = this;
                                       manager.DownloadLimiter.Add(downloadLimiter);
                                       manager.UploadLimiter.Add(uploadLimiter);

                                       if (_fastResume != null)
                                       {
                                           var fastResume = _fastResume
                                               .SingleOrDefault(fr => manager.InfoHash == fr.Infohash);
                                           if (fastResume != null)
                                               manager.LoadFastResume(fastResume);
                                       }

                                       if (dhtEngine != null && manager.Torrent != null && manager.Torrent.Nodes != null &&
                                           dhtEngine.State != DhtState.Ready)
                                       {
                                           try
                                           {
                                               dhtEngine.Add(manager.Torrent.Nodes);
                                           }
                                           catch
                                           {
                                               // FIXME: Should log this somewhere, though it's not critical
                                           }
                                       }
                                   });

            if (TorrentRegistered != null)
                TorrentRegistered(this, new TorrentEventArgs(manager));
        }

        public void RegisterDht(IDhtEngine engine)
        {
            MainLoop.QueueWait(() =>
                                   {
                                       if (dhtEngine != null)
                                       {
                                           dhtEngine.StateChanged -= DhtEngineStateChanged;
                                           dhtEngine.Stop();
                                           dhtEngine.Dispose();
                                       }
                                       dhtEngine = engine ?? new NullDhtEngine();
                                   });

            dhtEngine.StateChanged += DhtEngineStateChanged;
        }

        private void DhtEngineStateChanged(object o, EventArgs e)
        {
            if (dhtEngine.State != DhtState.Ready)
                return;

            MainLoop.Queue(() =>
                               {
                                   foreach (var manager in torrents.Where(manager => manager.CanUseDht))
                                   {
                                       dhtEngine.Announce(manager.InfoHash, Listener.Endpoint.Port);
                                       dhtEngine.GetPeers(manager.InfoHash);
                                   }
                               });
        }

        public void StartAll()
        {
            CheckDisposed();
            MainLoop.QueueWait(() =>
                                   {
                                       foreach (var torrentManager in torrents)
                                           torrentManager.Start();
                                   });
        }

        public void StopAll()
        {
            CheckDisposed();

            MainLoop.QueueWait(() =>
                                   {
                                       foreach (var torrentManager in torrents)
                                           torrentManager.Stop();
                                   });
        }

        public int TotalDownloadSpeed
        {
            get { return torrents.Sum(x => x.Monitor.DownloadSpeed); }
        }

        public int TotalUploadSpeed
        {
            get { return torrents.Sum(x => x.Monitor.UploadSpeed); }
        }

        public void Unregister(TorrentManager manager)
        {
            CheckDisposed();
            Check.Manager(manager);

            MainLoop.QueueWait(() =>
                                   {
                                       if (manager.Engine != this)
                                           throw new TorrentException(
                                               "The manager has not been registered with this engine");

                                       if (manager.State != TorrentState.Stopped)
                                           throw new TorrentException(
                                               "The manager must be stopped before it can be unregistered");

                                       torrents.Remove(manager);

                                       manager.PieceHashed -= PieceHashed;
                                       manager.Engine = null;
                                       manager.DownloadLimiter.Remove(downloadLimiter);
                                       manager.UploadLimiter.Remove(uploadLimiter);
                                   });

            if (TorrentUnregistered != null)
                TorrentUnregistered(this, new TorrentEventArgs(manager));
        }

        #endregion

        #region Private/Internal methods

        internal void Broadcast(TorrentManager manager)
        {
            if (LocalPeerSearchEnabled)
                localPeerManager.Broadcast(manager);
        }

        private void LogicTick()
        {
            tickCount++;

            if (tickCount%(1000/TickLength) == 0)
            {
                diskManager.WriteLimiter.UpdateChunks(settings.MaxWriteRate, diskManager.WriteRate);
                diskManager.ReadLimiter.UpdateChunks(settings.MaxReadRate, diskManager.ReadRate);
            }

            ConnectionManager.TryConnect();
            foreach (var torrentManager in torrents)
                torrentManager.Mode.Tick(tickCount);

            RaiseStatsUpdate(new StatsUpdateEventArgs());
        }

        internal void RaiseCriticalException(CriticalExceptionEventArgs e)
        {
            Toolbox.RaiseAsyncEvent(CriticalException, this, e);
        }

        private void PieceHashed(object sender, PieceHashedEventArgs e)
        {
            if (e.TorrentManager.State != TorrentState.Hashing)
                diskManager.QueueFlush(e.TorrentManager, e.PieceIndex);
        }

        internal void RaiseStatsUpdate(StatsUpdateEventArgs args)
        {
            Toolbox.RaiseAsyncEvent(StatsUpdate, this, args);
        }

        internal void Start()
        {
            CheckDisposed();
            isRunning = true;
            if (listener.Status == ListenerStatus.NotListening)
                listener.Start();
        }

        internal void Stop()
        {
            CheckDisposed();
            // If all the torrents are stopped, stop ticking
            isRunning = torrents.Exists(x => x.State != TorrentState.Stopped);
            if (!isRunning)
                listener.Stop();
        }

        #endregion
    }
}