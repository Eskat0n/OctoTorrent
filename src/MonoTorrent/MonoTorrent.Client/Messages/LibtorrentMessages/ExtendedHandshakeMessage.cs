//
// ExtendedHandshakeMessage.cs
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

namespace MonoTorrent.Client.Messages.Libtorrent
{
    using System.Linq;
    using BEncoding;

    public class ExtendedHandshakeMessage : ExtensionMessage
    {
        private static readonly BEncodedString MaxRequestKey = "reqq";
        private static readonly BEncodedString PortKey = "p";
        private static readonly BEncodedString SupportsKey = "m";
        private static readonly BEncodedString VersionKey = "v";
        private static readonly BEncodedString MetadataSizeKey = "metadata_size";

        private static readonly ExtensionSupport Support = new ExtensionSupport("LT_handshake", 0);

        private int _localPort;
        private int _maxRequests;
        private int _metadataSize;
        private ExtensionSupports _supports;
        private string _version;

        public override int ByteLength
        {
            get
            {
                // TODO: Implement this properly

                // The length of the payload, 4 byte length prefix, 1 byte BT message id, 1 byte LT message id
                return Create().LengthInBytes() + 4 + 1 + 1;
            }
        }

        public int MaxRequests
        {
            get { return _maxRequests; }
        }

        public int LocalPort
        {
            get { return _localPort; }
        }

        public ExtensionSupports Supports
        {
            get { return _supports; }
        }

        public string Version
        {
            get { return _version ?? ""; }
        }

        public int MetadataSize
        {
            get { return _metadataSize; }
        }

        #region Constructors

        public ExtendedHandshakeMessage()
            : base(Support.MessageId)
        {
            _supports = new ExtensionSupports(SupportedMessages);
        }

        public ExtendedHandshakeMessage(int metadataSize)
            : this()
        {
            this._metadataSize = metadataSize;
        }

        #endregion

        #region Methods

        public override void Decode(byte[] buffer, int offset, int length)
        {
            BEncodedValue val;
            var d = BEncodedValue.Decode<BEncodedDictionary>(buffer, offset, length, false);

            if (d.TryGetValue(MaxRequestKey, out val))
                _maxRequests = (int) ((BEncodedNumber) val).Number;
            if (d.TryGetValue(VersionKey, out val))
                _version = ((BEncodedString) val).Text;
            if (d.TryGetValue(PortKey, out val))
                _localPort = (int) ((BEncodedNumber) val).Number;

            LoadSupports((BEncodedDictionary) d[SupportsKey]);

            if (d.TryGetValue(MetadataSizeKey, out val))
                _metadataSize = (int) ((BEncodedNumber) val).Number;
        }

        private void LoadSupports(BEncodedDictionary supports)
        {
            var extensionSupports = supports
                .Select(k => new ExtensionSupport(k.Key.Text, (byte) ((BEncodedNumber) k.Value).Number));

            _supports = new ExtensionSupports(extensionSupports);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;
            var dict = Create();

            written += Write(buffer, written, dict.LengthInBytes() + 1 + 1);
            written += Write(buffer, written, MessageId);
            written += Write(buffer, written, Support.MessageId);
            written += dict.Encode(buffer, written);

            CheckWritten(written - offset);
            return written - offset;
        }

        private BEncodedDictionary Create()
        {
            if (!ClientEngine.SupportsExtended)
                throw new MessageException("Libtorrent extension messages not supported");

            var mainDict = new BEncodedDictionary();
            var supportsDict = new BEncodedDictionary();

            mainDict.Add(MaxRequestKey, (BEncodedNumber) _maxRequests);
            mainDict.Add(VersionKey, (BEncodedString) Version);
            mainDict.Add(PortKey, (BEncodedNumber) _localPort);

            SupportedMessages.ForEach(s => supportsDict.Add(s.Name, (BEncodedNumber) s.MessageId));
            mainDict.Add(SupportsKey, supportsDict);

            mainDict.Add(MetadataSizeKey, (BEncodedNumber) _metadataSize);

            return mainDict;
        }

        #endregion
    }
}