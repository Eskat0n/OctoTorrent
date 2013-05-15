#if !DISABLE_DHT
namespace OctoTorrent.Tests.Dht
{
    using System;
    using System.Threading;
    using NUnit.Framework;
    using System.Net;
    using OctoTorrent.Dht;

    [TestFixture]
    public class TokenTest
    {
        [Test]
        public void CheckTokenGenerator()
        {
            var manager = new TokenManager {Timeout = TimeSpan.FromMilliseconds(75)};

            var node1 = new Node(NodeId.Create(),new IPEndPoint(IPAddress.Parse("127.0.0.1"), 25));
            var node2 = new Node(NodeId.Create(),new IPEndPoint(IPAddress.Parse("127.0.0.2"), 25));
            var token1 = manager.GenerateToken(node1);
            var token2 = manager.GenerateToken(node1);

            Assert.AreEqual(token1, token2, "#1");

            Assert.IsTrue(manager.VerifyToken(node1, token1),"#2");
            Assert.IsFalse(manager.VerifyToken(node2, token1),"#3");

            Thread.Sleep(100);
            Assert.IsTrue(manager.VerifyToken(node1, token1), "#4");

            Thread.Sleep(100);
            Assert.IsFalse(manager.VerifyToken(node1, token1), "#5");
        }
    }
}
#endif