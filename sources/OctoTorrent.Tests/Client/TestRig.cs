namespace OctoTorrent.Tests.Client
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using OctoTorrent.Client;
    using OctoTorrent.Client.Connections;
    using BEncoding;
    using OctoTorrent.Client.Tracker;
    using OctoTorrent.Client.PieceWriters;
    using System.Net.Sockets;
    using System.Net;
    using System.Threading;
    using NUnit.Framework;
    using OctoTorrent.Common;

    public class TestWriter : PieceWriter
    {
        public readonly List<TorrentFile> FilesThatExist = new List<TorrentFile>();
        public readonly List<TorrentFile> DoNotReadFrom = new List<TorrentFile>();
        public bool DontWrite;

        private readonly List<String> _paths = new List<string>();

        public override int Read(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            if (DoNotReadFrom.Contains(file))
                return 0;

            if (!_paths.Contains(file.FullPath))
                _paths.Add(file.FullPath);

            if (!DontWrite)
                for (var i = 0; i < count; i++)
                    buffer[bufferOffset + i] = (byte)(bufferOffset + i);
            return count;
        }

        public override void Write(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
        }

        public override void Close(TorrentFile file)
        {
        }

        public override void Flush(TorrentFile file)
        {
        }

        public override bool Exists(TorrentFile file)
        {
            return FilesThatExist.Contains(file);
        }

        public override void Move(string oldPath, string newPath, bool ignoreExisting)
        {
        }
    }

    public class CustomTracker : Tracker
    {
        public readonly List<DateTime> AnnouncedAt = new List<DateTime>();
        public readonly List<DateTime> ScrapedAt = new List<DateTime>();

        public bool FailAnnounce;
        public bool FailScrape;

        public CustomTracker(Uri uri)
            : base(uri)
        {
            CanAnnounce = true;
            CanScrape = true;
        }

        public override void Announce(AnnounceParameters parameters, object state)
        {
            RaiseBeforeAnnounce();
            AnnouncedAt.Add(DateTime.Now);
            RaiseAnnounceComplete(new AnnounceResponseEventArgs(this, state, !FailAnnounce));
        }

        public override void Scrape(ScrapeParameters parameters, object state)
        {
            RaiseBeforeScrape();
            ScrapedAt.Add(DateTime.Now);
            RaiseScrapeComplete(new ScrapeResponseEventArgs(this, state, !FailScrape));
        }

        public void AddPeer(Peer p)
        {
            var id = new TrackerConnectionID(this, false, TorrentEvent.None, new ManualResetEvent(false));
            var e = new AnnounceResponseEventArgs(this, id, true);
            e.Peers.Add(p);
            RaiseAnnounceComplete(e);
            Assert.IsTrue(id.WaitHandle.WaitOne(1000, true), "#1 Tracker never raised the AnnounceComplete event");
        }

        public void AddFailedPeer(Peer p)
        {
            var id = new TrackerConnectionID(this, true, TorrentEvent.None, new ManualResetEvent(false));
            var e = new AnnounceResponseEventArgs(this, id, false);
            e.Peers.Add(p);
            RaiseAnnounceComplete(e);
            Assert.IsTrue(id.WaitHandle.WaitOne(1000, true), "#2 Tracker never raised the AnnounceComplete event");
        }

        public override string ToString()
        {
            return Uri.ToString();
        }
    }

    public class CustomConnection : IConnection
    {
        public string Name;
        public event EventHandler BeginReceiveStarted;
        public event EventHandler EndReceiveStarted;

        public event EventHandler BeginSendStarted;
        public event EventHandler EndSendStarted;

        private readonly Socket _socket;
        private readonly bool _incoming;

        public int? ManualBytesReceived { get; set; }
        public int? ManualBytesSent { get; set; }
        public bool SlowConnection { get; set; }

        public CustomConnection(Socket socket, bool incoming)
        {
            _socket = socket;
            _incoming = incoming;
        }

        public byte[] AddressBytes
        {
            get { return ((IPEndPoint)_socket.RemoteEndPoint).Address.GetAddressBytes(); }
        }

        public bool Connected
        {
            get { return _socket.Connected; }
        }

        public bool CanReconnect
        {
            get { return false; }
        }

        public bool IsIncoming
        {
            get { return _incoming; }
        }

        public EndPoint EndPoint
        {
            get { return _socket.RemoteEndPoint; }
        }

        public IAsyncResult BeginConnect(AsyncCallback callback, object state)
        {
            throw new InvalidOperationException();
        }

        public void EndConnect(IAsyncResult result)
        {
            throw new InvalidOperationException();
        }

        public IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (BeginReceiveStarted != null)
                BeginReceiveStarted (this, EventArgs.Empty);
            if (SlowConnection)
                count = 1;
            return _socket.BeginReceive(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndReceive(IAsyncResult result)
        {
            if (EndReceiveStarted != null)
                EndReceiveStarted(null, EventArgs.Empty);

            if (ManualBytesReceived.HasValue)
                return ManualBytesReceived.Value;

            try
            {
                return _socket.EndReceive(result);
            }
            catch (ObjectDisposedException)
            {
                return 0;
            }
        }

        public IAsyncResult BeginSend(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (BeginSendStarted != null)
                BeginSendStarted(null, EventArgs.Empty);

            if (SlowConnection)
                count = 1;
            return _socket.BeginSend(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndSend(IAsyncResult result)
        {
            if (EndSendStarted != null)
                EndSendStarted(null, EventArgs.Empty);

            if (ManualBytesSent.HasValue)
                return ManualBytesSent.Value;

            try
            {
                return _socket.EndSend(result);
            }
            catch (ObjectDisposedException)
            {
                return 0;
            }
        }

        //private bool disposed;
        public void Dispose()
        {
           // disposed = true;
            _socket.Close();
        }

        public override string ToString()
        {
            return Name;
        }

        public Uri Uri
        {
            get { return new Uri("tcp://127.0.0.1:1234"); }
        }


        public int Receive (byte[] buffer, int offset, int count)
        {
            var r = BeginReceive (buffer, offset, count, null, null);
            if (!r.AsyncWaitHandle.WaitOne (TimeSpan.FromSeconds (4)))
                throw new Exception ("Could not receive required data");
            return EndReceive (r);
        }

        public int Send (byte[] buffer, int offset, int count)
        {
            var r = BeginSend (buffer, offset, count, null, null);
            if (!r.AsyncWaitHandle.WaitOne (TimeSpan.FromSeconds (4)))
                throw new Exception ("Could not receive required data");
            return EndSend (r);
        }
    }

    public class CustomListener : PeerListener
    {
        public override void Start()
        {
        }

        public override void Stop()
        {
        }

        public CustomListener()
            :base(new IPEndPoint(IPAddress.Any, 0))
        {
        }

        public void Add(TorrentManager manager, IConnection connection)
        {
            var peer = new Peer("", new Uri("tcp://12.123.123.1:2342"));
            base.RaiseConnectionReceived(peer, connection, manager);
        }
    }

    public class ConnectionPair : IDisposable
    {
        public readonly CustomConnection Incoming;
        public readonly CustomConnection Outgoing;

        private readonly TcpListener _socketListener;

        public ConnectionPair(int port)
        {
            _socketListener = new TcpListener(IPAddress.Loopback, port);
            _socketListener.Start();

            var socketA = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socketA.Connect(IPAddress.Loopback, port);
            var socketB = _socketListener.AcceptSocket();

            Incoming = new CustomConnection(socketA, true);
            Outgoing = new CustomConnection(socketB, false);
            _socketListener.Stop();
        }

        public void Dispose()
        {
            Incoming.Dispose();
            Outgoing.Dispose();
            _socketListener.Stop();
        }
    }

    public class TestRig : IDisposable
    {
        static readonly Random Random = new Random(1000);
        static int _port = 10000;
        private BEncodedDictionary _torrentDict;
        private readonly ClientEngine _engine;
        private readonly CustomListener _listener;
        private TorrentManager _manager;
        private Torrent _torrent;

        public int BlocksPerPiece
        {
            get { return _torrent.PieceLength / (16 * 1024); }
        }

        public int Pieces
        {
            get { return _torrent.Pieces.Count; }
        }

        public int TotalBlocks
        {
            get
            {
                int count = 0;
                long size = _torrent.Size;
                while (size > 0)
                {
                    count++;
                    size -= Piece.BlockSize;
                }
                return count;
            }
        }

        public TestWriter Writer {
            get; set;
        }

        public ClientEngine Engine
        {
            get { return _engine; }
        }

        public CustomListener Listener
        {
            get { return _listener; }
        }

        public TorrentManager Manager
        {
            get { return _manager; }
        }

        public bool MetadataMode { get; private set; }

        public string MetadataPath { get; set; }

        public Torrent Torrent
        {
            get { return _torrent; }
        }

        public BEncodedDictionary TorrentDict
        {
            get { return _torrentDict; }
        }

        public CustomTracker Tracker
        {
            get { return (CustomTracker)_manager.TrackerManager.CurrentTracker; }
        }

        private readonly string _savePath;
        private readonly int _piecelength;
        private readonly string[][] _tier;

        public void AddConnection(IConnection connection)
        {
            _listener.Add(connection.IsIncoming
                              ? null
                              : _manager,
                          connection);
        }

        public PeerId CreatePeer(bool processingQueue)
        {
            return CreatePeer(processingQueue, true);
        }

        public PeerId CreatePeer(bool processingQueue, bool supportsFastPeer)
        {
            var sb = new StringBuilder();

            for (var i = 0; i < 20; i++)
                sb.Append((char) Random.Next('a', 'z'));

            var peer = new Peer(sb.ToString(), new Uri("tcp://127.0.0.1:" + (_port++)));
            var id = new PeerId(peer, Manager)
                         {
                             SupportsFastPeer = supportsFastPeer, 
                             ProcessingQueue = processingQueue
                         };

            return id;
        }

        public void Dispose()
        {
            _engine.Dispose();
        }

        public void RecreateManager()
        {
            if (_manager != null)
            {
                _manager.Dispose();
                if (_engine.Contains(_manager))
                    _engine.Unregister(_manager);
            }
            _torrentDict = CreateTorrent(_piecelength, _files, _tier);
            _torrent = Torrent.Load(_torrentDict);
            _manager = MetadataMode
                           ? new TorrentManager(_torrent.infoHash, _savePath, new TorrentSettings(), MetadataPath, new RawTrackerTiers())
                           : new TorrentManager(_torrent, _savePath, new TorrentSettings());
            _engine.Register(_manager);
        }

        #region Rig Creation

        private readonly TorrentFile[] _files;

        TestRig(string savePath, int piecelength, TestWriter writer, string[][] trackers, TorrentFile[] files)
            : this (savePath, piecelength, writer, trackers, files, false)
        {
            
        }

        TestRig(string savePath, int piecelength, TestWriter writer, string[][] trackers, TorrentFile[] files, bool metadataMode)
        {
            _files = files;
            _savePath = savePath;
            _piecelength = piecelength;
            _tier = trackers;
            MetadataMode = metadataMode;
            MetadataPath = "metadataSave.torrent";
            _listener = new CustomListener();
            _engine = new ClientEngine(new EngineSettings(), _listener, writer);
            Writer = writer;

            RecreateManager();
        }

        static TestRig()
        {
            TrackerFactory.Register("custom", typeof(CustomTracker));
        }

        private static void AddAnnounces(BEncodedDictionary dict, string[][] tiers)
        {
            var announces = new BEncodedList();
            foreach (string[] tier in tiers)
            {
                var bTier = new BEncodedList();
                announces.Add(bTier);
                foreach (string s in tier)
                    bTier.Add((BEncodedString)s);
            }
            dict["announce"] = (BEncodedString)tiers[0][0];
            dict["announce-list"] = announces;
        }

        BEncodedDictionary CreateTorrent(int pieceLength, TorrentFile[] files, string[][] tier)
        {
            var dict = new BEncodedDictionary();
            var infoDict = new BEncodedDictionary();

            AddAnnounces(dict, tier);
            AddFiles(infoDict, files);
            if (files.Length == 1)
                dict["url-list"] = (BEncodedString)"http://127.0.0.1:120/announce/File1.exe";
            else
                dict["url-list"] = (BEncodedString)"http://127.0.0.1:120/announce";
            dict["creation date"] = (BEncodedNumber)(int)(DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
            dict["encoding"] = (BEncodedString)"UTF-8";
            dict["info"] = infoDict;

            return dict;
        }

        void AddFiles(BEncodedDictionary dict, IEnumerable<TorrentFile> torrentFiles)
        {
            long totalSize = _piecelength - 1;
            var bFiles = new BEncodedList();
            foreach (var torrentFile in torrentFiles)
            {
                var path = new BEncodedList();
                foreach (var split in torrentFile.Path.Split('/'))
                    path.Add((BEncodedString)split);

                var dictionary = new BEncodedDictionary();
                dictionary["path"] = path;
                dictionary["length"] = (BEncodedNumber)torrentFile.Length;
                bFiles.Add(dictionary);

                totalSize += torrentFile.Length;
            }

            dict[new BEncodedString("torrentFiles")] = bFiles;
            dict[new BEncodedString("name")] = new BEncodedString("test.torrentFiles");
            dict[new BEncodedString("piece length")] = new BEncodedNumber(_piecelength);
            dict[new BEncodedString("pieces")] = new BEncodedString(new byte[20 * (totalSize / _piecelength)]);
        }

        public static TestRig CreateSingleFile()
        {
            return new TestRig(string.Empty, StandardPieceSize(), StandardWriter(), StandardTrackers(), StandardSingleFile());
        }

        public static TestRig CreateMultiFile()
        {
            return new TestRig(string.Empty, StandardPieceSize(), StandardWriter(), StandardTrackers(), StandardMultiFile());
        }

        internal static TestRig CreateMultiFile(TorrentFile[] files, int pieceLength)
        {
            return new TestRig(string.Empty, pieceLength, StandardWriter(), StandardTrackers(), files);
        }

        public static TestRig CreateTrackers(string[][] tier)
        {
            return new TestRig(string.Empty, StandardPieceSize(), StandardWriter(), tier, StandardMultiFile());
        }

        internal static TestRig CreateMultiFile(TestWriter writer)
        {
            return new TestRig (string.Empty, StandardPieceSize (), writer, StandardTrackers (), StandardMultiFile());
        }

        internal static TestRig CreateMultiFile(int pieceSize)
        {
            return new TestRig(string.Empty, pieceSize, StandardWriter(), StandardTrackers(), StandardMultiFile());
        }

        #region Create standard fake data

        static int StandardPieceSize()
        {
            return 256 * 1024;
        }

        static TorrentFile[] StandardMultiFile()
        {
            return new[]
                       {
                           new TorrentFile("Dir1/File1", (int) (StandardPieceSize()*0.44)),
                           new TorrentFile("Dir1/Dir2/File2", (int) (StandardPieceSize()*13.25)),
                           new TorrentFile("File3", (int) (StandardPieceSize()*23.68)),
                           new TorrentFile("File4", (int) (StandardPieceSize()*2.05)),
                       };
        }

        static TorrentFile[] StandardSingleFile()
        {
            return new[] {
                 new TorrentFile ("Dir1/File1", (int)(StandardPieceSize () * 0.44))
            };
        }

        static string[][] StandardTrackers()
        {
            return new[]
                       {
                           new[] {"custom://tier1/announce1", "custom://tier1/announce2"},
                           new[] {"custom://tier2/announce1", "custom://tier2/announce2", "custom://tier2/announce3"},
                       };
        }

        static TestWriter StandardWriter()
        {
            return new TestWriter();
        }

        #endregion Create standard fake data

        #endregion Rig Creation

        internal static TestRig CreateSingleFile(int torrentSize, int pieceLength)
        {
            return CreateSingleFile(torrentSize, pieceLength, false);
        }

        internal static TestRig CreateSingleFile(int torrentSize, int pieceLength, bool metadataMode)
        {
            TorrentFile[] files = StandardSingleFile();
            files[0] = new TorrentFile (files[0].Path, torrentSize);
            return new TestRig("", pieceLength, StandardWriter(), StandardTrackers(), files, metadataMode);
        }
    }
}
