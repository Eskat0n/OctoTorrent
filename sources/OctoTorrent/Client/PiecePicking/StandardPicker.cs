//
// StandardPicker.cs
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

using System;
using System.Collections.Generic;
using OctoTorrent.Common;
using OctoTorrent.Client.Messages;
using OctoTorrent.Client.Messages.Standard;

namespace OctoTorrent.Client
{
    using System.Linq;

    public class StandardPicker : PiecePicker
    {
        struct BinaryIndexComparer : IComparer<Piece>
        {
            int index;
            public BinaryIndexComparer(int index)
            {
                this.index = index;
            }

            public int Compare(Piece x, Piece y)
            {
                if (x == null)
                    return index.CompareTo(y.Index);
                return x.Index.CompareTo(index);
            }
        }

        static readonly Predicate<Block> TimedOut = b => b.RequestTimedOut;

        protected readonly SortList<Piece> Requests;

        public StandardPicker()
            : base(null)
        {
            Requests = new SortList<Piece>();
        }

        public override void CancelRequest(PeerId peer, int piece, int startOffset, int length)
        {
            CancelWhere(b => b.StartOffset == startOffset &&
                             b.RequestLength == length &&
                             b.PieceIndex == piece &&
                             peer.Equals(b.RequestedOff));
        }

        public override void CancelRequests(PeerId peer)
        {
            CancelWhere(b => peer.Equals(b.RequestedOff));
        }

        public override void CancelTimedOutRequests()
        {
            CancelWhere(TimedOut);
        }

        void CancelWhere(Predicate<Block> predicate)
        {
            var cancelled = false;
            Requests.ForEach(delegate(Piece p) {
                for (int i = 0; i < p.Blocks.Length; i++) {
                    if (predicate(p.Blocks[i]) && !p.Blocks[i].Received) {
                        cancelled = true;
                        p.Blocks[i].CancelRequest();
                    }
                }
            });

            if (cancelled)
                Requests.RemoveAll(x => x.NoBlocksRequested);
        }

        public override int CurrentRequestCount()
        {
            return Requests.Sum(p => p.TotalRequested - p.TotalReceived);
        }

        public override List<Piece> ExportActiveRequests()
        {
            return new List<Piece>(Requests);
        }

        public override void Initialise(BitField bitfield, TorrentFile[] files, IEnumerable<Piece> requests)
        {
            Requests.Clear();
            foreach (var p in requests)
                Requests.Add(p);
        }

        public override bool IsInteresting(BitField bitfield)
        {
            return !bitfield.AllFalse;
        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int count, int startIndex, int endIndex)
        {
            RequestMessage message;
            MessageBundle bundle;
            // If there is already a request on this peer, try to request the next block. If the peer is choking us, then the only
            // requests that could be continued would be existing "Fast" pieces.
            if ((message = ContinueExistingRequest(id)) != null)
                return (bundle = new MessageBundle(message));

            // Then we check if there are any allowed "Fast" pieces to download
            if (id.IsChoking && (message = GetFromList(id, peerBitfield, id.IsAllowedFastPieces)) != null)
                return (bundle = new MessageBundle(message));

            // If the peer is choking, then we can't download from them as they had no "fast" pieces for us to download
            if (id.IsChoking)
                return null;

            // If we are only requesting 1 piece, then we can continue any existing. Otherwise we should try
            // to request the full amount first, then try to continue any existing.
            if (count == 1 && (message = ContinueAnyExisting(id)) != null)
                return (bundle = new MessageBundle(message));

            // We see if the peer has suggested any pieces we should request
            if ((message = GetFromList(id, peerBitfield, id.SuggestedPieces)) != null)
                return (bundle = new MessageBundle(message));

            // Now we see what pieces the peer has that we don't have and try and request one
            if ((bundle = GetStandardRequest(id, peerBitfield, otherPeers, startIndex, endIndex, count)) != null)
                return bundle;

            // If all else fails, ignore how many we're requesting and try to continue any existing
            if ((message = ContinueAnyExisting(id)) != null)
                return (bundle = new MessageBundle(message));

            return null;
        }

        public override void Reset()
        {
            Requests.Clear();
        }

        public override bool ValidatePiece(PeerId id, int pieceIndex, int startOffset, int length, out Piece piece)
        {
            //Comparer.index = pieceIndex;
            var pIndex = Requests.BinarySearch(null, new BinaryIndexComparer(pieceIndex));
            if (pIndex < 0)
            {
                piece = null;
                Logger.Log(null, "Validating: {0} - {1}: ", pieceIndex, startOffset);
                Logger.Log(null, "No piece");
                return false;
            }
            piece = Requests[pIndex];
            // Pick out the block that this piece message belongs to
            int blockIndex = Block.IndexOf(piece.Blocks, startOffset, length);
            if (blockIndex == -1 || !id.Equals(piece.Blocks[blockIndex].RequestedOff))
            {
                Logger.Log (null, "Validating: {0} - {1}: ", pieceIndex, startOffset);
                Logger.Log (null, "no block");
                return false;
            }
            if (piece.Blocks[blockIndex].Received)
            {
                Logger.Log (null, "Validating: {0} - {1}: ", pieceIndex, startOffset);
                Logger.Log (null, "received");
                return false;
            }
            if (!piece.Blocks[blockIndex].Requested)
            {
                Logger.Log (null, "Validating: {0} - {1}: ", pieceIndex, startOffset);
                Logger.Log (null, "not requested");
                return false;
            }
            id.AmRequestingPiecesCount--;
            piece.Blocks[blockIndex].Received = true;

            if (piece.AllBlocksReceived)
                Requests.RemoveAt(pIndex);
            return true;
        }

        public override RequestMessage ContinueExistingRequest(PeerId id)
        {
            foreach (var piece in Requests)
            {
                // For each piece that was assigned to this peer, try to request a block from it
                // A piece is 'assigned' to a peer if he is the first person to request a block from that piece
                if (piece.AllBlocksRequested || !id.Equals(piece.Blocks[0].RequestedOff))
                    continue;

                for (int i = 0; i < piece.BlockCount; i++)
                {
                    if (piece.Blocks[i].Requested || piece.Blocks[i].Received)
                        continue;

                    piece.Blocks[i].Requested = true;
                    return piece.Blocks[i].CreateRequest(id);
                }
            }

            // If we get here it means all the blocks in the pieces being downloaded by the peer are already requested
            return null;
        }

        protected RequestMessage ContinueAnyExisting(PeerId id)
        {
            // If this peer is currently a 'dodgy' peer, then don't allow him to help with someone else's
            // piece request.
            if (id.Peer.RepeatedHashFails != 0)
                return null;

            // Otherwise, if this peer has any of the pieces that are currently being requested, try to
            // request a block from one of those pieces
            foreach (var piece in Requests)
            {
                // If the peer who this piece is assigned to is dodgy or if the blocks are all request or
                // the peer doesn't have this piece, we don't want to help download the piece.
                if (piece.AllBlocksRequested || piece.AllBlocksReceived || !id.BitField[piece.Index] ||
                    (piece.Blocks[0].RequestedOff != null && piece.Blocks[0].RequestedOff.Peer.RepeatedHashFails != 0))
                    continue;

                for (int i = 0; i < piece.Blocks.Length; i++)
                    if (!piece.Blocks[i].Requested && !piece.Blocks[i].Received)
                    {
                        piece.Blocks[i].Requested = true;
                        return piece.Blocks[i].CreateRequest(id);
                    }
            }

            return null;
        }

        protected RequestMessage GetFromList(PeerId id, BitField bitfield, IList<int> pieces)
        {
            if (!id.SupportsFastPeer || !ClientEngine.SupportsFastPeer)
                return null;

            for (int i = 0; i < pieces.Count; i++)
            {
                int index = pieces[i];
                // A peer should only suggest a piece he has, but just in case.
                if (index >= bitfield.Length || !bitfield[index] || AlreadyRequested(index))
                    continue;

                pieces.RemoveAt(i);
                var p = new Piece(index, id.TorrentManager.Torrent.PieceLength, id.TorrentManager.Torrent.Size);
                Requests.Add(p);
                p.Blocks[0].Requested = true;
                return p.Blocks[0].CreateRequest(id);
            }


            return null;
        }

        protected virtual MessageBundle GetStandardRequest(PeerId id, BitField current, List<PeerId> otherPeers, int startIndex, int endIndex, int count)
        {
            int piecesNeeded = (count * Piece.BlockSize) / id.TorrentManager.Torrent.PieceLength;
            if ((count * Piece.BlockSize) % id.TorrentManager.Torrent.PieceLength != 0)
                piecesNeeded++;
            int checkIndex = CanRequest(current, startIndex, endIndex, ref piecesNeeded);

            // Nothing to request.
            if (checkIndex == -1)
                return null;

            var bundle = new MessageBundle();
            for (int i = 0; bundle.Messages.Count < count && i < piecesNeeded; i++)
            {
                // Request the piece
                var p = new Piece(checkIndex + i, id.TorrentManager.Torrent.PieceLength, id.TorrentManager.Torrent.Size);
                Requests.Add(p);

                for (int j = 0; j < p.Blocks.Length && bundle.Messages.Count < count; j++)
                {
                    p.Blocks[j].Requested = true;
                    bundle.Messages.Add(p.Blocks[j].CreateRequest(id));
                }
            }
            return bundle;
        }

        protected bool AlreadyRequested(int index)
        {
            return Requests.BinarySearch(null, new BinaryIndexComparer(index)) >= 0;
        }

        private int CanRequest(BitField bitfield, int pieceStartIndex, int pieceEndIndex, ref int pieceCount)
        {
            int largestStart=0;
            int largestEnd=0;
            while ((pieceStartIndex = bitfield.FirstTrue(pieceStartIndex, pieceEndIndex)) != -1)
            {
                int end = bitfield.FirstFalse(pieceStartIndex, pieceEndIndex);
                if (end == -1)
                    end = Math.Min(pieceStartIndex + pieceCount, bitfield.Length);

                for (int i = pieceStartIndex; i < end; i++)
                    if (AlreadyRequested(i))
                        end = i;

                if ((end - pieceStartIndex) >= pieceCount)
                    return pieceStartIndex;

                if ((largestEnd - largestStart) < (end - pieceStartIndex))
                {
                    largestStart = pieceStartIndex;
                    largestEnd = end;
                }

                pieceStartIndex = Math.Max(pieceStartIndex + 1, end);
            }

            pieceCount = largestEnd - largestStart;
            return pieceCount == 0 ? -1 : largestStart;
        }
    }
}
