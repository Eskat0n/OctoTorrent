#if !DISABLE_DHT
namespace OctoTorrent.Tests.Dht
{
    using System.Text;
    using NUnit.Framework;
    using OctoTorrent.Dht;
    using OctoTorrent.Dht.Messages;
    using BEncoding;
    using OctoTorrent.Client.Messages;
    using Message = OctoTorrent.Dht.Messages.Message;

    [TestFixture]
    public class MessageTests
    {
        private readonly NodeId _id = new NodeId(Encoding.UTF8.GetBytes("abcdefghij0123456789"));
        private readonly NodeId _infohash = new NodeId(Encoding.UTF8.GetBytes("mnopqrstuvwxyz123456"));
        private readonly BEncodedString _token = "aoeusnth";
        private readonly BEncodedString _transactionId = "aa";

        private QueryMessage _message;

        [SetUp]
        public void Setup()
        {
            Message.UseVersionKey = false;
        }

        [TearDown]
        public void Teardown()
        {
            Message.UseVersionKey = true;
        }
        
        #region Encode Tests

        [Test]
        public void AnnouncePeerEncode()
        {
//            var n = new Node(NodeId.Create(), null) {Token = _token};
            var message = new AnnouncePeer(_id, _infohash, 6881, _token) {TransactionId = _transactionId};

            Compare(message, "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz1234564:porti6881e5:token8:aoeusnthe1:q13:announce_peer1:t2:aa1:y1:qe");
        }

        [Test]
        public void AnnouncePeerResponseEncode()
        {
            var message = new AnnouncePeerResponse(_infohash, _transactionId);

            Compare(message, "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re");
        }

        [Test]
        public void FindNodeEncode()
        {
            var message = new FindNode(_id, _infohash) {TransactionId = _transactionId};

            Compare(message, "d1:ad2:id20:abcdefghij01234567896:target20:mnopqrstuvwxyz123456e1:q9:find_node1:t2:aa1:y1:qe");
            _message = message;
        }

        [Test]
        public void FindNodeResponseEncode()
        {
            var m = new FindNodeResponse(_id, _transactionId) {Nodes = "def456..."};

            Compare(m, "d1:rd2:id20:abcdefghij01234567895:nodes9:def456...e1:t2:aa1:y1:re");
        }

        [Test]
        public void GetPeersEncode()
        {
            var m = new GetPeers(_id, _infohash) {TransactionId = _transactionId};

            Compare(m, "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz123456e1:q9:get_peers1:t2:aa1:y1:qe");
            _message = m;
        }

        [Test]
        public void GetPeersResponseEncode()
        {
            var m = new GetPeersResponse(_id, _transactionId, _token)
                        {
                            Values = new BEncodedList
                                         {
                                             (BEncodedString) "axje.u", 
                                             (BEncodedString) "idhtnm"
                                         }
                        };
            Compare(m, "d1:rd2:id20:abcdefghij01234567895:token8:aoeusnth6:valuesl6:axje.u6:idhtnmee1:t2:aa1:y1:re");
        }

        [Test]
        public void PingEncode()
        {
            var m = new Ping(_id) {TransactionId = _transactionId};

            Compare(m, "d1:ad2:id20:abcdefghij0123456789e1:q4:ping1:t2:aa1:y1:qe");
            _message = m;
        }

        [Test]
        public void PingResponseEncode()
        {
            var m = new PingResponse(_infohash, _transactionId);

            Compare(m, "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re");
        }


        #endregion

        #region Decode Tests

        [Test]
        public void AnnouncePeerDecode()
        {
            const string text = "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz1234564:porti6881e5:token8:aoeusnthe1:q13:announce_peer1:t2:aa1:y1:qe";

            var m = (AnnouncePeer)Decode("d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz1234564:porti6881e5:token8:aoeusnthe1:q13:announce_peer1:t2:aa1:y1:qe");
            Assert.AreEqual(m.TransactionId, _transactionId, "#1");
            Assert.AreEqual(m.MessageType, QueryMessage.QueryType, "#2");
            Assert.AreEqual(_id, m.Id, "#3");
            Assert.AreEqual(_infohash, m.InfoHash, "#3");
            Assert.AreEqual((BEncodedNumber)6881, m.Port, "#4");
            Assert.AreEqual(_token, m.Token, "#5");

            Compare(m, text);
            _message = m;
        }

        [Test]
        public void AnnouncePeerResponseDecode()
        {
            const string text = "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re";

            // Register the query as being sent so we can decode the response
            AnnouncePeerDecode();
            MessageFactory.RegisterSend(_message);

            var m = (AnnouncePeerResponse)Decode(text);
            Assert.AreEqual(_infohash, m.Id, "#1");

            Compare(m, text);
        }

        [Test]
        public void FindNodeDecode()
        {
            const string text = "d1:ad2:id20:abcdefghij01234567896:target20:mnopqrstuvwxyz123456e1:q9:find_node1:t2:aa1:y1:qe";

            var message = (FindNode)Decode(text);

            Assert.AreEqual(_id, message.Id, "#1");
            Assert.AreEqual(_infohash, message.Target, "#1");
            Compare(message, text);
        }

        [Test]
        public void FindNodeResponseDecode()
        {
            const string text = "d1:rd2:id20:abcdefghij01234567895:nodes9:def456...e1:t2:aa1:y1:re";

            FindNodeEncode();
            MessageFactory.RegisterSend(_message);
            FindNodeResponse m = (FindNodeResponse)Decode(text);

            Assert.AreEqual(_id, m.Id, "#1");
            Assert.AreEqual((BEncodedString)"def456...", m.Nodes, "#2");
            Assert.AreEqual(_transactionId, m.TransactionId, "#3");

            Compare(m, text);
        }

        [Test]
        public void GetPeersDecode()
        {
            const string text = "d1:ad2:id20:abcdefghij01234567899:info_hash20:mnopqrstuvwxyz123456e1:q9:get_peers1:t2:aa1:y1:qe";

            var message = (GetPeers)Decode(text);

            Assert.AreEqual(_infohash, message.InfoHash, "#1");
            Assert.AreEqual(_id, message.Id, "#2");
            Assert.AreEqual(_transactionId, message.TransactionId, "#3");

            Compare(message, text);
        }

        [Test]
        public void GetPeersResponseDecode()
        {
            const string text = "d1:rd2:id20:abcdefghij01234567895:token8:aoeusnth6:valuesl6:axje.u6:idhtnmee1:t2:aa1:y1:re";

            GetPeersEncode();
            MessageFactory.RegisterSend(_message);

            var m = (GetPeersResponse)Decode(text);

            Assert.AreEqual(_token, m.Token, "#1");
            Assert.AreEqual(_id, m.Id, "#2");

            var list = new BEncodedList
                           {
                               (BEncodedString) "axje.u",
                               (BEncodedString) "idhtnm"
                           };

            Assert.AreEqual(list, m.Values, "#3");
            Compare(m, text);
        }

        [Test]
        public void PingDecode()
        {
            const string text = "d1:ad2:id20:abcdefghij0123456789e1:q4:ping1:t2:aa1:y1:qe";

            var message = (Ping) Decode(text);

            Assert.AreEqual(_id, message.Id, "#1");
            Compare(message, text);
        }

        [Test]
        public void PingResponseDecode()
        {
            const string text = "d1:rd2:id20:mnopqrstuvwxyz123456e1:t2:aa1:y1:re";

            PingEncode();
            MessageFactory.RegisterSend(_message);

            var message = (PingResponse)Decode(text);

            Assert.AreEqual(_infohash, message.Id);
            Compare(message, text);
        }

        #endregion

        private static void Compare(IMessage message, string expected)
        {
            Assert.AreEqual(Encoding.UTF8.GetString(message.Encode()), expected);
        }

        private static Message Decode(string p)
        {
            var buffer = Encoding.UTF8.GetBytes(p);
            return MessageFactory.DecodeMessage(BEncodedValue.Decode<BEncodedDictionary>(buffer));
        }
    }
}
#endif