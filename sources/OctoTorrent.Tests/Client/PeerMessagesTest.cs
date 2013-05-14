namespace OctoTorrent.Tests.Client
{
    using System;
    using System.Globalization;
    using System.Text;
    using NUnit.Framework;
    using OctoTorrent.Client;
    using OctoTorrent.Client.Messages;
    using OctoTorrent.Client.Messages.Standard;
    using BEncoding;
    using OctoTorrent.Client.Messages.Libtorrent;
    using OctoTorrent.Common;

    [TestFixture]
    public class PeerMessagesTest
    {
        private const int Offset = 2362;

        private TestRig _testRig;
        private byte[] _buffer;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            _buffer = new byte[100000];
            _testRig = TestRig.CreateMultiFile();
        }

        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            _testRig.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            for (var i = 0; i < _buffer.Length; i++)
                _buffer[i] = 0xff;
        }

        [TearDown]
        public void GlobalTeardown()
        {
            _testRig.Dispose();
        }

        [Test]
        public void BitFieldEncoding()
        {
            var data = new[] { true, false, false, true, false, true, false, true, false, true,
                                       false, true, false, false, false, true, true, true, false, false,
                                       false, true, false, true, false, false, true, false, true, false,
                                       true, true, false, false, true, false, false, true, true, false };

            var encoded = new BitfieldMessage(new BitField(data)).Encode();

            var message = (BitfieldMessage) PeerMessage.DecodeMessage(encoded, 0, encoded.Length, _testRig.Manager);
            Assert.AreEqual(data.Length, message.BitField.Length, "#1");
            for (var i = 0; i < data.Length; i++)
                Assert.AreEqual(data[i], message.BitField[i], "#2." + i);
        }

        [Test]
        public void BitFieldDecoding()
        {
            var buffer = new byte[] { 0x00, 0x00, 0x00, 0x04, 0x05, 0xff, 0x08, 0xAA, 0xE3, 0x00 };
            Console.WriteLine("Pieces: " + _testRig.Manager.Torrent.Pieces.Count);
            var message = (BitfieldMessage) PeerMessage.DecodeMessage(buffer, 0, 8, _testRig.Manager);

            for (var i = 0; i < 8; i++)
                Assert.IsTrue(message.BitField[i], i.ToString(CultureInfo.InvariantCulture));

            for (var i = 8; i < 12; i++)
                Assert.IsFalse(message.BitField[i], i.ToString(CultureInfo.InvariantCulture));

            Assert.IsTrue(message.BitField[12], 12.ToString(CultureInfo.InvariantCulture));
            for (var i = 13; i < 15; i++)
                Assert.IsFalse(message.BitField[i], i.ToString(CultureInfo.InvariantCulture));

            EncodeDecode(message);
        }

        [ExpectedException(typeof(MessageException))]
        [Ignore("Deliberately broken to work around bugs in azureus")]
        public void BitfieldCorrupt()
        {
            var data = new[] { true, false, false, true, false, true, false, true, false, true, false, true, false, false, false, true };
            var encoded = new BitfieldMessage(new BitField(data)).Encode();

            PeerMessage.DecodeMessage(encoded, 0, encoded.Length, _testRig.Manager);
        }

        [Test]
        public void CancelEncoding()
        {
            var length = new CancelMessage(15, 1024, 16384).Encode(_buffer, Offset);
            Assert.AreEqual("00-00-00-0D-08-00-00-00-0F-00-00-04-00-00-00-40-00", BitConverter.ToString(_buffer, Offset, length));
        }
        [Test]
        public void CancelDecoding()
        {
            EncodeDecode(new CancelMessage(563, 4737, 88888));
        }

        [Test]
        public void ChokeEncoding()
        {
            var length = new ChokeMessage().Encode(_buffer, Offset);
            Assert.AreEqual("00-00-00-01-00", BitConverter.ToString(_buffer, Offset, length));
        }

        [Test]
        public void ChokeDecoding()
        {
            EncodeDecode(new ChokeMessage());
        }

        [Test]
        public void HandshakeEncoding()
        {
            var infohash = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 0, 12, 15, 12, 52 };
            var length = new HandshakeMessage(new InfoHash (infohash), "12312312345645645678", VersionInfo.ProtocolStringV100, false, false).Encode(_buffer, Offset);

            Console.WriteLine(BitConverter.ToString(_buffer, Offset, length));
            var peerId = Encoding.ASCII.GetBytes("12312312345645645678");
            var protocolVersion = Encoding.ASCII.GetBytes(VersionInfo.ProtocolStringV100);
            Assert.AreEqual(19, _buffer[Offset], "1");
            Assert.IsTrue(Toolbox.ByteMatch(protocolVersion, 0, _buffer, Offset + 1, 19), "2");
            Assert.IsTrue(Toolbox.ByteMatch(new byte[8], 0, _buffer, Offset + 20, 8), "3");
            Assert.IsTrue(Toolbox.ByteMatch(infohash, 0, _buffer, Offset + 28, 20), "4");
            Assert.IsTrue(Toolbox.ByteMatch(peerId, 0, _buffer, Offset + 48, 20), "5");
            Assert.AreEqual(length, 68, "6");

            length = new HandshakeMessage(new InfoHash (infohash), "12312312345645645678", VersionInfo.ProtocolStringV100, true, false).Encode(_buffer, Offset);
            Assert.AreEqual(BitConverter.ToString(_buffer, Offset, length), "13-42-69-74-54-6F-72-72-65-6E-74-20-70-72-6F-74-6F-63-6F-6C-00-00-00-00-00-00-00-04-01-02-03-04-05-06-07-08-09-0A-0B-0C-0D-0E-0F-00-0C-0F-0C-34-31-32-33-31-32-33-31-32-33-34-35-36-34-35-36-34-35-36-37-38", "#7");
        }

        [Test]
        public void HandshakeDecoding()
        {
            var infohash = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 0, 12, 15, 12, 52 };
            var orig = new HandshakeMessage(new InfoHash (infohash), "12312312345645645678", VersionInfo.ProtocolStringV100);
            orig.Encode(_buffer, Offset);
            var dec = new HandshakeMessage();
            dec.Decode(_buffer, Offset, 68);
            Assert.IsTrue(orig.Equals(dec));
            Assert.AreEqual(orig.Encode(), dec.Encode());
        }



        [Test]
        public void HaveEncoding()
        {
            var length = new HaveMessage(150).Encode(_buffer, Offset);
            Assert.AreEqual("00-00-00-05-04-00-00-00-96", BitConverter.ToString(_buffer, Offset, length));
        }
        [Test]
        public void HaveDecoding()
        {
            EncodeDecode(new HaveMessage(34622));
        }

        [Test]
        public void InterestedEncoding()
        {
            var length = new InterestedMessage().Encode(_buffer, Offset);
            Assert.AreEqual("00-00-00-01-02", BitConverter.ToString(_buffer, Offset, length));
        }

        [Test]
        public void InterestedDecoding()
        {
            EncodeDecode(new InterestedMessage());
        }

        [Test]
        public void KeepAliveEncoding()
        {
            new KeepAliveMessage().Encode(_buffer, Offset);
            Assert.IsTrue(_buffer[Offset] == 0
                            && _buffer[Offset + 1] == 0
                            && _buffer[Offset + 2] == 0
                            && _buffer[Offset + 3] == 0);
        }

        [Test]
        public void KeepAliveDecoding()
        {
        }

        [Test]
        public void NotInterestedEncoding()
        {
            var length = new NotInterestedMessage().Encode(_buffer, Offset);
            Assert.AreEqual("00-00-00-01-03", BitConverter.ToString(_buffer, Offset, length));
        }

        [Test]
        public void NotInterestedDecoding()
        {
            EncodeDecode(new NotInterestedMessage());
        }

        [Test]
        public void PieceEncoding()
        {
            var message = new PieceMessage(15, 10, Piece.BlockSize)
                              {
                                  Data = new byte[Piece.BlockSize]
                              };
            message.Encode(_buffer, Offset);
        }

        [Test]
        public void PieceDecoding()
        {
            var message = new PieceMessage(15, 10, Piece.BlockSize)
                                       {
                                           Data = new byte[Piece.BlockSize]
                                       };
            EncodeDecode(message);
        }

        [Test]
        public void PortEncoding()
        {
            var length = new PortMessage(2500).Encode(_buffer, Offset);
            Assert.AreEqual("00-00-00-03-09-09-C4", BitConverter.ToString(_buffer, Offset, length));
        }

        [Test]
        public void PortDecoding()
        {
            EncodeDecode(new PortMessage(5452));
        }

        [Test]
        public void RequestEncoding()
        {
            var length = new RequestMessage(5, 1024, 16384).Encode(_buffer, Offset);
            Assert.AreEqual("00-00-00-0D-06-00-00-00-05-00-00-04-00-00-00-40-00", BitConverter.ToString(_buffer, Offset, length));
        }

        [Test]
        public void RequestDecoding()
        {
            EncodeDecode(new RequestMessage(123, 789, 4235));
        }

        [Test]
        public void UnchokeEncoding()
        {
            var length = new UnchokeMessage().Encode(_buffer, Offset);
            Assert.AreEqual("00-00-00-01-01", BitConverter.ToString(_buffer, Offset, length));
        }
        [Test]
        public void UnchokeDecoding()
        {
            EncodeDecode(new UnchokeMessage());
        }

		[Test]
		public void PeerExchangeMessageTest ()
		{
			var data = new BEncodedDictionary ().Encode ();
			var message = new PeerExchangeMessage ();
			message.Decode (data, 0, data.Length);
			Assert.IsNotNull (message.Added, "#1");
			Assert.IsNotNull (message.AddedDotF, "#1");
			Assert.IsNotNull (message.Dropped, "#1");
		}

        private void EncodeDecode(IMessage orig)
        {
            orig.Encode(_buffer, Offset);
            Message dec = PeerMessage.DecodeMessage(_buffer, Offset, orig.ByteLength, _testRig.Manager);
            Assert.IsTrue(orig.Equals(dec), string.Format("orig: {0}, new: {1}", orig, dec));

            Assert.IsTrue(Toolbox.ByteMatch(orig.Encode(), PeerMessage.DecodeMessage(orig.Encode(), 0, orig.ByteLength, _testRig.Manager).Encode()));
        }
    }
}