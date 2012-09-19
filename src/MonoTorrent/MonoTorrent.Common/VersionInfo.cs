//
// VersionInfo.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

namespace MonoTorrent.Common
{
    using System;
    using System.Linq;
    using System.Reflection;

    public static class VersionInfo
    {
        /// <summary>
        ///   Protocol string for version 1.0 of Bittorrent Protocol
        /// </summary>
        public static readonly string ProtocolStringV100 = "BitTorrent protocol";

        public static readonly string DhtClientVersion = "MO06";

        internal static Version Version;

        /// <summary>
        ///   The current version of the client
        /// </summary>
        public static string ClientVersion
        {
            get { return CreateClientVersion(); }
        }

        private static string CreateClientVersion()
        {
            var monotorrentAssembly = Assembly.GetExecutingAssembly();
            var versionAttribute = GetAssemblyAttribute<AssemblyInformationalVersionAttribute>(monotorrentAssembly);

            var version = new Version(versionAttribute.InformationalVersion);

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

        private static TAttribute GetAssemblyAttribute<TAttribute>(Assembly assembly) 
            where TAttribute : Attribute
        {
            return assembly.GetCustomAttributes(typeof (TAttribute), false)
                       .FirstOrDefault() as TAttribute;
        }
    }
}