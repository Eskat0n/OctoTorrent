namespace OctoTorrent.Tests.Client
{
    using NUnit.Framework;
    using System.Net;
    using System.Net.Sockets;
    using OctoTorrent.Client;

    [TestFixture]
    public class ConnectionListenerTests
    {
        //static void Main(string[] args)
        //{
        //    ConnectionListenerTests t = new ConnectionListenerTests();
        //    t.Setup();
        //    t.AcceptThree();
        //    t.Teardown();
        //}
        private SocketListener listener;
        private IPEndPoint endpoint;
        [SetUp]
        public void Setup()
        {
            endpoint = new IPEndPoint(IPAddress.Loopback, 55652);
            listener = new SocketListener(endpoint);
            listener.Start();
            System.Threading.Thread.Sleep(100);
        }

        [TearDown]
        public void Teardown()
        {
            listener.Stop();
        }

        [Test]
        public void AcceptThree()
        {
            using (TcpClient c = new TcpClient(AddressFamily.InterNetwork))
                c.Connect(endpoint);
            using (TcpClient c = new TcpClient(AddressFamily.InterNetwork))
                c.Connect(endpoint);
            using (TcpClient c = new TcpClient(AddressFamily.InterNetwork))
                c.Connect(endpoint);
        }

        [Test]
        public void ChangePortThree()
        {
            endpoint.Port++;
            listener.ChangeEndpoint(endpoint);
            AcceptThree();

            endpoint.Port++;
            listener.ChangeEndpoint(endpoint);
            AcceptThree();

            endpoint.Port++;
            listener.ChangeEndpoint(endpoint);
            AcceptThree();
        }
    }
}
