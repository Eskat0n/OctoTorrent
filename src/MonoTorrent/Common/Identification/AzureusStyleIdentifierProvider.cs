namespace OctoTorrent.Common
{
    using System;

    public abstract class AzureusStyleIdentifierProvider : IIdentifierProvider
    {
        private readonly string _clientId;

        protected AzureusStyleIdentifierProvider(string clientId)
        {
            _clientId = clientId;
        }

        public virtual string CreatePeerId()
        {
            return string.Format("-{0}{1}-", _clientId, CreateVersionString());
        }

        public abstract string CreateHumanReadableId();

        public abstract string CreateDhtClientVersion();

        protected virtual string CreateVersionString()
        {
            var version = GetVersion();

            // 'MO' for MonoTorrent then four digit version number
            var versionString = string.Format("{0}{1}{2}{3}",
                                              Math.Max(version.Major, 0),
                                              Math.Max(version.Minor, 0),
                                              Math.Max(version.Build, 0),
                                              Math.Max(version.Revision, 0));
            versionString = versionString.Length > 4
                                ? versionString.Substring(0, 4)
                                : versionString.PadRight(4, '0');
            return versionString;
        }

        protected abstract Version GetVersion();
    }
}