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
    public static class VersionInfo
    {
        /// <summary>
        ///   Protocol string for version 1.0 of Bittorrent Protocol
        /// </summary>
        public const string ProtocolStringV100 = "BitTorrent protocol";

        private static readonly IIdentifierProvider DefaultIdentifierProvider = new MonoTorrentIdentifierProvider();
        private static IIdentifierProvider _customIdentifierProvider;

        public static IIdentifierProvider IdentifierProvider
        {
            get { return _customIdentifierProvider ?? DefaultIdentifierProvider; }
            set { _customIdentifierProvider = value; }
        }

        public static string DhtClientVersion
        {
            get { return IdentifierProvider.CreateDhtClientVersion(); }
        }

        /// <summary>
        ///   The current version of the client
        /// </summary>
        public static string ClientVersion
        {
            get { return IdentifierProvider.CreatePeerId(); }
        }

        internal static string HumanReadableId
        {
            get { return IdentifierProvider.CreateHumanReadableId(); }
        }
    }
}