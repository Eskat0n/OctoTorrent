namespace MonoTorrent.Common
{
    using System;
    using System.Linq;
    using System.Reflection;

    public sealed class MonoTorrentIdentifierProvider : IIdentifierProvider
    {
        public string CreatePeerId()
        {
            var version = GetAssemblyVersion();

            // 'MO' for MonoTorrent then four digit version number
            var versionString = string.Format("{0}{1}{2}{3}",
                                              Math.Max(version.Major, 0),
                                              Math.Max(version.Minor, 0),
                                              Math.Max(version.Build, 0),
                                              Math.Max(version.Revision, 0));
            versionString = versionString.Length > 4
                                ? versionString.Substring(0, 4)
                                : versionString.PadRight(4, '0');

            return string.Format("-MO{0}-", versionString);
        }

        public string CreateHumanReadableId()
        {
            return string.Format("MonoTorrent {0}", GetAssemblyVersion());
        }

        public string CreateDhtClientVersion()
        {
            return "MO06";
        }

        private static Version GetAssemblyVersion(Assembly assembly = null)
        {
            var monotorrentAssembly = assembly ?? Assembly.GetExecutingAssembly();
            var versionAttribute = GetAssemblyAttribute<AssemblyInformationalVersionAttribute>(monotorrentAssembly);

            return new Version(versionAttribute.InformationalVersion);
        }

        private static TAttribute GetAssemblyAttribute<TAttribute>(Assembly assembly)
            where TAttribute : Attribute
        {
            return assembly.GetCustomAttributes(typeof(TAttribute), false)
                       .FirstOrDefault() as TAttribute;
        }
    }
}