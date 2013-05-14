#if !DISABLE_DHT

using OctoTorrent.BEncoding;

namespace OctoTorrent.Dht.Messages
{
    internal abstract class ResponseMessage : Message
    {
        private static readonly BEncodedString ReturnValuesKey = "r";
        private static readonly BEncodedString ResponseType = "r";
        private readonly QueryMessage _queryMessage;

        internal override NodeId Id
        {
            get { return new NodeId((BEncodedString)Parameters[IdKey]); }
        }
        public BEncodedDictionary Parameters
        {
            get { return (BEncodedDictionary)Properties[ReturnValuesKey]; }
        }

        public QueryMessage Query
        {
            get { return _queryMessage; }
        }

        protected ResponseMessage(NodeId id, BEncodedValue transactionId)
            : base(ResponseType)
        {
            Properties.Add(ReturnValuesKey, new BEncodedDictionary());
            Parameters.Add(IdKey, id.BencodedString());
            TransactionId = transactionId;
        }

        protected ResponseMessage(BEncodedDictionary d, QueryMessage m)
            : base(d)
        {
            _queryMessage = m;
        }
    }
}
#endif