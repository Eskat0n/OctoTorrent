//
// BitfieldMessage.cs
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
    using Common;

    /// <summary>
    /// </summary>
    public class BitfieldMessage : PeerMessage
    {
        internal const byte MessageId = 5;

        #region Member Variables

        private readonly BitField _bitField;

        /// <summary>
        ///   The bitfield
        /// </summary>
        public BitField BitField
        {
            get { return _bitField; }
        }

        #endregion

        #region Constructors

        /// <summary>
        ///   Creates a new BitfieldMessage
        /// </summary>
        /// <param name="length"> The length of the bitfield </param>
        public BitfieldMessage(int length)
        {
            _bitField = new BitField(length);
        }


        /// <summary>
        ///   Creates a new BitfieldMessage
        /// </summary>
        /// <param name="bitfield"> The bitfield to use </param>
        public BitfieldMessage(BitField bitfield)
        {
            _bitField = bitfield;
        }

        #endregion

        #region Methods

        /// <summary>
        ///   Returns the length of the message in bytes
        /// </summary>
        public override int ByteLength
        {
            get { return (_bitField.LengthInBytes + 5); }
        }

        public override void Decode(byte[] buffer, int offset, int length)
        {
            _bitField.FromArray(buffer, offset, length);
        }

        public override int Encode(byte[] buffer, int offset)
        {
            var written = offset;

            written += Write(buffer, written, _bitField.LengthInBytes + 1);
            written += Write(buffer, written, MessageId);
            _bitField.ToByteArray(buffer, written);
            written += _bitField.LengthInBytes;

            return CheckWritten(written - offset);
        }

        #endregion

        #region Overridden Methods

        /// <summary>
        /// </summary>
        /// <returns> </returns>
        public override string ToString()
        {
            return "BitfieldMessage";
        }

        public override bool Equals(object obj)
        {
            var bitfieldMessage = obj as BitfieldMessage;
            return bitfieldMessage != null && _bitField.Equals(bitfieldMessage._bitField);
        }

        public override int GetHashCode()
        {
            return _bitField.GetHashCode();
        }

        #endregion
    }
}