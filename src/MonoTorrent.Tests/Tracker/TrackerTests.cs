namespace MonoTorrent.Tracker
{
    using System;
    using System.Threading;
    using Client.Tracker;
    using Common;
    using Listeners;
    using NUnit.Framework;

    [TestFixture]
    public class TrackerTests
    {
        //static void Main(string[] args)
        //{
        //    TrackerTests t = new TrackerTests();
        //    t.FixtureSetup();
        //    t.Setup();
        //    t.MultipleAnnounce();
        //    t.FixtureTeardown();
        //}

        #region Setup/Teardown

        [SetUp]
        public void Setup()
        {
            //tracker = new MonoTorrent.Client.Tracker.HTTPTracker(uri);
        }

        #endregion

        private readonly Uri _uri = new Uri("http://127.0.0.1:23456/");
        private HttpListener _listener;
        private Tracker _server;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            _listener = new HttpListener(_uri.OriginalString);
            _listener.Start();
            _server = new Tracker();
            _server.RegisterListener(_listener);
            _listener.Start();
        }

        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            _listener.Stop();
            _server.Dispose();
        }

        [Test]
        public void MultipleAnnounce()
        {
            var announceCount = 0;
            var random = new Random();
            var handle = new ManualResetEvent(false);

            for (var i = 0; i < 20; i++)
            {
                var infoHash = new InfoHash(new byte[20]);
                random.NextBytes(infoHash.Hash);
                var tier = new TrackerTier(new[] {_uri.ToString()});
                tier.Trackers[0].AnnounceComplete += (sender, args) =>
                                                         {
                                                             if (++announceCount == 20)
                                                                 handle.Set();
                                                         };
                var id = new TrackerConnectionID(tier.Trackers[0], false, TorrentEvent.Started,
                                                 new ManualResetEvent(false));
                var parameters = new MonoTorrent.Client.Tracker.AnnounceParameters(0, 0, 0, TorrentEvent.Started,
                                                                                   infoHash, false, new string('1', 20),
                                                                                   string.Empty, 1411);
                tier.Trackers[0].Announce(parameters, id);
            }

            Assert.IsTrue(handle.WaitOne(5000, true), "Some of the responses weren't received");
        }
    }
}