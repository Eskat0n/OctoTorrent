namespace MonoTorrent.Client.Messages.Libtorrent
{
    using System;
    using BEncoding;

    public class PeerExchangeMessage : ExtensionMessage
    {
        public static readonly ExtensionSupport Support = CreateSupport("ut_pex");

        private static readonly BEncodedString AddedKey = "added";
        private static readonly BEncodedString AddedDotFKey = "added.f";
        private static readonly BEncodedString DroppedKey = "dropped";
        private BEncodedDictionary _peerDict;

        public PeerExchangeMessage()
            : base(Support.MessageId)
        {
            _peerDict = new BEncodedDictionary();
        }

        internal PeerExchangeMessage(byte messageId, byte[] added, byte[] addedDotF, byte[] dropped)
            : this()
        {
            ExtensionId = messageId;
            Initialise(added, addedDotF, dropped);
        }

        public PeerExchangeMessage(PeerId id, byte[] added, byte[] addedDotF, byte[] dropped)
            : this()
        {
            ExtensionId = id.ExtensionSupports.MessageId(Support);
            Initialise(added, addedDotF, dropped);
        }

        public byte[] Added
        {
            set { _peerDict[AddedKey] = (BEncodedString) (value ?? BufferManager.EmptyBuffer); }
            get { return ((BEncodedString) _peerDict[AddedKey]).TextBytes; }
        }

        public byte[] AddedDotF
        {
            set { _peerDict[AddedDotFKey] = (BEncodedString) (value ?? BufferManager.EmptyBuffer); }
            get { return ((BEncodedString) _peerDict[AddedDotFKey]).TextBytes; }
        }

        public byte[] Dropped
        {
            set { _peerDict[DroppedKey] = (BEncodedString) (value ?? BufferManager.EmptyBuffer); }
            get { return ((BEncodedString) _peerDict[DroppedKey]).TextBytes; }
        }

        public override int ByteLength
        {
            get { return 4 + 1 + 1 + _peerDict.LengthInBytes(); }
        }

        private void Initialise(byte[] added, byte[] addedDotF, byte[] dropped)
        {
            _peerDict[AddedKey] = (BEncodedString) (added ?? BufferManager.EmptyBuffer);
            _peerDict[AddedDotFKey] = (BEncodedString) (addedDotF ?? BufferManager.EmptyBuffer);
            _peerDict[DroppedKey] = (BEncodedString) (dropped ?? BufferManager.EmptyBuffer);
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            _peerDict = BEncodedValue.Decode<BEncodedDictionary>(buffer, offset, length, false);
            if (!_peerDict.ContainsKey(AddedKey))
                _peerDict.Add(AddedKey, (BEncodedString) "");
            if (!_peerDict.ContainsKey(AddedDotFKey))
                _peerDict.Add(AddedDotFKey, (BEncodedString) "");
            if (!_peerDict.ContainsKey(DroppedKey))
                _peerDict.Add(DroppedKey, (BEncodedString) "");
        }

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;

            written += Write(buffer, offset, ByteLength - 4);
            written += Write(buffer, written, MessageId);
            written += Write(buffer, written, ExtensionId);
            written += _peerDict.Encode(buffer, written);

            return CheckWritten(written - offset);
        }

        public override string ToString()
        {
            var added = (BEncodedString) _peerDict[AddedKey];
            var numPeers = added.TextBytes.Length/6;

            return String.Format("PeerExchangeMessage: {0} peers", numPeers);
        }
    }
}