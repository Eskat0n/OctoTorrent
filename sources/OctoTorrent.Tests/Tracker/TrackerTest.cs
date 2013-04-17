namespace OctoTorrent.Tests.Tracker
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using OctoTorrent.BEncoding;
    using System.Net;
    using OctoTorrent.Tracker;

    [TestFixture]
    public class TrackerTest
    {
        public TrackerTest()
        {
        }
        private TrackerTestRig rig;

        [SetUp]
        public void Setup()
        {
            rig = new TrackerTestRig();
        }

        [TearDown]
        public void Teardown()
        {
            rig.Dispose();
        }

        [Test]
        public void AddTrackableTest()
        {
            // Make sure they all add in
            AddAllTrackables();

            // Ensure none are added a second time
            rig.Trackables.ForEach(delegate(Trackable t) { Assert.IsFalse(rig.Tracker.Add(t), "#2"); });

            // Clone each one and ensure that the clone can't be added
            List<Trackable> clones = new List<Trackable>();
            rig.Trackables.ForEach(delegate(Trackable t) { clones.Add(new Trackable(Clone(t.InfoHash), t.Name)); });

            clones.ForEach(delegate(Trackable t) { Assert.IsFalse(rig.Tracker.Add(t), "#3"); });

            Assert.AreEqual(rig.Trackables.Count, rig.Tracker.Count, "#4");
        }

        [Test]
        public void GetManagerTest()
        {
            AddAllTrackables();
            rig.Trackables.ForEach(trackable => Assert.IsNotNull(rig.Tracker.GetManager(trackable)));
        }

        [Test]
        public void AnnouncePeersTest()
        {
            AddAllTrackables();
            rig.Peers.ForEach(peerDetails => rig.Listener.Handle(peerDetails, rig.Trackables[0]));

            var manager = rig.Tracker.GetManager(rig.Trackables[0]);

            Assert.AreEqual(rig.Peers.Count, manager.Count, "#1");
            foreach (var trackable in rig.Trackables)
            {
                var torrentManager = rig.Tracker.GetManager(trackable);
                if (torrentManager == manager)
                    continue;

                Assert.AreEqual(0, torrentManager.Count, "#2");
            }

            foreach (var peer in manager.GetPeers())
            {
                var peerDetails = rig.Peers.Find(details => details.ClientAddress == peer.ClientAddress.Address &&
                                                            details.Port == peer.ClientAddress.Port);
                Assert.AreEqual(peerDetails.Downloaded, peer.Downloaded, "#3");
                Assert.AreEqual(peerDetails.PeerId, peer.PeerId, "#4");
                Assert.AreEqual(peerDetails.Remaining, peer.Remaining, "#5");
                Assert.AreEqual(peerDetails.Uploaded, peer.Uploaded, "#6");
            }
        }

        [Test]
        public void AnnounceInvalidTest()
        {
            rig.Peers.ForEach(peerDetails => rig.Listener.Handle(peerDetails, rig.Trackables[0]));
            Assert.AreEqual(0, rig.Tracker.Count, "#1");
        }

        [Test]
        public void CheckPeersAdded()
        {
            var i = 0;
            AddAllTrackables();

            var lists = new[] { new List<PeerDetails>(), new List<PeerDetails>(), new List<PeerDetails>(), new List<PeerDetails>() };
            rig.Peers.ForEach(peerDetails =>
                                  {
                                      lists[i%4].Add(peerDetails);
                                      rig.Listener.Handle(peerDetails, rig.Trackables[i++%4]);
                                  });

            for (i = 0; i < 4; i++)
            {
                var manager = rig.Tracker.GetManager(rig.Trackables[i]);
                var peers = manager.GetPeers();

                Assert.AreEqual(25, peers.Count, "#1");

                foreach (var peer in peers)
                    Assert.IsTrue(lists[i].Exists(d => d.Port == peer.ClientAddress.Port &&
                                                       d.ClientAddress == peer.ClientAddress.Address));
            }
        }

        [Test]
        public void CustomKeyTest()
        {
            rig.Tracker.Add(rig.Trackables[0], new CustomComparer());
            rig.Listener.Handle(rig.Peers[0], rig.Trackables[0]);

            rig.Peers[0].ClientAddress = IPAddress.Loopback;
            rig.Listener.Handle(rig.Peers[0], rig.Trackables[0]);

            rig.Peers[0].ClientAddress = IPAddress.Broadcast;
            rig.Listener.Handle(rig.Peers[0], rig.Trackables[0]);

            Assert.AreEqual(1, rig.Tracker.GetManager(rig.Trackables[0]).GetPeers().Count, "#1");
        }

        [Test]
        public void TestReturnedPeers()
        {
            rig.Tracker.AllowNonCompact = true;
            rig.Tracker.Add(rig.Trackables[0]);

            var peers = new List<PeerDetails>();
            for (var i = 0; i < 25; i++)
                peers.Add(rig.Peers[i]);

            foreach (var peerDetails in peers)
                rig.Listener.Handle(peerDetails, rig.Trackables[0]);

            var dict = (BEncodedDictionary) rig.Listener.Handle(rig.Peers[24], rig.Trackables[0]);
            var list = (BEncodedList) dict["peers"];

            Assert.AreEqual(25, list.Count, "#1");

            foreach (BEncodedDictionary d in list)
            {
                var up = IPAddress.Parse(d["ip"].ToString());
                var port = (int)((BEncodedNumber)d["port"]).Number;
                var peerId = ((BEncodedString)d["peer id"]).Text;

                Assert.IsTrue(peers.Exists(pd => pd.ClientAddress.Equals(up) &&
                                                 pd.Port == port &&
                                                 pd.PeerId == peerId), "#2");
            }
        }

        private void AddAllTrackables()
        {
            rig.Trackables.ForEach(trackable => Assert.IsTrue(rig.Tracker.Add(trackable), "#1"));
        }

        private InfoHash Clone(InfoHash p)
        {
            return new InfoHash((byte[])p.Hash.Clone());
        }
    }
}
