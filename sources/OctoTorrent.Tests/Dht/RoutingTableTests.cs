#if !DISABLE_DHT
namespace OctoTorrent.Tests.Dht
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using System.Net;
    using OctoTorrent.Dht;

    [TestFixture]
    public class RoutingTableTests
    {
        private byte[] _id;
        private RoutingTable _table;
        private Node _node;
        private int _addedCount;
        
        [SetUp]
        public void Setup()
        {
            _id = new byte[20];
            _id[1] = 128;
            _node = new Node(new NodeId(_id), new IPEndPoint(IPAddress.Any, 0));
            _table = new RoutingTable(_node);
            _table.NodeAdded += delegate { _addedCount++; };
            _table.Add(_node); //the local node is no more in routing table so add it to show test is still ok
            _addedCount = 0;
        }

        [Test]
        public void AddSame()
        {
            _table.Clear();
            for (var i = 0; i < Bucket.MaxCapacity; i++)
            {
                var id = (byte[])_id.Clone();
                _table.Add(new Node(new NodeId(id), new IPEndPoint(IPAddress.Any, 0)));
            }

            Assert.AreEqual(1, _addedCount, "#a");
            Assert.AreEqual(1, _table.Buckets.Count, "#1");
            Assert.AreEqual(1, _table.Buckets[0].Nodes.Count, "#2");

            CheckBuckets();
        }

        [Test]
        public void AddSimilar()
        {
            for (var i = 0; i < Bucket.MaxCapacity * 3; i++)
            {
                var id = (byte[])_id.Clone();
                id[0] += (byte)i;
                _table.Add(new Node(new NodeId(id), new IPEndPoint(IPAddress.Any, 0)));
            }

            Assert.AreEqual(Bucket.MaxCapacity * 3 - 1, _addedCount, "#1");
            Assert.AreEqual(6, _table.Buckets.Count, "#2");
            Assert.AreEqual(8, _table.Buckets[0].Nodes.Count, "#3");
            Assert.AreEqual(8, _table.Buckets[1].Nodes.Count, "#4");
            Assert.AreEqual(8, _table.Buckets[2].Nodes.Count, "#5");
            Assert.AreEqual(0, _table.Buckets[3].Nodes.Count, "#6");
            Assert.AreEqual(0, _table.Buckets[4].Nodes.Count, "#7");
            Assert.AreEqual(0, _table.Buckets[5].Nodes.Count, "#8");
            CheckBuckets();
        }

        [Test]
        public void GetClosestTest()
        {
            List<NodeId> nodes;
            TestHelper.ManyNodes(out _table, out nodes);
            
            var closest = _table.GetClosest(_table.LocalNode.Id);

            Assert.AreEqual(8, closest.Count, "#1");

            for (var i = 0; i < 8; i++)
                Assert.IsTrue(closest.Exists(node => nodes[i].Equals(closest[i].Id)));
        }
        
        private void CheckBuckets()
        {
            foreach (var bucket in _table.Buckets)
                foreach (var node in bucket.Nodes)
                    Assert.IsTrue(node.Id >= bucket.Min && node.Id < bucket.Max);
        }
    }
}
#endif