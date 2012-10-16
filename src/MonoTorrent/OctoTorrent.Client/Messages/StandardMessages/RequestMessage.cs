namespace MonoTorrent.Client.Messages.Standard
{
    using System.Text;

    public class RequestMessage : PeerMessage
    {
        private const int MessageLength = 13;

        internal const int MaxSize = 65536 + 64;
        internal const int MinSize = 4096;
        internal const byte MessageId = 6;

        #region Public Properties

        public override int ByteLength
        {
            get { return MessageLength + 4; }
        }

        public int StartOffset { get; private set; }

        public int PieceIndex { get; private set; }

        public int RequestLength { get; private set; }

        #endregion

        #region Constructors

        public RequestMessage()
        {
        }

        public RequestMessage(int pieceIndex, int startOffset, int requestLength)
        {
            PieceIndex = pieceIndex;
            StartOffset = startOffset;
            RequestLength = requestLength;
        }

        #endregion

        #region Methods

        public override void Decode(byte[] buffer, int offset, int length)
        {
            PieceIndex = ReadInt(buffer, ref offset);
            StartOffset = ReadInt(buffer, ref offset);
            RequestLength = ReadInt(buffer, ref offset);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;

            written += Write(buffer, written, MessageLength);
            written += Write(buffer, written, MessageId);
            written += Write(buffer, written, PieceIndex);
            written += Write(buffer, written, StartOffset);
            written += Write(buffer, written, RequestLength);

            return CheckWritten(written - offset);
        }

        public override bool Equals(object obj)
        {
            var requestMessage = obj as RequestMessage;
            return requestMessage != null &&
                   PieceIndex == requestMessage.PieceIndex &&
                   StartOffset == requestMessage.StartOffset &&
                   RequestLength == requestMessage.RequestLength;
        }

        public override int GetHashCode()
        {
            return PieceIndex.GetHashCode() ^ RequestLength.GetHashCode() ^ StartOffset.GetHashCode();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append("RequestMessage ");
            sb.Append(" Index ");
            sb.Append(PieceIndex);
            sb.Append(" Offset ");
            sb.Append(StartOffset);
            sb.Append(" Length ");
            sb.Append(RequestLength);

            return sb.ToString();
        }

        #endregion
    }
}