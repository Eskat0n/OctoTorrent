//
// PeerConnectionEventArgs.cs
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
    using Common;

    /// <summary>
    /// Provides the data needed to handle a PeerConnection event
    /// </summary>
    public class PeerConnectionEventArgs : TorrentEventArgs
    {
        private readonly PeerId _peerConnectionId;
        private readonly Direction _connectionDirection;
        private readonly String _message;

        #region Member Variables

        public PeerId PeerID
        {
            get { return _peerConnectionId; }
        }

        /// <summary>
        /// The peer event that just happened
        /// </summary>
        public Direction ConnectionDirection
        {
            get { return _connectionDirection; }
        }

        /// <summary>
        /// Any message that might be associated with this event
        /// </summary>
        public String Message
        {
            get { return _message; }
        }

        #endregion

        #region Constructors

        internal PeerConnectionEventArgs( TorrentManager manager, PeerId id, Direction direction )
            : this(manager, id, direction, string.Empty)
        {
        }

        internal PeerConnectionEventArgs( TorrentManager manager, PeerId id, Direction direction, String message )
            : base( manager )
        {
            _peerConnectionId = id;
            _connectionDirection = direction;
            _message = message;
        }

        #endregion
    }
}