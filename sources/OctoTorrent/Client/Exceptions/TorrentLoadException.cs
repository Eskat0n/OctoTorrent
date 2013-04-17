namespace OctoTorrent.Client
{
    using System;
    using System.Runtime.Serialization;
    using Common;

    public class TorrentLoadException : TorrentException
    {
        public TorrentLoadException()
        {
        }

        public TorrentLoadException(string message)
            : base(message)
        {
        }

        public TorrentLoadException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public TorrentLoadException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}