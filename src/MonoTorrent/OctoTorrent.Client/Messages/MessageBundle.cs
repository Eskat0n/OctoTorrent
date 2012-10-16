namespace OctoTorrent.Client.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class MessageBundle : PeerMessage
    {
        private readonly List<PeerMessage> _messages;

        public MessageBundle()
        {
            _messages = new List<PeerMessage>();
        }

        public MessageBundle(PeerMessage message)
            : this()
        {
            _messages.Add(message);
        }

        public List<PeerMessage> Messages
        {
            get { return _messages; }
        }

        public override int ByteLength
        {
            get
            {
                return _messages.Sum(pm => pm.ByteLength);
            }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            throw new InvalidOperationException();
        }

        public override int Encode(byte[] buffer, int offset)
        {
            var written = _messages.Aggregate(offset, (current, t) => current + t.Encode(buffer, current));

            return CheckWritten(written - offset);
        }
    }
}