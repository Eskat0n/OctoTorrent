//
// UdpTrackerTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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

namespace MonoTorrent.Client
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Common;
    using Messages;
    using Messages.UdpTracker;
    using MonoTorrent.Tracker.Listeners;
    using NUnit.Framework;
    using Tracker;

    [TestFixture]
    public class UdpTrackerTests
    {
        #region Setup/Teardown

        [SetUp]
        public void Setup()
        {
            _keys.Clear();
        }

        #endregion

        private static void Main(string[] args)
        {
            var t = new UdpTrackerTests();
            t.ConnectMessageTest();
            t.ConnectResponseTest();
            t.AnnounceMessageTest();
            t.AnnounceResponseTest();
            t.ScrapeMessageTest();
            t.ScrapeResponseTest();
            t.FixtureSetup();

            t.AnnounceTest();
            t.Setup();
            t.ScrapeTest();

            t.FixtureTeardown();
        }

        private const string Prefix = "udp://localhost:6767/announce/";

        private readonly AnnounceParameters _announceParams = new AnnounceParameters(100, 50, int.MaxValue,
                                                                                    TorrentEvent.Completed,
                                                                                    new InfoHash(new byte[]
                                                                                                     {
                                                                                                         1, 2, 3, 4, 5, 1,
                                                                                                         2, 3, 4, 5, 1, 2,
                                                                                                         3, 4, 5, 1, 2, 3,
                                                                                                         4, 5
                                                                                                     }),
                                                                                    false, new string('a', 20), null,
                                                                                    1515);

        private MonoTorrent.Tracker.Tracker _server;
        private UdpListener _listener;
        private List<string> _keys;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            _keys = new List<string>();
            _server = new MonoTorrent.Tracker.Tracker {AllowUnregisteredTorrents = true};
            _listener = new UdpListener(6767);
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

        private static void Check(IMessage message, MessageType type)
        {
            var e = message.Encode();
            Assert.AreEqual(e.Length, message.ByteLength, "#1");
            Assert.IsTrue(Toolbox.ByteMatch(e, UdpTrackerMessage.DecodeMessage(e, 0, e.Length, type).Encode()), "#2");
        }

        private void OfflineAnnounceTest()
        {
            var udpTracker = (UdpTracker) TrackerFactory.Create(new Uri("udp://127.0.0.1:57532/announce"));
            udpTracker.RetryDelay = TimeSpan.FromMilliseconds(500);
            var id = new TrackerConnectionID(udpTracker, false, TorrentEvent.Started, new ManualResetEvent(false));

            AnnounceResponseEventArgs p = null;
            udpTracker.AnnounceComplete += delegate(object o, AnnounceResponseEventArgs e)
                                      {
                                          p = e;
                                          id.WaitHandle.Set();
                                      };
            var pars = new AnnounceParameters
                           {
                               InfoHash = new InfoHash(new byte[20]), 
                               PeerId = ""
                           };

            udpTracker.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.IsNotNull(p, "#1");
            Assert.IsFalse(p.Successful);
        }

        private void OfflineScrapeTest()
        {
            var udpTracker = (UdpTracker) TrackerFactory.Create(new Uri("udp://127.0.0.1:57532/announce"));
            udpTracker.RetryDelay = TimeSpan.FromMilliseconds(500);
            var id = new TrackerConnectionID(udpTracker, false, TorrentEvent.Started, new ManualResetEvent(false));

            ScrapeResponseEventArgs p = null;
            udpTracker.ScrapeComplete += (o, e) =>
                                             {
                                                 if (e.Successful)
                                                     Console.ReadLine();
                                                 p = e;
                                                 id.WaitHandle.Set();
                                             };
            var pars = new ScrapeParameters(new InfoHash(new byte[20]));

            udpTracker.Scrape(pars, id);
            Wait(id.WaitHandle);
            Assert.IsNotNull(p, "#1");
            Assert.IsFalse(p.Successful);
        }

        [Test]
        public void AnnounceMessageTest()
        {
            var m = new AnnounceMessage(0, 12345, _announceParams);
            var d = (AnnounceMessage) UdpTrackerMessage.DecodeMessage(m.Encode(), 0, m.ByteLength, MessageType.Request);
            Check(m, MessageType.Request);

            Assert.AreEqual(1, m.Action);
            Assert.AreEqual(m.Action, d.Action);
            Assert.IsTrue(Toolbox.ByteMatch(m.Encode(), d.Encode()));
            Assert.AreEqual(12345, d.ConnectionId);
        }

        [Test]
        public void AnnounceResponseTest()
        {
            var peers = new List<Peer>
                            {
                                new Peer(new string('1', 20), new Uri("tcp://127.0.0.1:1")),
                                new Peer(new string('2', 20), new Uri("tcp://127.0.0.1:2")),
                                new Peer(new string('3', 20), new Uri("tcp://127.0.0.1:3"))
                            };

            var m = new AnnounceResponseMessage(12345, TimeSpan.FromSeconds(10), 43, 65, peers);
            var d =
                (AnnounceResponseMessage)
                UdpTrackerMessage.DecodeMessage(m.Encode(), 0, m.ByteLength, MessageType.Response);
            Check(m, MessageType.Response);

            Assert.AreEqual(1, m.Action);
            Assert.AreEqual(m.Action, d.Action);
            Assert.IsTrue(Toolbox.ByteMatch(m.Encode(), d.Encode()));
            Assert.AreEqual(12345, d.TransactionId);
        }

        [Test]
        [Ignore("Important test, but could not be runned under CI server environment")]
        public void AnnounceTest()
        {
            var udpTracker = (UdpTracker) TrackerFactory.Create(new Uri(Prefix));
            var id = new TrackerConnectionID(udpTracker, false, TorrentEvent.Started, new ManualResetEvent(false));

            AnnounceResponseEventArgs p = null;
            udpTracker.AnnounceComplete += delegate(object o, AnnounceResponseEventArgs e)
                                               {
                                                   p = e;
                                                   id.WaitHandle.Set();
                                               };
            var pars = new AnnounceParameters
                           {
                               InfoHash = new InfoHash(new byte[20]),
                               PeerId = ""
                           };

            udpTracker.Announce(pars, id);
            Wait(id.WaitHandle);
            Assert.IsNotNull(p, "#1");
            Assert.IsTrue(p.Successful);
            //Assert.AreEqual(keys[0], t.Key, "#2");
        }

        [Test]
        public void AnnounceTestNoConnect()
        {
            var listener = new IgnoringListener(57532);
            try
            {
                listener.IgnoreConnects = true;
                listener.Start();
                OfflineAnnounceTest();
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void AnnounceTestNoAnnounce()
        {
            var listener = new IgnoringListener(57532);
            try
            {
                listener.IgnoreAnnounces = true;
                listener.Start();
                OfflineAnnounceTest();
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void ConnectMessageTest()
        {
            var m = new ConnectMessage();
            var d = (ConnectMessage) UdpTrackerMessage.DecodeMessage(m.Encode(), 0, m.ByteLength, MessageType.Request);
            Check(m, MessageType.Request);

            Assert.AreEqual(0, m.Action, "#0");
            Assert.AreEqual(m.Action, d.Action, "#1");
            Assert.AreEqual(m.ConnectionId, d.ConnectionId, "#2");
            Assert.AreEqual(m.TransactionId, d.TransactionId, "#3");
            Assert.IsTrue(Toolbox.ByteMatch(m.Encode(), d.Encode()), "#4");
        }

        [Test]
        public void ConnectResponseTest()
        {
            var expectedMessage = new ConnectResponseMessage(5371, 12345);
            var actualMessage =
                (ConnectResponseMessage)
                UdpTrackerMessage.DecodeMessage(expectedMessage.Encode(), 0, expectedMessage.ByteLength, MessageType.Response);
            Check(expectedMessage, MessageType.Response);

            Assert.AreEqual(0, expectedMessage.Action, "#0");
            Assert.AreEqual(expectedMessage.Action, actualMessage.Action, "#1");
            Assert.AreEqual(expectedMessage.ConnectionId, actualMessage.ConnectionId, "#2");
            Assert.AreEqual(expectedMessage.TransactionId, actualMessage.TransactionId, "#3");
            Assert.IsTrue(Toolbox.ByteMatch(expectedMessage.Encode(), actualMessage.Encode()), "#4");
            Assert.AreEqual(12345, actualMessage.ConnectionId);
            Assert.AreEqual(5371, actualMessage.TransactionId);
        }

        [Test]
        public void ScrapeMessageTest()
        {
            var hashes = new List<byte[]>();
            var r = new Random();
            var hash1 = new byte[20];
            var hash2 = new byte[20];
            var hash3 = new byte[20];
            r.NextBytes(hash1);
            r.NextBytes(hash2);
            r.NextBytes(hash3);
            hashes.Add(hash1);
            hashes.Add(hash2);
            hashes.Add(hash3);

            var m = new ScrapeMessage(12345, 123, hashes);
            var d = (ScrapeMessage) UdpTrackerMessage.DecodeMessage(m.Encode(), 0, m.ByteLength, MessageType.Request);
            Check(m, MessageType.Request);

            Assert.AreEqual(2, m.Action);
            Assert.AreEqual(m.Action, d.Action);
            Assert.IsTrue(Toolbox.ByteMatch(m.Encode(), d.Encode()));
        }

        [Test]
        public void ScrapeResponseTest()
        {
            var details = new List<ScrapeDetails>
                              {
                                  new ScrapeDetails(1, 2, 3),
                                  new ScrapeDetails(4, 5, 6),
                                  new ScrapeDetails(7, 8, 9)
                              };

            var expectedMessage = new ScrapeResponseMessage(12345, details);
            var actualMessage =
                (ScrapeResponseMessage)
                UdpTrackerMessage.DecodeMessage(expectedMessage.Encode(), 0, expectedMessage.ByteLength, MessageType.Response);
            Check(expectedMessage, MessageType.Response);

            Assert.AreEqual(2, expectedMessage.Action);
            Assert.AreEqual(expectedMessage.Action, actualMessage.Action);
            Assert.IsTrue(Toolbox.ByteMatch(expectedMessage.Encode(), actualMessage.Encode()));
            Assert.AreEqual(12345, actualMessage.TransactionId);
        }

        [Test]
        [Ignore("Important test, but could not be runned under CI server environment")]
        public void ScrapeTest()
        {
            var udpTracker = (UdpTracker) TrackerFactory.Create(new Uri(Prefix));
            Assert.IsTrue(udpTracker.CanScrape, "#1");

            var id = new TrackerConnectionID(udpTracker, false, TorrentEvent.Started, new ManualResetEvent(false));

            ScrapeResponseEventArgs p = null;
            udpTracker.ScrapeComplete += delegate(object o, ScrapeResponseEventArgs e)
                                             {
                                                 p = e;
                                                 id.WaitHandle.Set();
                                             };
            var pars = new ScrapeParameters(new InfoHash(new byte[20]));

            udpTracker.Scrape(pars, id);
            Wait(id.WaitHandle);
            Assert.IsNotNull(p, "#2");
            Assert.IsTrue(p.Successful, "#3");
            Assert.AreEqual(0, udpTracker.Complete, "#1");
            Assert.AreEqual(0, udpTracker.Incomplete, "#2");
            Assert.AreEqual(0, udpTracker.Downloaded, "#3");
        }

        [Test]
        public void ScrapeTestNoConnect()
        {
            var listener = new IgnoringListener(57532);
            try
            {
                listener.IgnoreConnects = true;
                listener.Start();
                OfflineScrapeTest();
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void ScrapeTestNoScrapes()
        {
            var listener = new IgnoringListener(57532);
            try
            {
                listener.IgnoreScrapes = true;
                listener.Start();
                OfflineScrapeTest();
            }
            finally
            {
                listener.Stop();
            }
        }

        private static void Wait(WaitHandle handle)
        {
            Assert.IsTrue(handle.WaitOne(1000000, true), "Wait handle failed to trigger");
        }
    }

    internal class IgnoringListener : UdpListener
    {
        public bool IgnoreAnnounces;
        public bool IgnoreConnects;
        public bool IgnoreErrors;
        public bool IgnoreScrapes;

        public IgnoringListener(int port)
            : base(port)
        {
        }

        protected override void ReceiveConnect(ConnectMessage connectMessage)
        {
            if (!IgnoreConnects)
                base.ReceiveConnect(connectMessage);
        }

        protected override void ReceiveAnnounce(AnnounceMessage announceMessage)
        {
            if (!IgnoreAnnounces)
                base.ReceiveAnnounce(announceMessage);
        }

        protected override void ReceiveError(ErrorMessage errorMessage)
        {
            if (!IgnoreErrors)
                base.ReceiveError(errorMessage);
        }

        protected override void ReceiveScrape(ScrapeMessage scrapeMessage)
        {
            if (!IgnoreScrapes)
                base.ReceiveScrape(scrapeMessage);
        }
    }
}