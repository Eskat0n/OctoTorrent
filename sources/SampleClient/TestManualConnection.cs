namespace OctoTorrent.SampleClient
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using BEncoding;
    using Client;
    using Client.Connections;
    using Client.PieceWriters;
    using Client.Tracker;
    using Common;

    public class CustomTracker : Tracker
    {
        public CustomTracker(Uri uri)
            : base(uri)
        {
            CanScrape = false;
        }

        public override void Announce(AnnounceParameters parameters, object state)
        {
            RaiseAnnounceComplete(new AnnounceResponseEventArgs(this, state, true));
        }

        public override void Scrape(ScrapeParameters parameters, object state)
        {
            RaiseScrapeComplete(new ScrapeResponseEventArgs(this, state, true));
        }

        public void AddPeer(Peer p)
        {
//            var id = new TrackerConnectionID(this, false, TorrentEvent.None, null);
            var e = new AnnounceResponseEventArgs(this, null, true);
            e.Peers.Add(p);
            e.Successful = true;
            RaiseAnnounceComplete(e);
        }

        public void AddFailedPeer(Peer p)
        {
            var id = new TrackerConnectionID(this, true, TorrentEvent.None, null);
            var e = new AnnounceResponseEventArgs(this, null, true);
            e.Peers.Add(p);
            e.Successful = false;
            RaiseAnnounceComplete(e);
        }
    }

    public class NullWriter : PieceWriter
    {
        public override int Read(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
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
            return false;
        }

        public override void Move(string oldPath, string newPath, bool ignoreExisting)
        {
        }
    }

    public class CustomConnection : IConnection
    {
        private readonly bool _incoming;
        private readonly string _name;
        private readonly Socket _socket;

        public CustomConnection(Socket socket, bool incoming, string name)
        {
            _name = name;
            _socket = socket;
            _incoming = incoming;
        }

        #region IConnection Members

        public byte[] AddressBytes
        {
            get { return ((IPEndPoint) _socket.RemoteEndPoint).Address.GetAddressBytes(); }
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
            return _socket.BeginReceive(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndReceive(IAsyncResult result)
        {
            Console.WriteLine("{0} - {1}", _name, "received");
            return _socket.EndReceive(result);
        }

        public IAsyncResult BeginSend(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _socket.BeginSend(buffer, offset, count, SocketFlags.None, callback, state);
        }

        public int EndSend(IAsyncResult result)
        {
            Console.WriteLine("{0} - {1}", _name, "sent");
            return _socket.EndSend(result);
        }

        public void Dispose()
        {
            _socket.Close();
        }

        public Uri Uri
        {
            get { return null; }
        }

        #endregion

        public override string ToString()
        {
            return _name;
        }
    }

    public class CustomListener : PeerListener
    {
        public CustomListener()
            : base(new IPEndPoint(IPAddress.Any, 0))
        {
        }

        public override void Start()
        {
        }

        public override void Stop()
        {
        }

        public void Add(TorrentManager manager, IConnection connection)
        {
            var peer = new Peer(string.Empty, new Uri("tcp://12.123.123.1:2342"));
            base.RaiseConnectionReceived(peer, connection, manager);
        }
    }

    public class ConnectionPair : IDisposable
    {
        public readonly IConnection Incoming;
        public readonly IConnection Outgoing;
        private readonly TcpListener _socketListener;

        public ConnectionPair(int port)
        {
            _socketListener = new TcpListener(port);
            _socketListener.Start();

            var socket1 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket1.Connect(IPAddress.Loopback, port);
            var socket2 = _socketListener.AcceptSocket();

            Incoming = new CustomConnection(socket1, true, "1A");
            Outgoing = new CustomConnection(socket2, false, "1B");
        }

        #region IDisposable Members

        public void Dispose()
        {
            Incoming.Dispose();
            Outgoing.Dispose();
            _socketListener.Stop();
        }

        #endregion
    }

    public class EngineTestRig
    {
        private readonly ClientEngine _engine;
        private readonly CustomListener _listener;
        private readonly TorrentManager _manager;
        private readonly Torrent _torrent;
        private readonly BEncodedDictionary _torrentDict;

        static EngineTestRig()
        {
            TrackerFactory.Register("custom", typeof (CustomTracker));
        }

        public EngineTestRig(string savePath, PieceWriter writer)
            : this(savePath, 256*1024, writer)
        {
        }

        public EngineTestRig(string savePath, int piecelength = 256*1024, PieceWriter writer = null)
        {
            if (writer == null)
                writer = new MemoryWriter(new NullWriter());
            _listener = new CustomListener();
            _engine = new ClientEngine(new EngineSettings(), _listener, writer);
            _torrentDict = CreateTorrent(piecelength);
            _torrent = Torrent.Load(_torrentDict);
            _manager = new TorrentManager(_torrent, savePath, new TorrentSettings());
            _engine.Register(_manager);
            //manager.Start();
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
            get { return (CustomTracker) _manager.TrackerManager.CurrentTracker; }
        }


        public void AddConnection(IConnection connection)
        {
            _listener.Add(_manager, connection);
        }

        private static BEncodedDictionary CreateTorrent(int pieceLength)
        {
            var infoDict = new BEncodedDictionary();
            infoDict[new BEncodedString("piece length")] = new BEncodedNumber(pieceLength);
            infoDict[new BEncodedString("pieces")] = new BEncodedString(new byte[20*15]);
            infoDict[new BEncodedString("length")] = new BEncodedNumber(15*256*1024 - 1);
            infoDict[new BEncodedString("name")] = new BEncodedString("test.files");

            var dict = new BEncodedDictionary();
            dict[new BEncodedString("info")] = infoDict;

            var announceTier = new BEncodedList
                                   {
                                       new BEncodedString("custom://transfers1/announce"),
                                       new BEncodedString("custom://transfers2/announce"),
                                       new BEncodedString("http://transfers3/announce")
                                   };
            var announceList = new BEncodedList {announceTier};
            dict[new BEncodedString("announce-list")] = announceList;
            return dict;
        }
    }

    internal class TestManualConnection
    {
        private readonly EngineTestRig _rig1;
        private readonly EngineTestRig _rig2;

        public TestManualConnection()
        {
            _rig1 = new EngineTestRig("Downloads1");
            _rig1.Manager.Start();
            _rig2 = new EngineTestRig("Downloads2");
            _rig2.Manager.Start();

            var p = new ConnectionPair(5151);

            _rig1.AddConnection(p.Incoming);
            _rig2.AddConnection(p.Outgoing);

            while (true)
            {
                Console.WriteLine("Connection 1A active: {0}", p.Incoming.Connected);
                Console.WriteLine("Connection 2A active: {0}", p.Outgoing.Connected);
                Thread.Sleep(1000);
            }
        }
    }
}