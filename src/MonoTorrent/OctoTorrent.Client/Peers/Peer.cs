//
// Peer.cs
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

namespace OctoTorrent.Client
{
    using System;
    using System.Text;
    using System.Net;
    using System.Collections.Generic;
    using Common;
    using BEncoding;
    using Encryption;

    public class Peer
    {
        #region Private Fields

        private int _cleanedUpCount;
        private readonly Uri _connectionUri;
        private EncryptionTypes _encryption;
        private int _failedConnectionAttempts;
        private int _localPort;
        private int _totalHashFails;
        private bool _isSeeder;
        private string _peerId;
        private int _repeatedHashFails;
        private DateTime _lastConnectionAttempt;

        #endregion Private Fields

        #region Properties

        public Uri ConnectionUri
        {
            get { return _connectionUri; }
        }

        internal int CleanedUpCount
        {
            get { return _cleanedUpCount; }
            set { _cleanedUpCount = value; }
        }

        public EncryptionTypes Encryption
        {
            get { return _encryption; }
            set { _encryption = value; }
        }

        internal int TotalHashFails
        {
            get { return this._totalHashFails; }
        }

        internal string PeerId
        {
            get { return _peerId; }
            set { _peerId = value; }
        }

        internal bool IsSeeder
        {
            get { return this._isSeeder; }
            set { this._isSeeder = value; }
        }

        internal int FailedConnectionAttempts
        {
            get { return this._failedConnectionAttempts; }
            set { this._failedConnectionAttempts = value; }
        }

        internal int LocalPort
        {
            get { return _localPort; }
            set { _localPort = value; }
        }

        internal DateTime LastConnectionAttempt
        {
            get { return this._lastConnectionAttempt; }
            set { this._lastConnectionAttempt = value; }
        }

        internal int RepeatedHashFails
        {
            get { return _repeatedHashFails; }
        }

        #endregion Properties

        #region Constructors

        public Peer(string peerId, Uri connectionUri)
            : this (peerId, connectionUri, EncryptionTypes.All)
        {
        }

        public Peer(string peerId, Uri connectionUri, EncryptionTypes encryption)
        {
            if (peerId == null)
                throw new ArgumentNullException("peerId");
            if (connectionUri == null)
                throw new ArgumentNullException("connectionUri");

            this._connectionUri = connectionUri;
            this._encryption = encryption;
            this._peerId = peerId;
        }

        #endregion

        public override bool Equals(object obj)
        {
            return Equals(obj as Peer);
        }

        public bool Equals(Peer other)
        {
            if (other == null)
                return false;

            // FIXME: Don't compare the port, just compare the IP
            if (string.IsNullOrEmpty(_peerId) && string.IsNullOrEmpty(other._peerId))
                return _connectionUri.Host.Equals(other._connectionUri.Host);

            return _peerId == other._peerId;
        }

        public override int GetHashCode()
        {
            return _connectionUri.Host.GetHashCode();
        }

        public override string ToString()
        {
            return _connectionUri.ToString();
        }

        internal byte[] CompactPeer()
        {
            var data = new byte[6];
            CompactPeer(data, 0);
            return data;
        }

        internal void CompactPeer(byte[] data, int offset)
        {
            Buffer.BlockCopy(IPAddress.Parse(this._connectionUri.Host).GetAddressBytes(), 0, data, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(((short)this._connectionUri.Port))), 0, data, offset + 4, 2);
        }

        internal void HashedPiece(bool succeeded)
        {
            if (succeeded && _repeatedHashFails > 0)
                _repeatedHashFails--;

            if (succeeded) 
                return;

            _repeatedHashFails++;
            _totalHashFails++;
        }

        public static MonoTorrentCollection<Peer> Decode(BEncodedList peers)
        {
            var list = new MonoTorrentCollection<Peer>(peers.Count);
            foreach (var value in peers)
            {
                try
                {
                    if (value is BEncodedDictionary)
                        list.Add(DecodeFromDict((BEncodedDictionary)value));
                    else if (value is BEncodedString)
                        list.AddRange(Decode((BEncodedString) value));
                }
                catch
                {
                    // If something is invalid and throws an exception, ignore it
                    // and continue decoding the rest of the peers
                }
            }
            return list;
        }

        private static Peer DecodeFromDict(IDictionary<BEncodedString, BEncodedValue> dict)
        {
            string peerId;

            if (dict.ContainsKey("peer id"))
                peerId = dict["peer id"].ToString();
            else if (dict.ContainsKey("peer_id"))       // HACK: Some trackers return "peer_id" instead of "peer id"
                peerId = dict["peer_id"].ToString();
            else
                peerId = string.Empty;

            var connectionUri = new Uri(string.Format("tcp://{0}:{1}", dict["ip"], dict["port"]));
            return new Peer(peerId, connectionUri, EncryptionTypes.All);
        }

        public static MonoTorrentCollection<Peer> Decode(BEncodedString peers)
        {
            // "Compact Response" peers are encoded in network byte order. 
            // IP's are the first four bytes
            // Ports are the following 2 bytes
            var byteOrderedData = peers.TextBytes;
            var i = 0;
            var sb = new StringBuilder(27);
            var list = new MonoTorrentCollection<Peer>((byteOrderedData.Length / 6) + 1);
            while ((i + 5) < byteOrderedData.Length)
            {
                sb.Remove(0, sb.Length);

                sb.Append("tcp://");
                sb.Append(byteOrderedData[i++]);
                sb.Append('.');
                sb.Append(byteOrderedData[i++]);
                sb.Append('.');
                sb.Append(byteOrderedData[i++]);
                sb.Append('.');
                sb.Append(byteOrderedData[i++]);

                var port = (UInt16)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(byteOrderedData, i));
                i += 2;
                sb.Append(':');
                sb.Append(port);

                var uri = new Uri(sb.ToString());
                list.Add(new Peer("", uri, EncryptionTypes.All));
            }

            return list;
        }

        internal static BEncodedList Encode(IEnumerable<Peer> peers)
        {
            var list = new BEncodedList();
            foreach (var p in peers)
                list.Add((BEncodedString)p.CompactPeer());
            return list;
        }
    }
}