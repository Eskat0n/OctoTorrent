namespace OctoTorrent.Tests.Client
{
    using System;
    using NUnit.Framework;
    using OctoTorrent.Client;

    [TestFixture]
    public class PeerTest
    {
        [Test]
        public void CompactPeerTest()
        {
            const string peerId = "12345abcde12345abcde";

            var uri = new Uri("tcp://192.168.0.5:12345");
            var p = new Peer(peerId, uri);
            var compact = p.CompactPeer();
            var peer = Peer.Decode(compact)[0];

            Assert.AreEqual(p.ConnectionUri, peer.ConnectionUri);
        }
    }
}
