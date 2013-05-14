namespace OctoTorrent.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using Client;
    using NUnit.Framework;
    using BEncoding;
    using System.IO;
    using OctoTorrent.Client.PieceWriters;
    using System.Security.Cryptography;
    using OctoTorrent.Common;

    public class TestTorrentCreator : TorrentCreator
    {
        protected override PieceWriter CreateReader()
        {
            return new TestWriter {DontWrite = true};
        }
    }

    [TestFixture]
    public class TorrentCreatorTests
    {
        private const string Comment = "My Comment";
        private const string CreatedBy = "Created By MonoTorrent";
        private const int PieceLength = 64*1024;
        private const string Publisher = "My Publisher";
        private const string PublisherUrl = "www.mypublisher.com";
        private readonly BEncodedString _customKey = "Custom Key";
        private readonly BEncodedString _customValue = "My custom value";

        private RawTrackerTiers _announces;
        private TorrentCreator _creator;
        private List<TorrentFile> _files;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            HashAlgoFactory.Register<SHA1, SHA1Fake>();
        }

        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            HashAlgoFactory.Register<SHA1, SHA1CryptoServiceProvider>();
        }

        [SetUp]
        public void Setup()
        {
            _creator = new TestTorrentCreator();
            _announces = new RawTrackerTiers
                             {
                                 new RawTrackerTier(new[]
                                                        {
                                                            "http://tier1.com/announce1",
                                                            "http://tier1.com/announce2"
                                                        }),
                                 new RawTrackerTier(new[]
                                                        {
                                                            "http://tier2.com/announce1",
                                                            "http://tier2.com/announce2"
                                                        })
                             };

            _creator.Comment = Comment;
            _creator.CreatedBy = CreatedBy;
            _creator.PieceLength = PieceLength;
            _creator.Publisher = Publisher;
            _creator.PublisherUrl = PublisherUrl;
            _creator.SetCustom(_customKey, _customValue);

            _files = new List<TorrentFile>(new[] { 
                new TorrentFile(Path.Combine(Path.Combine("Dir1", "SDir1"), "File1"), (int)(PieceLength * 2.30), 0, 1),
                new TorrentFile(Path.Combine(Path.Combine("Dir1", "SDir1"), "File2"), (int)(PieceLength * 36.5), 1, 3),
                new TorrentFile(Path.Combine(Path.Combine("Dir1", "SDir2"), "File3"), (int)(PieceLength * 3.17), 3, 12),
                new TorrentFile(Path.Combine(Path.Combine("Dir2", "SDir1"), "File4"), (int)(PieceLength * 1.22), 12, 15),
                new TorrentFile(Path.Combine(Path.Combine("Dir2", "SDir2"), "File5"), (int)(PieceLength * 6.94), 15, 15)
                                                 });

            new TestWriter {DontWrite = true};
        }

        [Test]
        public void CreateMultiTest()
        {
            foreach (var v in _announces)
                _creator.Announces.Add (v);

            var dict = _creator.Create("TorrentName", _files);
            var torrent = Torrent.Load(dict);

            VerifyCommonParts(torrent);
            foreach (var torrentFile in torrent.Files)
                Assert.IsTrue(_files.Exists(file => file.Equals(torrentFile)));
        }
        [Test]
        public void NoTrackersTest()
        {
            var dict = _creator.Create("TorrentName", _files);
            var torrent = Torrent.Load(dict);

            Assert.AreEqual(0, torrent.AnnounceUrls.Count, "#1");
        }

        [Test]
        public void CreateSingleTest()
        {
            foreach (var v in _announces)
                _creator.Announces.Add (v);

            var torrentFile = new TorrentFile(Path.GetFileName(_files[0].Path),
                                            _files[0].Length,
                                            _files[0].StartPieceIndex,
                                            _files[0].EndPieceIndex);

            var dict = _creator.Create(torrentFile.Path,
                                       new List<TorrentFile>(new[] {torrentFile}));
            var torrent = Torrent.Load(dict);

            VerifyCommonParts(torrent);
            Assert.AreEqual(1, torrent.Files.Length, "#1");
            Assert.AreEqual(torrentFile, torrent.Files[0], "#2");
        }
        [Test]
        public void CreateSingleFromFolder()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var dict = _creator.Create(new TorrentFileSource(assembly.Location));

            var torrent = Torrent.Load(dict);

            Assert.AreEqual(1, torrent.Files.Length, "#1");
            Assert.AreEqual(Path.GetFileName(assembly.Location), torrent.Name, "#2");
            Assert.AreEqual(Path.GetFileName(assembly.Location), torrent.Files[0].Path, "#3");

            // Create it again
            _creator.Create(new TorrentFileSource(assembly.Location));
        }

        [Test]
        public void LargeMultiTorrent()
        {
            var name1 = Path.Combine(Path.Combine("Dir1", "SDir1"), "File1");
            var name2 = Path.Combine(Path.Combine("Dir1", "SDir1"), "File1");
            var name3 = Path.Combine(Path.Combine("Dir1", "SDir1"), "File1");
            var name4 = Path.Combine(Path.Combine("Dir1", "SDir1"), "File1");
            var name5 = Path.Combine(Path.Combine("Dir1", "SDir1"), "File1");

            _files = new List<TorrentFile>(new[] { 
                new TorrentFile(name1, (long)(PieceLength * 200.30), 0, 1),
                new TorrentFile(name2, (long)(PieceLength * 42000.5), 1, 3),
                new TorrentFile(name3, (long)(PieceLength * 300.17), 3, 12),
                new TorrentFile(name4, (long)(PieceLength * 100.22), 12, 15),
                new TorrentFile(name5, (long)(PieceLength * 600.94), 15, 15)
                                                 });

            var torrent = Torrent.Load (_creator.Create("BaseDir", _files));
            Assert.AreEqual(5, torrent.Files.Length, "#1");
            Assert.AreEqual(name1, torrent.Files[0].Path, "#2");
            Assert.AreEqual(name2, torrent.Files[1].Path, "#3");
            Assert.AreEqual(name3, torrent.Files[2].Path, "#4");
            Assert.AreEqual(name4, torrent.Files[3].Path, "#5");
            Assert.AreEqual(name5, torrent.Files[4].Path, "#6");
        }

        [Test]
        [ExpectedException (typeof (ArgumentException))]
        public void IllegalDestinationPath ()
        {
            var source = new CustomFileSource (new List <FileMapping> {
                new FileMapping("a", "../../dest1"),
            });
            new TorrentCreator().Create(source);
        }

        [Test]
        [ExpectedException (typeof (ArgumentException))]
        public void TwoFilesSameDestionation ()
        {
            var source = new CustomFileSource (new List <FileMapping> {
                new FileMapping("a", "dest1"),
                new FileMapping ("b", "dest2"),
                new FileMapping ("c", "dest1"),
            });
            new TorrentCreator ().Create (source);
        }

        static void VerifyCommonParts(Torrent torrent)
        {
            Assert.AreEqual(Comment, torrent.Comment, "#1");
            Assert.AreEqual(CreatedBy, torrent.CreatedBy, "#2");
            Assert.IsTrue((DateTime.Now - torrent.CreationDate) < TimeSpan.FromSeconds(5), "#3");
            Assert.AreEqual(PieceLength, torrent.PieceLength, "#4");
            Assert.AreEqual(Publisher, torrent.Publisher, "#5");
            Assert.AreEqual(PublisherUrl, torrent.PublisherUrl, "#6");
            Assert.AreEqual(2, torrent.AnnounceUrls.Count, "#7");
            Assert.AreEqual(2, torrent.AnnounceUrls[0].Count, "#8");
            Assert.AreEqual(2, torrent.AnnounceUrls[1].Count, "#9");
        }
    }
}
