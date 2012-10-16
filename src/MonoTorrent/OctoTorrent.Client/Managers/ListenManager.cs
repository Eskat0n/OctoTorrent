namespace MonoTorrent.Client
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Common;
    using Encryption;
    using Messages;
    using Messages.Standard;

    /// <summary>
    ///   Instance methods of this class are threadsafe
    /// </summary>
    public class ListenManager : IDisposable
    {
        #region Member Variables

        private readonly AsyncCallback _endCheckEncryptionCallback;
        private readonly AsyncMessageReceivedCallback _handshakeReceivedCallback;
        private readonly MonoTorrentCollection<PeerListener> _listeners;
        private ClientEngine _engine;

        #endregion Member Variables

        #region Properties

        public MonoTorrentCollection<PeerListener> Listeners
        {
            get { return _listeners; }
        }

        internal ClientEngine Engine
        {
            get { return _engine; }
            private set { _engine = value; }
        }

        #endregion Properties

        #region Constructors

        internal ListenManager(ClientEngine engine)
        {
            Engine = engine;
            _listeners = new MonoTorrentCollection<PeerListener>();
            _endCheckEncryptionCallback = ClientEngine.MainLoop.Wrap(EndCheckEncryption);
            _handshakeReceivedCallback = (a, b, c) => ClientEngine.MainLoop.Queue(() => OnPeerHandshakeReceived(a, b, c));
        }

        #endregion Constructors

        #region Public Methods

        public void Dispose()
        {
        }

        public void Register(PeerListener listener)
        {
            listener.ConnectionReceived += ConnectionReceived;
        }

        public void Unregister(PeerListener listener)
        {
            listener.ConnectionReceived -= ConnectionReceived;
        }

        #endregion Public Methods

        private void ConnectionReceived(object sender, NewConnectionEventArgs e)
        {
            if (_engine.ConnectionManager.ShouldBanPeer(e.Peer))
            {
                e.Connection.Dispose();
                return;
            }
            var id = new PeerId(e.Peer, e.TorrentManager) {Connection = e.Connection};

            Logger.Log(id.Connection, "ListenManager - ConnectionReceived");

            if (id.Connection.IsIncoming)
            {
                var skeys = new List<InfoHash>();

                ClientEngine.MainLoop.QueueWait(delegate
                                                    {
                                                        for (var i = 0; i < _engine.Torrents.Count; i++)
                                                            skeys.Add(_engine.Torrents[i].InfoHash);
                                                    });

                EncryptorFactory.BeginCheckEncryption(id, HandshakeMessage.HandshakeLength, _endCheckEncryptionCallback,
                                                      id, skeys.ToArray());
            }
            else
            {
                ClientEngine.MainLoop.Queue(() => _engine.ConnectionManager.ProcessFreshConnection(id));
            }
        }

        private void EndCheckEncryption(IAsyncResult result)
        {
            var id = (PeerId) result.AsyncState;
            try
            {
                byte[] initialData;
                EncryptorFactory.EndCheckEncryption(result, out initialData);

                if (initialData != null && initialData.Length == HandshakeMessage.HandshakeLength)
                {
                    var message = new HandshakeMessage();
                    message.Decode(initialData, 0, initialData.Length);
                    HandleHandshake(id, message);
                }
                else if (initialData.Length > 0)
                {
                    throw new Exception("Argh. I can't handle this scenario. It also shouldn't happen. Ever.");
                }
                else
                {
                    PeerIO.EnqueueReceiveHandshake(id.Connection, id.Decryptor, _handshakeReceivedCallback, id);
                }
            }
            catch
            {
                id.Connection.Dispose();
            }
        }


        private void HandleHandshake(PeerId id, HandshakeMessage message)
        {
            TorrentManager man = null;
            try
            {
                if (message.ProtocolString != VersionInfo.ProtocolStringV100)
                    throw new ProtocolException("Invalid protocol string in handshake");
            }
            catch (Exception ex)
            {
                Logger.Log(id.Connection, ex.Message);
                id.Connection.Dispose();
                return;
            }

            ClientEngine.MainLoop.QueueWait(() =>
                                                {
                                                    foreach (var torrentManager in _engine.Torrents.Where(tm => message.InfoHash == tm.InfoHash))
                                                        man = torrentManager;
                                                });

            //FIXME: #warning FIXME: Don't stop the message loop until Dispose() and track all incoming connections
            if (man == null) // We're not hosting that torrent
            {
                Logger.Log(id.Connection, "ListenManager - Handshake requested nonexistant torrent");
                id.Connection.Dispose();
                return;
            }
            if (man.State == TorrentState.Stopped)
            {
                Logger.Log(id.Connection, "ListenManager - Handshake requested for torrent which is not running");
                id.Connection.Dispose();
                return;
            }
            if (!man.Mode.CanAcceptConnections)
            {
                Logger.Log(id.Connection, "ListenManager - Current mode does not support connections");
                id.Connection.Dispose();
                return;
            }

            id.Peer.PeerId = message.PeerId;
            id.TorrentManager = man;

            // If the handshake was parsed properly without encryption, then it definitely was not encrypted. If this is not allowed, abort
            if ((id.Encryptor is PlainTextEncryption &&
                 !Toolbox.HasEncryption(_engine.Settings.AllowedEncryption, EncryptionTypes.PlainText)) &&
                ClientEngine.SupportsEncryption)
            {
                Logger.Log(id.Connection, "ListenManager - Encryption is required but was not active");
                id.Connection.Dispose();
                return;
            }

            message.Handle(id);
            Logger.Log(id.Connection, "ListenManager - Handshake successful handled");

            id.ClientApp = new Software(message.PeerId);

            message = new HandshakeMessage(id.TorrentManager.InfoHash, _engine.PeerId, VersionInfo.ProtocolStringV100);
            var callback = _engine.ConnectionManager.IncomingConnectionAcceptedCallback;
            PeerIO.EnqueueSendMessage(id.Connection, id.Encryptor, message, id.TorrentManager.UploadLimiter,
                                      id.Monitor, id.TorrentManager.Monitor, callback, id);
        }

        /// <summary>
        /// </summary>
        private void OnPeerHandshakeReceived(bool succeeded, PeerMessage message, object state)
        {
            var id = (PeerId) state;

            try
            {
                if (succeeded)
                    HandleHandshake(id, (HandshakeMessage) message);
                else
                    id.Connection.Dispose();
            }
            catch (Exception)
            {
                Logger.Log(id.Connection, "ListenManager - Socket exception receiving handshake");
                id.Connection.Dispose();
            }
        }
    }
}