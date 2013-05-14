namespace OctoTorrent.Client.Encryption
{
    using System;
    using Common;

    public class EncryptionException : TorrentException
    {
        public EncryptionException()
        {
        }

        public EncryptionException(string message)
            : base(message)
        {
        }

        public EncryptionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}