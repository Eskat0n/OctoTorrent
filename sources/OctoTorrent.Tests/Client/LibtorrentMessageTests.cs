namespace OctoTorrent.Tests.Client
{
    using NUnit.Framework;
    using OctoTorrent.Client.Messages.Libtorrent;
    using OctoTorrent.Client.Messages;
    using OctoTorrent.Common;

    [TestFixture]
    public class LibtorrentMessageTests
    {
        private TestRig _rig;
        private byte[] _buffer;

        [TestFixtureSetUp]
        public void GlobalSetup()
        {
            _rig = TestRig.CreateMultiFile();
        }

        [TestFixtureTearDown]
        public void GlobalTeardown()
        {
            _rig.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            _buffer = new byte[100000];
            for (var i = 0; i < _buffer.Length; i++)
                _buffer[i] = 0xff;
        }

        [Test]
        public void HandshakeSupportsTest()
        {
            var message = new ExtendedHandshakeMessage();
            var encoded = message.Encode();

            Assert.AreEqual(message.ByteLength, encoded.Length, "#1");
            Assert.IsTrue(message.Supports.Exists(delegate(ExtensionSupport s) { return s.Name.Equals(PeerExchangeMessage.Support.Name); }), "#2");
            Assert.IsTrue(message.Supports.Exists(delegate(ExtensionSupport s) { return s.Name.Equals(LTChat.Support.Name); }), "#3");
            Assert.IsTrue(message.Supports.Exists(delegate(ExtensionSupport s) { return s.Name.Equals(LTMetadata.Support.Name); }), "#4");
        }

        [Test]
        public void HandshakeDecodeTest()
        {
            var message = new ExtendedHandshakeMessage();
            byte[] data = message.Encode();
            var decoded = (ExtendedHandshakeMessage)PeerMessage.DecodeMessage(data, 0, data.Length, _rig.Manager);

            Assert.AreEqual(message.ByteLength, data.Length);
            Assert.AreEqual(message.ByteLength, decoded.ByteLength, "#1");
            Assert.AreEqual(message.LocalPort, decoded.LocalPort, "#2");
            Assert.AreEqual(message.MaxRequests, decoded.MaxRequests, "#3");
            Assert.AreEqual(message.Version, decoded.Version, "#4");
            Assert.AreEqual(message.Supports.Count, decoded.Supports.Count, "#5");

            message.Supports.ForEach(support => Assert.IsTrue(decoded.Supports.Contains(support), string.Format("#6:{0}", support.ToString())));
        }

        [Test]
        public void LTChatDecodeTest()
        {
            var m = new LTChat(LTChat.Support.MessageId, "This Is My Message");

            var data = m.Encode();
            var decoded = (LTChat)PeerMessage.DecodeMessage(data, 0, data.Length, _rig.Manager);
        
            Assert.AreEqual(m.Message, decoded.Message, "#1");
        }

        [Test]
        public void PeerExchangeMessageTest()
        {
            // Decodes as: 192.168.0.1:100
            var peer = new byte[] { 192, 168, 0, 1, 100, 0 };
            var supports = new[] { (byte)(1 | 2) }; // 1 == encryption, 2 == seeder

            byte id = PeerExchangeMessage.Support.MessageId;
            var message = new PeerExchangeMessage(id, peer, supports, null);

            var buffer = message.Encode();
            var m = (PeerExchangeMessage)PeerMessage.DecodeMessage(buffer, 0, buffer.Length, _rig.Manager);
            Assert.IsTrue(Toolbox.ByteMatch(peer, m.Added), "#1");
            Assert.IsTrue(Toolbox.ByteMatch(supports, m.AddedDotF), "#1");
        }

        /*public static void Main(string[] args)
        {
            LibtorrentMessageTests t = new LibtorrentMessageTests();
            t.GlobalSetup();
            t.Setup();
            t.HandshakeDecodeTest();
            t.LTChatDecodeTest();
            t.GlobalTeardown();
        }*/
    }
}
