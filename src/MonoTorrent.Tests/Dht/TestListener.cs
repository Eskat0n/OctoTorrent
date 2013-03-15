#if !DISABLE_DHT
namespace OctoTorrent.Tests.Dht
{
    using OctoTorrent.Dht;
    using OctoTorrent.Dht.Listeners;
    using OctoTorrent.Dht.Messages;
    using System.Net;

    internal class TestListener : DhtListener
    {
        private bool started;

        public TestListener()
            : base(new IPEndPoint(IPAddress.Loopback, 0))
        {

        }

        public bool Started
        {
            get { return started; }
        }

        public override void Send(byte[] buffer, IPEndPoint endpoint)
        {
            // Do nothing
        }

        public void RaiseMessageReceived(Message message, IPEndPoint endpoint)
        {
            DhtEngine.MainLoop.Queue(() => OnMessageReceived(message.Encode(), endpoint));
        }

        public override void Start()
        {
            started = true;
        }

        public override void Stop()
        {
            started = false;
        }
    }
}
#endif