//
// PortMessage.cs
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

namespace OctoTorrent.Client.Messages.Standard
{
    /// <summary>
    /// Represents a "Port" message
    /// </summary>
    public class PortMessage : PeerMessage
    {
        private const int MessageLength = 3;
        internal const byte MessageId = 9;

        #region Private Fields

        #endregion

        #region Public Properties

        public override int ByteLength
        {
            get { return (MessageLength + 4); }
        }

        public ushort Port { get; private set; }

        #endregion

        #region Constructors

        public PortMessage()
        {
        }

        public PortMessage(ushort port)
        {
            Port = port;
        }
        
        #endregion

        #region Methods

        public override void Decode(byte[] buffer, int offset, int length)
        {
            Port = (ushort) ReadShort(buffer, ref offset);
        }

        public override int Encode(byte[] buffer, int offset)
        {
			var written = offset;

			written += Write(buffer, written, MessageLength);
			written += Write(buffer, written, MessageId);
			written += Write(buffer, written, Port);

            return CheckWritten(written - offset);
        }

        public override bool Equals(object obj)
        {
            var portMessage = obj as PortMessage;
            return portMessage != null && Port == portMessage.Port;
        }

        public override int GetHashCode()
        {
            return Port.GetHashCode();
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("PortMessage ");
            sb.Append(" Port ");
            sb.Append(Port);
            return sb.ToString();
        }

        #endregion
    }
}