 //
// ConnectionListener.cs
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
    using System.Net;
    using System.Net.Sockets;
    using Encryption;
    using Common;
    using Connections;

    /// <summary>
    /// Accepts incoming connections and passes them off to the right TorrentManager
    /// </summary>
    public class SocketListener : PeerListener
    {
        private readonly AsyncCallback _endAcceptCallback;
        private Socket _listener;

        public SocketListener(IPEndPoint endpoint)
            : base(endpoint)
        {
            _endAcceptCallback = EndAccept;
        }

        private void EndAccept(IAsyncResult result)
        {
            Socket peerSocket = null;
            try
            {
                var listener = (Socket)result.AsyncState;
                peerSocket = listener.EndAccept(result);

                var endpoint = (IPEndPoint)peerSocket.RemoteEndPoint;
                var uri = new Uri(string.Format("tcp://{0}{1}{2}", endpoint.Address, ':', endpoint.Port));
                var peer = new Peer("", uri, EncryptionTypes.All);
                var connection = peerSocket.AddressFamily == AddressFamily.InterNetwork
                                     ? (IConnection) new IPV4Connection(peerSocket, true)
                                     : new IPV6Connection(peerSocket, true);


                RaiseConnectionReceived(peer, connection, null);
            }
            catch (SocketException)
            {
                // Just dump the connection
                if (peerSocket != null)
                    peerSocket.Close();
            }
            catch (ObjectDisposedException)
            {
                // We've stopped listening
            }
            finally
            {
                try
                {
                    if (Status == ListenerStatus.Listening)
                        _listener.BeginAccept(_endAcceptCallback, _listener);
                }
                catch (ObjectDisposedException)
                {

                }
            }
        }

        public override void Start()
        {
            if (Status == ListenerStatus.Listening)
                return;

            try
            {
                _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _listener.Bind(Endpoint);
                _listener.Listen(6);
                _listener.BeginAccept(_endAcceptCallback, _listener);
                RaiseStatusChanged(ListenerStatus.Listening);
            }
            catch (SocketException)
            {
                RaiseStatusChanged(ListenerStatus.PortNotFree);
            }
        }

        public override void Stop()
        {
            RaiseStatusChanged(ListenerStatus.NotListening);

            if (_listener != null)
                _listener.Close();
        }
    }
}