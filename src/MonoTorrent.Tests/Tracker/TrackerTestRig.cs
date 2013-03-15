namespace OctoTorrent.Tests.Tracker
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Net;
    using OctoTorrent.BEncoding;
    using OctoTorrent.Tracker;
    using OctoTorrent.Tracker.Listeners;

    public class CustomComparer : IPeerComparer
    {
        #region IPeerComparer Members

        public object GetKey(AnnounceParameters parameters)
        {
            return parameters.Uploaded;
        }

        #endregion

        public new bool Equals(object left, object right)
        {
            return left.Equals(right);
        }

        public int GetHashCode(object obj)
        {
            return obj.GetHashCode();
        }
    }

    public class CustomListener : ListenerBase
    {
        public override bool Running
        {
            get { return true; }
        }

        public BEncodedValue Handle(PeerDetails peerDetails, ITrackable trackable)
        {
            var c = new NameValueCollection();
            c.Add("info_hash", trackable.InfoHash.UrlEncode());
            c.Add("peer_id", peerDetails.PeerId);
            c.Add("port", peerDetails.Port.ToString(CultureInfo.InvariantCulture));
            c.Add("uploaded", peerDetails.Uploaded.ToString(CultureInfo.InvariantCulture));
            c.Add("downloaded", peerDetails.Downloaded.ToString(CultureInfo.InvariantCulture));
            c.Add("left", peerDetails.Remaining.ToString(CultureInfo.InvariantCulture));
            c.Add("compact", "0");

            return base.Handle(c, peerDetails.ClientAddress, false);
        }

        public override void Start()
        {
        }

        public override void Stop()
        {
        }
    }

    public class Trackable : ITrackable
    {
        private readonly InfoHash infoHash;
        private readonly string name;


        public Trackable(InfoHash infoHash, string name)
        {
            this.infoHash = infoHash;
            this.name = name;
        }

        #region ITrackable Members

        public InfoHash InfoHash
        {
            get { return infoHash; }
        }

        public string Name
        {
            get { return name; }
        }

        #endregion
    }

    public class PeerDetails
    {
        public IPAddress ClientAddress;
        public long Downloaded;
        public int Port;
        public long Remaining;
        public long Uploaded;
        public string PeerId;
        public ITrackable Trackable;
    }

    public class TrackerTestRig : IDisposable
    {
        private readonly Random _random = new Random(1000);

        public readonly CustomListener Listener;

        public List<PeerDetails> Peers;
        public List<Trackable> Trackables;
        public readonly Tracker Tracker;

        public TrackerTestRig()
        {
            Tracker = new Tracker();
            Listener = new CustomListener();
            Tracker.RegisterListener(Listener);

            GenerateTrackables();
            GeneratePeers();
        }

        #region IDisposable Members

        public void Dispose()
        {
            Tracker.Dispose();
            Listener.Stop();
        }

        #endregion

        private void GenerateTrackables()
        {
            Trackables = new List<Trackable>();
            for (var i = 0; i < 10; i++)
            {
                var infoHash = new byte[20];
                _random.NextBytes(infoHash);
                Trackables.Add(new Trackable(new InfoHash(infoHash), i.ToString()));
            }
        }

        private void GeneratePeers()
        {
            Peers = new List<PeerDetails>();
            for (var i = 0; i < 100; i++)
            {
                var peerDetails = new PeerDetails
                            {
                                ClientAddress = IPAddress.Parse(string.Format("127.0.{0}.2", i)),
                                Downloaded = (int) (10000*_random.NextDouble()),
                                PeerId = string.Format("-----------------{0:0.000}", i),
                                Port = _random.Next(65000),
                                Remaining = _random.Next(10000, 100000),
                                Uploaded = _random.Next(10000, 100000)
                            };
                Peers.Add(peerDetails);
            }
        }
    }
}