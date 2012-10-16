//
// LocalPeerListener.cs
//
// Authors:
//   Jared Hendry hendry.jared@gmail.com
//
// Copyright (C) 2008 Jared Hendry
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
    using System.Text;
    using System.Text.RegularExpressions;
    using Common;
    using Encryption;

    class LocalPeerListener : Listener
    {
        const int MulticastPort = 6771;
        static readonly IPAddress MulticastIpAddress = IPAddress.Parse("239.192.152.143");

        private readonly ClientEngine _engine;
        private UdpClient _udpClient;

        public LocalPeerListener(ClientEngine engine)
            : base(new IPEndPoint(IPAddress.Any, 6771))
        {
            _engine = engine;
        }

        public override void Start()
        {
            if (Status == ListenerStatus.Listening)
                return;

            try
            {
                _udpClient = new UdpClient(MulticastPort);
                _udpClient.JoinMulticastGroup(MulticastIpAddress);
                _udpClient.BeginReceive(OnReceiveCallBack, _udpClient);
                RaiseStatusChanged(ListenerStatus.Listening);
            }
            catch
            {
                RaiseStatusChanged(ListenerStatus.PortNotFree);
            }
        }

        public override void Stop()
        {
            if (Status == ListenerStatus.NotListening)
                return;

            RaiseStatusChanged(ListenerStatus.NotListening);
            var udpClient = _udpClient;
            _udpClient = null;
            if (udpClient != null)
                udpClient.Close();
        }

        private void OnReceiveCallBack(IAsyncResult asyncResult)
        {
            var udpClient = (UdpClient) asyncResult.AsyncState;
            var endPoint = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                var receiveBytes = udpClient.EndReceive(asyncResult, ref endPoint);
                var receiveString = Encoding.ASCII.GetString(receiveBytes);

                var exp = new Regex("BT-SEARCH \\* HTTP/1.1\\r\\nHost: 239.192.152.143:6771\\r\\nPort: (?<port>[^@]+)\\r\\nInfohash: (?<hash>[^@]+)\\r\\n\\r\\n\\r\\n");
                var match = exp.Match(receiveString);

                if (!match.Success)
                    return;

                var portcheck = Convert.ToInt32(match.Groups["port"].Value);
                if (portcheck < 0 || portcheck > 65535)
                    return;

                TorrentManager manager = null;
                var matchHash = InfoHash.FromHex(match.Groups["hash"].Value);
                for (var i = 0; manager == null && i < _engine.Torrents.Count; i++)
                    if (_engine.Torrents[i].InfoHash == matchHash)
                        manager = _engine.Torrents[i];
                
                if (manager == null)
                    return;

                var uri = new Uri(string.Format("tcp://{0}{1}{2}", endPoint.Address, ':', match.Groups["port"].Value));
                var peer = new Peer("", uri, EncryptionTypes.All);

                // Add new peer to matched Torrent
                if (!manager.HasMetadata || !manager.Torrent.IsPrivate)
                {
                    ClientEngine.MainLoop.Queue(() =>
                                                    {
                                                        var count = manager.AddPeersCore(peer);
                                                        manager.RaisePeersFound(new LocalPeersAdded(manager, count, 1));
                                                    });
                }
            }
            catch
            {
                // Failed to receive data, ignore
            }
            finally
            {
                try
                {
                    udpClient.BeginReceive(OnReceiveCallBack, asyncResult.AsyncState);
                }
                catch
                {
                    // It's closed
                }
            }
        }
    }
}
