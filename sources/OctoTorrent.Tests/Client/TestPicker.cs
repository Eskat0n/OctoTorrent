//
// TestPicker.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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


namespace OctoTorrent.Tests.Client
{
    using System.Collections.Generic;
    using OctoTorrent.Client;
    using OctoTorrent.Client.Messages;
    using OctoTorrent.Common;

    internal class TestPicker : PiecePicker
    {
        private readonly List<BitField> _isInterestingBitfield = new List<BitField>();
        private readonly List<PeerId> _pickPieceId = new List<PeerId>();
        private readonly List<List<PeerId>> _pickPiecePeers = new List<List<PeerId>>();
        private readonly List<int> _pickPieceStartIndex = new List<int>();
        private readonly List<int> _pickPieceEndIndex = new List<int>();
        private readonly List<int> _pickPieceCount = new List<int>();

        public readonly List<int> PickedPieces = new List<int>();
        public readonly List<BitField> PickPieceBitfield = new List<BitField>();

        public bool ReturnNoPiece = true;

        public TestPicker()
            : base(null)
        {
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int count, int startIndex, int endIndex)
        {
            _pickPieceId.Add(id);
            var clone = new BitField(peerBitfield.Length);
            clone.Or(peerBitfield);
            PickPieceBitfield.Add(clone);
            _pickPiecePeers.Add(otherPeers);
            _pickPieceStartIndex.Add(startIndex);
            _pickPieceEndIndex.Add(endIndex);
            _pickPieceCount.Add(count);

            for (var i = startIndex; i < endIndex; i++)
            {
                if (PickedPieces.Contains(i))
                    continue;

                PickedPieces.Add(i);

                return ReturnNoPiece
                           ? null
                           : new MessageBundle();
            }

            return null;
        }

        public override void Initialise(BitField bitfield, TorrentFile[] files, IEnumerable<Piece> requests)
        {
        }

        public override bool IsInteresting(BitField bitfield)
        {
            _isInterestingBitfield.Add(bitfield);
            return !bitfield.AllFalse;
        }
    }
}
