namespace OctoTorrent.Client.Messages.Libtorrent
{
    public struct ExtensionSupport
    {
        private readonly byte _messageId;
        private readonly string _name;

        public ExtensionSupport(string name, byte messageId)
        {
            _messageId = messageId;
            _name = name;
        }

        public byte MessageId
        {
            get { return _messageId; }
        }

        public string Name
        {
            get { return _name; }
        }

        public override string ToString()
        {
            return string.Format("{1}: {0}", _name, _messageId);
        }
    }
}