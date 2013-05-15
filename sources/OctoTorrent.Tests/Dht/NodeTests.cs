#if !DISABLE_DHT
namespace OctoTorrent.Tests.Dht
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using System.Net;
    using BEncoding;
    using OctoTorrent.Common;
    using OctoTorrent.Dht;

    [TestFixture]
    public class NodeTests
    {
        [Test]
        public void CompactPort()
        {
            var node = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Parse("1.21.121.3"), 511));
            var port = node.CompactPort();

            Assert.AreEqual(1, port.TextBytes[0], "#1");
            Assert.AreEqual(21, port.TextBytes[1], "#1");
            Assert.AreEqual(121, port.TextBytes[2], "#1");
            Assert.AreEqual(3, port.TextBytes[3], "#1");
            Assert.AreEqual(1, port.TextBytes[4], "#1");
            Assert.AreEqual(255, port.TextBytes[5], "#1");
        }

        [Test]
        public void FromCompactNode()
        {
            var buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 1, 21, 131, 3, 1, 255 };
            var node = Node.FromCompactNode(buffer, 0);

            Assert.IsTrue(Toolbox.ByteMatch(buffer, 0, node.Id.Bytes, 0, 20), "#1");
            Assert.AreEqual(IPAddress.Parse("1.21.131.3"), node.EndPoint.Address, "#2");
            Assert.AreEqual(511, node.EndPoint.Port, "#3");
        }

        [Test]
        public void CompactNode()
        {
            var node = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Parse("1.21.121.3"), 511));
            var port = node.CompactNode();

            Assert.IsTrue(Toolbox.ByteMatch(node.Id.Bytes, 0, port.TextBytes, 0, 20), "#A");
            Assert.AreEqual(1, port.TextBytes[20], "#1");
            Assert.AreEqual(21, port.TextBytes[21], "#1");
            Assert.AreEqual(121, port.TextBytes[22], "#1");
            Assert.AreEqual(3, port.TextBytes[23], "#1");
            Assert.AreEqual(1, port.TextBytes[24], "#1");
            Assert.AreEqual(255, port.TextBytes[25], "#1");
        }

        [Test]
        public void SortByLastSeen()
        {
            var nodes = new List<Node>();
            var start = DateTime.Now;

            for (var i = 0; i < 5; i++)
            {
                var node = new Node(NodeId.Create(), new IPEndPoint(IPAddress.Any, 0))
                               {
                                   LastSeen = start.AddDays(-i)
                               };
                nodes.Add(node);
            }

            nodes.Sort();

            Assert.AreEqual(start.AddDays(-4), nodes[0].LastSeen);
            Assert.AreEqual(start, nodes[4].LastSeen);
        }
    }
}
#endif