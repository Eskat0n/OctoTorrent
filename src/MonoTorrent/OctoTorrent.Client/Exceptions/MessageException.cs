namespace OctoTorrent.Client
{
    using System;
    using System.Runtime.Serialization;
    using Common;

    public class MessageException : TorrentException
    {
        public MessageException()
        {
        }

        public MessageException(string message)
            : base(message)
        {
        }

        public MessageException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public MessageException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}