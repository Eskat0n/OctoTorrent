namespace OctoTorrent.Tests.Client
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using NUnit.Framework;
    using BEncoding;
    using OctoTorrent.Client;

    [TestFixture]
    public class PeerTests
    {
        private List<Peer> _peers;

        [SetUp]
        public void Setup()
        {
            _peers = new List<Peer>();
            for (var i = 0; i < 10; i++)
            {
                var uri = new Uri(string.Format("tcp://192.168.0.{0}:1", i));
                _peers.Add(new Peer(new string(i.ToString(CultureInfo.InvariantCulture)[0], 20), uri));
            }
            _peers.Add(new Peer(new string('a', 20), new Uri("tcp://255.255.255.255:6530")));
            _peers.Add(new Peer(new string('b', 20), new Uri("tcp://255.0.0.0:123")));
            _peers.Add(new Peer(new string('c', 20), new Uri("tcp://0.0.255.0:312")));
            _peers.Add(new Peer(new string('a', 20), new Uri("tcp://0.0.0.255:3454")));
        }

        [Test]
        public void CompactPeer()
        {
            const string peerId = "12345abcde12345abcde";

            var uri = new Uri("tcp://192.168.0.5:12345");
            var p = new Peer(peerId, uri);

            var compact = p.CompactPeer();
            var peer = Peer.Decode(compact)[0];

            Assert.AreEqual(p.ConnectionUri, peer.ConnectionUri, "#1");
        }

        [Test]
        public void CorruptDictionary()
        {
            var list = new BEncodedList();
            var dictionary = new BEncodedDictionary();

            list.Add(dictionary);
            IList<Peer> decoded = Peer.Decode(list);
            Assert.AreEqual(0, decoded.Count, "#1");
        }

        [Test]
        public void CorruptList()
        {
            BEncodedList list = new BEncodedList();
            for (int i = 0; i < _peers.Count; i++)
                list.Add((BEncodedString) _peers[i].CompactPeer());

            list.Insert(2, new BEncodedNumber(5));
            VerifyDecodedPeers(Peer.Decode(list));

            list.Clear();
            list.Add(new BEncodedString(new byte[3]));
            IList<Peer> decoded = Peer.Decode(list);
            Assert.AreEqual(0, decoded.Count, "#1");
        }

        [Test]
        public void CorruptString()
        {
            IList<Peer> p = Peer.Decode((BEncodedString)"1234");
            Assert.AreEqual(0, p.Count, "#1");

            byte[] b = new byte[] { 255, 255, 255, 255, 255, 255 };
            p = Peer.Decode((BEncodedString)b);
            Assert.AreEqual(1, p.Count, "#2");

            b = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            p = Peer.Decode((BEncodedString)b);
            Assert.AreEqual(1, p.Count, "#3");
        }

        [Test]
        public void DecodeList()
        {
            // List of String
            BEncodedList list = new BEncodedList();
            foreach (Peer p in _peers)
                list.Add((BEncodedString)p.CompactPeer());
           
            VerifyDecodedPeers(Peer.Decode(list));
        }

        [Test]
        public void DecodeDictionary()
        {
            BEncodedList list = new BEncodedList();
            foreach (Peer p in _peers)
            {
                BEncodedDictionary dict = new BEncodedDictionary();
                dict.Add("ip", (BEncodedString)p.ConnectionUri.Host);
                dict.Add("port", (BEncodedNumber)p.ConnectionUri.Port);
                dict.Add("peer id", (BEncodedString)p.PeerId);
                list.Add(dict);
            }

            VerifyDecodedPeers(Peer.Decode(list));
        }

        [Test]
        public void DecodeCompact()
        {
            byte[] bytes = new byte[_peers.Count * 6];
            for (int i = 0; i < _peers.Count; i++)
                _peers[i].CompactPeer(bytes, i * 6);
            VerifyDecodedPeers(Peer.Decode((BEncodedString)bytes));
        }



        private void VerifyDecodedPeers(List<Peer> decoded)
        {
            Assert.AreEqual(_peers.Count, decoded.Count, "#1");
            foreach (Peer dec in decoded)
                Assert.IsTrue(_peers.Exists(delegate(Peer p) { return p.ConnectionUri.Equals(dec.ConnectionUri); }));
        }
    }
}
