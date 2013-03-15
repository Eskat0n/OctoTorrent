namespace OctoTorrent.Client.Messages
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using FastPeer;
    using Libtorrent;
    using Standard;

    public delegate PeerMessage CreateMessage(TorrentManager manager);

    public abstract class PeerMessage : Message
    {
        private static readonly Dictionary<byte, CreateMessage> MessageDict;

        static PeerMessage()
        {
            MessageDict = new Dictionary<byte, CreateMessage>();

            // Note - KeepAlive messages aren't registered as they have no payload or ID and are never 'decoded'
            //      - Handshake messages aren't registered as they are always the first message sent/received
            Register(AllowedFastMessage.MessageId, manager => new AllowedFastMessage());
            Register(BitfieldMessage.MessageId, manager => new BitfieldMessage(manager.Torrent.Pieces.Count));
            Register(CancelMessage.MessageId, manager => new CancelMessage());
            Register(ChokeMessage.MessageId, manager => new ChokeMessage());
            Register(HaveAllMessage.MessageId, manager => new HaveAllMessage());
            Register(HaveMessage.MessageId, manager => new HaveMessage());
            Register(HaveNoneMessage.MessageId, manager => new HaveNoneMessage());
            Register(InterestedMessage.MessageId, manager => new InterestedMessage());
            Register(NotInterestedMessage.MessageId, manager => new NotInterestedMessage());
            Register(PieceMessage.MessageId, manager => new PieceMessage());
            Register(PortMessage.MessageId, manager => new PortMessage());
            Register(RejectRequestMessage.MessageId, manager => new RejectRequestMessage());
            Register(RequestMessage.MessageId, manager => new RequestMessage());
            Register(SuggestPieceMessage.MessageId, manager => new SuggestPieceMessage());
            Register(UnchokeMessage.MessageId, manager => new UnchokeMessage());

            // We register this solely so that the user cannot register their own message with this ID.
            // Actual decoding is handled with manual detection
            Register(ExtensionMessage.MessageId, manager => { throw new MessageException("Shouldn't decode extension message this way"); });
        }

        private static void Register(byte identifier, CreateMessage creator)
        {
            if (creator == null)
                throw new ArgumentNullException("creator");

            lock (MessageDict)
                MessageDict.Add(identifier, creator);
        }

        public static PeerMessage DecodeMessage(byte[] buffer, int offset, int count, TorrentManager manager)
        {
            CreateMessage creator;

            if (count < 4)
                throw new ArgumentException("A message must contain a 4 byte length prefix");

            var messageLength = IPAddress.HostToNetworkOrder(BitConverter.ToInt32(buffer, offset));

            if (messageLength > (count - 4))
                throw new ArgumentException("Incomplete message detected");

            if (buffer[offset + 4] == ExtensionMessage.MessageId)
                return ExtensionMessage.DecodeMessage(buffer, offset + 4 + 1, count - 4 - 1, manager);

            if (!MessageDict.TryGetValue(buffer[offset + 4], out creator))
                throw new ProtocolException("Unknown message received");

            // The message length is given in the second byte and the message body follows directly after that
            // We decode up to the number of bytes Received. If the message isn't complete, throw an exception
            var message = creator(manager);
            message.Decode(buffer, offset + 4 + 1, count - 4 - 1);
            return message;
        }

        internal void Handle(PeerId id)
        {
            id.TorrentManager.Mode.HandleMessage(id, this);
        }
    }
}