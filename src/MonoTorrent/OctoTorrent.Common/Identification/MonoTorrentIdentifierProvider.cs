namespace OctoTorrent.Common
{
    using System;
    using System.Linq;
    using System.Reflection;

    public sealed class MonoTorrentIdentifierProvider : AzureusStyleIdentifierProvider
    {
        public MonoTorrentIdentifierProvider()
            : base("MO")
        {
        }

        public override string CreateHumanReadableId()
        {
            return string.Format("MonoTorrent {0}", GetVersion());
        }

        public override string CreateDhtClientVersion()
        {
            return "MO06";
        }

        protected override Version GetVersion()
        {
            var monotorrentAssembly = Assembly.GetExecutingAssembly();
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