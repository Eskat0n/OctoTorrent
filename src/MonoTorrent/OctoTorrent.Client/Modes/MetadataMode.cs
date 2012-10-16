//
// MetadataMode.cs
//
// Authors:
//   Olivier Dufour olivier.duff@gmail.com
//   Alan McGovern alan.mcgovern@gmail.com
// Copyright (C) 2009 Olivier Dufour
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
    using System;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using BEncoding;
    using Common;
    using Messages;
    using Messages.FastPeer;
    using Messages.Libtorrent;
    using Messages.Standard;

    internal class MetadataMode : Mode
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
        private BitField _bitField;
        private PeerId _currentId;
        private DateTime _requestTimeout;
        private string _savePath;

        public MetadataMode(TorrentManager manager, string savePath)
            : base(manager)
        {
            _savePath = savePath;
        }

        public override bool CanHashCheck
        {
            get { return true; }
        }

        public override TorrentState State
        {
            get { return TorrentState.Metadata; }
        }

        internal MemoryStream Stream { get; private set; }

        public override void Tick(int counter)
        {
            //if one request have been sent and we have wait more than timeout
            // request the next peer
            if (_requestTimeout < DateTime.Now)
            {
                SendRequestToNextPeer();
            }
        }

        protected override void HandlePeerExchangeMessage(PeerId id, PeerExchangeMessage message)
        {
            // Nothing
        }

        private void SendRequestToNextPeer()
        {
            NextPeer();

            if (_currentId != null)
            {
                RequestNextNeededPiece(_currentId);
            }
        }

        private void NextPeer()
        {
            var flag = false;

            foreach (var id in Manager.Peers.ConnectedPeers.Where(id => id.SupportsLTMessages && id.ExtensionSupports.Supports(LTMetadata.Support.Name)))
            {
                if (id == _currentId)
                    flag = true;
                else if (flag)
                {
                    _currentId = id;
                    return;
                }
            }
            //second pass without removing the currentid and previous ones
            foreach (var id in Manager.Peers.ConnectedPeers.Where(id => id.SupportsLTMessages && id.ExtensionSupports.Supports(LTMetadata.Support.Name)))
            {
                _currentId = id;
                return;
            }
            _currentId = null;
        }

        protected override void HandleLtMetadataMessage(PeerId id, LTMetadata message)
        {
            base.HandleLtMetadataMessage(id, message);

            switch (message.MetadataMessageType)
            {
                case LTMetadata.eMessageType.Data:
                    if (Stream == null)
                        throw new Exception("Need extention handshake before ut_metadata message.");

                    Stream.Seek(message.Piece*LTMetadata.BlockSize, SeekOrigin.Begin);
                    Stream.Write(message.MetadataPiece, 0, message.MetadataPiece.Length);
                    _bitField[message.Piece] = true;
                    if (_bitField.AllTrue)
                    {
                        byte[] hash;
                        Stream.Position = 0;
                        using (var hasher = HashAlgoFactory.Create<SHA1>())
                            hash = hasher.ComputeHash(Stream);

                        if (!Manager.InfoHash.Equals(hash))
                        {
                            _bitField.SetAll(false);
                        }
                        else
                        {
                            Torrent t;
                            Stream.Position = 0;
                            var dict = new BEncodedDictionary {{"info", BEncodedValue.Decode(Stream)}};
                            // FIXME: Add the trackers too
                            if (Torrent.TryLoad(dict.Encode(), out t))
                            {
                                try
                                {
                                    if (Directory.Exists(_savePath))
                                        _savePath = Path.Combine(_savePath, Manager.InfoHash.ToHex() + ".torrent");
                                    File.WriteAllBytes(_savePath, dict.Encode());
                                }
                                catch (Exception ex)
                                {
                                    Logger.Log(null, "*METADATA EXCEPTION* - Can not write in {0} : {1}", _savePath, ex);
                                    Manager.Error = new Error(Reason.WriteFailure, ex);
                                    Manager.Mode = new ErrorMode(Manager);
                                    return;
                                }
                                t.TorrentPath = _savePath;
                                Manager.Torrent = t;
                                SwitchToRegular();
                            }
                            else
                            {
                                _bitField.SetAll(false);
                            }
                        }
                    }
                    //Double test because we can change the bitfield in the other block
                    if (!_bitField.AllTrue)
                    {
                        RequestNextNeededPiece(id);
                    }
                    break;
                case LTMetadata.eMessageType.Reject:
                    //TODO
                    //Think to what we do in this situation
                    //for moment nothing ;)
                    //reject or flood?
                    break;
                case LTMetadata.eMessageType.Request: //ever done in base class but needed to avoid default
                    break;
                default:
                    throw new MessageException(string.Format("Invalid messagetype in LTMetadata: {0}",
                                                             message.MetadataMessageType));
            }
        }

        private void SwitchToRegular()
        {
            var torrent = Manager.Torrent;
            foreach (var peer in Manager.Peers.ConnectedPeers)
                peer.CloseConnection();
            Manager.Bitfield = new BitField(torrent.Pieces.Count);
            Manager.PieceManager.ChangePicker(Manager.CreateStandardPicker(), Manager.Bitfield, torrent.Files);
            foreach (var file in torrent.Files)
                file.FullPath = Path.Combine(Manager.SavePath, file.Path);
            Manager.Start();
        }

        protected override void HandleHaveAllMessage(PeerId id, HaveAllMessage message)
        {
            // Nothing
        }

        protected override void HandleHaveMessage(PeerId id, HaveMessage message)
        {
            // Nothing
        }

        protected override void HandleHaveNoneMessage(PeerId id, HaveNoneMessage message)
        {
            // Nothing
        }

        protected override void HandleInterestedMessage(PeerId id, InterestedMessage message)
        {
            // Nothing
        }

        private void RequestNextNeededPiece(PeerId id)
        {
            var index = _bitField.FirstFalse();
            if (index == -1)
                return; //throw exception or switch to regular?

            var m = new LTMetadata(id, LTMetadata.eMessageType.Request, index);
            id.Enqueue(m);
            _requestTimeout = DateTime.Now.Add(Timeout);
        }

        internal Torrent GetTorrent()
        {
            byte[] calculatedInfoHash;
            using (var sha = HashAlgoFactory.Create<SHA1>())
                calculatedInfoHash = sha.ComputeHash(Stream.ToArray());
            if (!Manager.InfoHash.Equals(calculatedInfoHash))
                throw new Exception("invalid metadata"); //restart ?

            var d = BEncodedValue.Decode(Stream);
            var dict = new BEncodedDictionary {{"info", d}};

            return Torrent.LoadCore(dict);
        }

        protected override void AppendBitfieldMessage(PeerId id, MessageBundle bundle)
        {
            // We can't send a bitfield message in metadata mode as
            // we don't know what size the bitfield is
        }

        protected override void HandleExtendedHandshakeMessage(PeerId id, ExtendedHandshakeMessage message)
        {
            base.HandleExtendedHandshakeMessage(id, message);

            if (!id.ExtensionSupports.Supports(LTMetadata.Support.Name)) return;
            Stream = new MemoryStream(new byte[message.MetadataSize], 0, message.MetadataSize, true, true);
            var size = message.MetadataSize%LTMetadata.BlockSize;
            if (size > 0)
                size = 1;
            size += message.MetadataSize/LTMetadata.BlockSize;
            _bitField = new BitField(size);
            RequestNextNeededPiece(id);
        }

        protected override void SetAmInterestedStatus(PeerId id, bool interesting)
        {
            // Never set a peer as interesting when in metadata mode
            // we don't want to try download any data
        }
    }
}