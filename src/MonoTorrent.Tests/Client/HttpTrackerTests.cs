namespace OctoTorrent.Client
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Common;
    using NUnit.Framework;
    using OctoTorrent.Tracker.Listeners;
    using Tracker;

    [TestFixture]
    public class HttpTrackerTests
    {
        //static void Main()
        //{
        //    HttpTrackerTests t = new HttpTrackerTests();
        //    t.FixtureSetup();
        //    t.KeyTest();
        //}

        #region Setup/Teardown

        [SetUp]
        public void Setup()
        {
            _keys.Clear();
        }

        #endregion

        private OctoTorrent.Tracker.Tracker _server;
        private HttpListener _listener;
        private const string Prefix = "http://localhost:47124/announce/";
        private List<string> _keys;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            _keys = new List<string>();
            _server = new OctoTorrent.Tracker.Tracker {AllowUnregisteredTorrents = true};
            _listener = new HttpListener(Prefix);
            _listener.AnnounceReceived += (o, e) => _keys.Add(e.Key);
            _server.RegisterListener(_listener);

            _listener.Start();
        }

        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            _listener.Stop();
            _server.Dispose();
        }

        private static void Wait(WaitHandle handle)
        {
            Assert.IsTrue(handle.WaitOne(1000000, true), "Wait handle failed to trigger");
        }

        [Test]
        public void AnnounceTest()
        {
            var t = (HTTPTracker) TrackerFactory.Create(new Uri(Prefix));
            var id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));

            AnnounceResponseEventArgs p = null;
            t.AnnounceComplete += (o, e) =>
                                      {
                                          p = e;
                                          id.WaitHandle.Set();
                                      };
            var pars = new AnnounceParameters
                           {
                               PeerId = "id",
                               InfoHash = new InfoHash(new byte[20])
                           };

            t.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.IsNotNull(p, "#1");
            Assert.IsTrue(p.Successful);
            Assert.AreEqual(_keys[0], t.Key, "#2");
        }

        [Test]
        public void CanAnnouceOrScrapeTest()
        {
            var t = TrackerFactory.Create(new Uri("http://mytracker.com/myurl"));
            Assert.IsFalse(t.CanScrape, "#1");
            Assert.IsTrue(t.CanAnnounce, "#1b");

            t = TrackerFactory.Create(new Uri("http://mytracker.com/announce/yeah"));
            Assert.IsFalse(t.CanScrape, "#2");
            Assert.IsTrue(t.CanAnnounce, "#2b");

            t = TrackerFactory.Create(new Uri("http://mytracker.com/announce"));
            Assert.IsTrue(t.CanScrape, "#3");
            Assert.IsTrue(t.CanAnnounce, "#4");

            var tracker = (HTTPTracker) TrackerFactory.Create(new Uri("http://mytracker.com/announce/yeah/announce"));
            Assert.IsTrue(tracker.CanScrape, "#4");
            Assert.IsTrue(tracker.CanAnnounce, "#4");
            Assert.AreEqual("http://mytracker.com/announce/yeah/scrape", tracker.ScrapeUri.ToString(), "#5");
        }

        [Test]
        public void KeyTest()
        {
            var pars = new AnnounceParameters
                           {
                               PeerId = "id",
                               InfoHash = new InfoHash(new byte[20])
                           };

            var t = TrackerFactory.Create(new Uri(Prefix + "?key=value"));
            var id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));
            t.AnnounceComplete += delegate { id.WaitHandle.Set(); };
            t.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.AreEqual("value", _keys[0], "#1");
        }

        [Test]
        public void ScrapeTest()
        {
            var t = TrackerFactory.Create(new Uri(Prefix.Substring(0, Prefix.Length - 1)));
            Assert.IsTrue(t.CanScrape, "#1");
            var id = new TrackerConnectionID(t, false, TorrentEvent.Started, new ManualResetEvent(false));

            AnnounceResponseEventArgs p = null;
            t.AnnounceComplete += delegate(object o, AnnounceResponseEventArgs e)
                                      {
                                          p = e;
                                          id.WaitHandle.Set();
                                      };
            var pars = new AnnounceParameters {PeerId = "id", InfoHash = new InfoHash(new byte[20])};

            t.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.IsNotNull(p, "#2");
            Assert.IsTrue(p.Successful, "#3");
            Assert.AreEqual(1, t.Complete, "#1");
            Assert.AreEqual(0, t.Incomplete, "#2");
            Assert.AreEqual(0, t.Downloaded, "#3");
        }
    }
}