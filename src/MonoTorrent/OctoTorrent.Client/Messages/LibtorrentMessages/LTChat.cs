namespace MonoTorrent.Client.Messages.Libtorrent
{
    using BEncoding;

    public class LTChat : ExtensionMessage
    {
        public static readonly ExtensionSupport Support = CreateSupport("LT_chat");

        private static readonly BEncodedString MessageKey = "msg";
        private BEncodedDictionary _messageDict = new BEncodedDictionary();

        public LTChat()
            : base(Support.MessageId)
        {
        }

        internal LTChat(byte messageId, string message)
            : this()
        {
            ExtensionId = messageId;
            Message = message;
        }

        public LTChat(PeerId peer, string message)
            : this()
        {
            ExtensionId = peer.ExtensionSupports.MessageId(Support);
            Message = message;
        }

        public string Message
        {
            set { _messageDict[MessageKey] = (BEncodedString) (value ?? ""); }
            get { return ((BEncodedString) _messageDict[MessageKey]).Text; }
        }

        public override int ByteLength
        {
            get { return 4 + 1 + 1 + _messageDict.LengthInBytes(); }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            _messageDict = BEncodedValue.Decode<BEncodedDictionary>(buffer, offset, length, false);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            if (!ClientEngine.SupportsFastPeer)
                throw new MessageException("Libtorrent extension messages not supported");

            var written = offset;

            written += Write(buffer, offset, ByteLength - 4);
            written += Write(buffer, written, MessageId);
            written += Write(buffer, written, ExtensionId);
            written += _messageDict.Encode(buffer, written);

            CheckWritten(written - offset);
            return written - offset;
            ;
        }
    }
}