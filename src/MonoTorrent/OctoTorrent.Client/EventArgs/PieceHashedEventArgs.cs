//
// PieceHashedEventArgs.cs
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

namespace MonoTorrent.Client
{
    /// <summary>
    /// Provides the data needed to handle a PieceHashed event
    /// </summary>
    public class PieceHashedEventArgs : TorrentEventArgs
    {
        private readonly int _pieceIndex;
        private readonly bool _hashPassed;

        #region Member Variables

        /// <summary>
        /// The index of the piece that was just hashed
        /// </summary>
        public int PieceIndex
        {
            get { return _pieceIndex; }
        }        

        /// <summary>
        /// The value of whether the piece passed or failed the hash check
        /// </summary>
        public bool HashPassed
        {
            get { return _hashPassed; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new PieceHashedEventArgs
        /// </summary>
        public PieceHashedEventArgs(TorrentManager manager, int pieceIndex, bool hashPassed)
            : base(manager)
        {
            _pieceIndex = pieceIndex;
            _hashPassed = hashPassed;
        }

        #endregion
    }
}
