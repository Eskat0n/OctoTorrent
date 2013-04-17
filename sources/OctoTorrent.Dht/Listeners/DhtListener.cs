#if !DISABLE_DHT

namespace OctoTorrent.Dht.Listeners
{
    using System.Net;

    public delegate void MessageReceived(byte[] buffer, IPEndPoint endpoint);

    public class DhtListener : UdpListener
    {
        public DhtListener(IPEndPoint endpoint)
            : base(endpoint)
        {
        }

        public event MessageReceived MessageReceived;

        protected override void OnMessageReceived(byte[] buffer, IPEndPoint endpoint)
        {
            var onMessageReceived = MessageReceived;
            if (onMessageReceived != null)
                onMessageReceived(buffer, endpoint);
        }
    }
}

#endif