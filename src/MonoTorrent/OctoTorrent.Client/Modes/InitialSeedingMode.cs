//
// InitialSeedingMode.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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
    using Common;
    using Messages;
    using Messages.FastPeer;
    using Messages.Standard;

    internal class InitialSeedingMode : Mode
    {
        private readonly InitialSeedUnchoker _unchoker;
        private readonly BitField _zero;

        public InitialSeedingMode(TorrentManager manager)
            : base(manager)
        {
            _unchoker = new InitialSeedUnchoker(manager);
            manager.chokeUnchoker = _unchoker;
            _zero = new BitField(manager.Bitfield.Length);
        }

        public override TorrentState State
        {
            get { return TorrentState.Seeding; }
        }

        protected override void AppendBitfieldMessage(PeerId id, MessageBundle bundle)
        {
            if (id.SupportsFastPeer)
                bundle.Messages.Add(new HaveNoneMessage());
            else
                bundle.Messages.Add(new BitfieldMessage(_zero));
        }

        protected override void HandleHaveMessage(PeerId id, HaveMessage message)
        {
            base.HandleHaveMessage(id, message);
            _unchoker.ReceivedHave(id, message.PieceIndex);
        }

        protected override void HandleRequestMessage(PeerId id, RequestMessage message)
        {
            base.HandleRequestMessage(id, message);
            _unchoker.SentBlock(id, message.PieceIndex);
        }

        protected override void HandleNotInterested(PeerId id, NotInterestedMessage message)
        {
            base.HandleNotInterested(id, message);
            _unchoker.ReceivedNotInterested(id);
        }

        public override void HandlePeerConnected(PeerId id, Direction direction)
        {
            base.HandlePeerConnected(id, direction);
            _unchoker.PeerConnected(id);
        }

        public override void HandlePeerDisconnected(PeerId id)
        {
            _unchoker.PeerDisconnected(id);
            base.HandlePeerDisconnected(id);
        }

        public override void Tick(int counter)
        {
            base.Tick(counter);
            if (!_unchoker.Complete) 
                return;

            PeerMessage bitfieldMessage = new BitfieldMessage(Manager.Bitfield);
            PeerMessage haveAllMessage = new HaveAllMessage();
            foreach (var peer in Manager.Peers.ConnectedPeers)
            {
                var message = peer.SupportsFastPeer && Manager.Complete ? haveAllMessage : bitfieldMessage;
                peer.Enqueue(message);
            }
            Manager.Mode = new DownloadMode(Manager);
        }
    }
}