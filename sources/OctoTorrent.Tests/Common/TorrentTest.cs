//
// TorrentTest.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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

namespace OctoTorrent.Tests.Common
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using Client;
    using OctoTorrent.BEncoding;
    using NUnit.Framework;
    using OctoTorrent.Common;

    [TestFixture]
    public class TorrentTest
    {
        //static void Main(string[] args)
        //{
        //    TorrentTest t = new TorrentTest();
        //    t.StartUp();

        //}

        private readonly SHA1 _sha = System.Security.Cryptography.SHA1.Create();
        private Torrent _torrent;
        private long _creationTime;

        [SetUp]
        public void StartUp()
        {
            var current = new DateTime(2006, 7, 1, 5, 5, 5);
            var epochStart = new DateTime(1970, 1, 1, 0, 0, 0);
            var span = current - epochStart;
            _creationTime = (long) span.TotalSeconds;
            Console.WriteLine(_creationTime + "Creation seconds");

            var torrentInfo = new BEncodedDictionary
                                  {
                                      {"announce", new BEncodedString("http://myannouceurl/announce")},
                                      {"creation date", new BEncodedNumber(_creationTime)},
                                      {"nodes", new BEncodedList()},
                                      {"comment.utf-8", new BEncodedString("my big long comment")},
                                      {"comment", new BEncodedString("my big long comment")},
                                      {"azureus_properties", new BEncodedDictionary()},
                                      {"created by", new BEncodedString("MonoTorrent/" + VersionInfo.ClientVersion)},
                                      {"encoding", new BEncodedString("UTF-8")},
                                      {"info", CreateInfoDict()},
                                      {"private", new BEncodedString("1")}
                                  };

            _torrent = Torrent.Load(torrentInfo);
        }

        private BEncodedDictionary CreateInfoDict()
        {
            var dict = new BEncodedDictionary
                           {
                               {"source", new BEncodedString("http://www.thisiswhohostedit.com")},
                               {
                                   "sha1",
                                   new BEncodedString(
                                   _sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("this is a sha1 hash string")))
                               },
                               {
                                   "ed2k",
                                   new BEncodedString(
                                   _sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("ed2k isn't a sha, but who cares")))
                               },
                               {"publisher-url.utf-8", new BEncodedString("http://www.iamthepublisher.com")},
                               {"publisher-url", new BEncodedString("http://www.iamthepublisher.com")},
                               {"publisher.utf-8", new BEncodedString("MonoTorrent Inc.")},
                               {"publisher", new BEncodedString("MonoTorrent Inc.")},
                               {"files", CreateFiles()},
                               {"name.utf-8", new BEncodedString("MyBaseFolder")},
                               {"name", new BEncodedString("MyBaseFolder")},
                               {"piece length", new BEncodedNumber(512)},
                               {"private", new BEncodedString("1")},
                               {"pieces", new BEncodedString(new byte[((26000 + 512)/512)*20])}
                           };
            return dict;
        }

        private BEncodedList CreateFiles()
        {
            var files = new BEncodedList();
            var path = new BEncodedList {new BEncodedString("file1.txt")};

            var file = new BEncodedDictionary
                           {
                               {
                                   "sha1",
                                   new BEncodedString(_sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash1")))
                               },
                               {
                                   "ed2k",
                                   new BEncodedString(_sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash2")))
                               },
                               {"length", new BEncodedNumber(50000)},
                               {
                                   "md5sum",
                                   new BEncodedString(_sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash3")))
                               },
                               {"path.utf-8", path},
                               {"path", path}
                           };

            files.Add(file);

            path = new BEncodedList
                       {
                           new BEncodedString("subfolder1"),
                           new BEncodedString("file2.txt")
                       };

            file = new BEncodedDictionary
                       {
                           {
                               "sha1",
                               new BEncodedString(_sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash1")))
                           },
                           {
                               "ed2k",
                               new BEncodedString(_sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash2")))
                           },
                           {"length", new BEncodedNumber(60000)},
                           {
                               "md5sum",
                               new BEncodedString(_sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash3")))
                           },
                           {"path.utf-8", path},
                           {"path", path}
                       };

            files.Add(file);

            path = new BEncodedList
                       {
                           new BEncodedString("subfolder1"),
                           new BEncodedString("subfolder2"),
                           new BEncodedString("file3.txt")
                       };

            file = new BEncodedDictionary
                       {
                           {
                               "sha1",
                               new BEncodedString(_sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash1")))
                           },
                           {
                               "ed2k",
                               new BEncodedString(_sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash2")))
                           },
                           {"length", new BEncodedNumber(70000)},
                           {
                               "md5sum",
                               new BEncodedString(_sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash3")))
                           },
                           {"path.utf-8", path},
                           {"path", path}
                       };

            files.Add(file);

            path = new BEncodedList
                       {
                           new BEncodedString("subfolder1"),
                           new BEncodedString("subfolder2"),
                           new BEncodedString("file4.txt")
                       };

            file = new BEncodedDictionary
                       {
                           {
                               "sha1",
                               new BEncodedString(_sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash1")))
                           },
                           {
                               "ed2k",
                               new BEncodedString(_sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash2")))
                           },
                           {"length", new BEncodedNumber(80000)},
                           {
                               "md5sum",
                               new BEncodedString(_sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("file1 hash3")))
                           },
                           {"path.utf-8", path},
                           {"path", path}
                       };

            files.Add(file);

            return files;
        }

        [Test]
        public void AnnounceUrl()
        {
            Assert.IsTrue(_torrent.AnnounceUrls.Count == 1);
            Assert.IsTrue(_torrent.AnnounceUrls[0].Count == 1);
            Assert.IsTrue(_torrent.AnnounceUrls[0][0] == "http://myannouceurl/announce");
        }

        [Test]
        public void Comment()
        {
            Assert.AreEqual(_torrent.Comment, "my big long comment");
        }

        [Test]
        public void CreatedBy()
        {
            Assert.AreEqual(_torrent.CreatedBy, "MonoTorrent/" + VersionInfo.ClientVersion);
        }

        [Test]
        public void CreationDate()
        {
            Assert.AreEqual(2006, _torrent.CreationDate.Year, "Year wrong");
            Assert.AreEqual(7, _torrent.CreationDate.Month, "Month Wrong");
            Assert.AreEqual(1, _torrent.CreationDate.Day, "Day Wrong");
            Assert.AreEqual(5, _torrent.CreationDate.Hour, "Hour Wrong");
            Assert.AreEqual(5, _torrent.CreationDate.Minute, "Minute Wrong");
            Assert.AreEqual(5, _torrent.CreationDate.Second, "Second Wrong");
            Assert.AreEqual(new DateTime(2006, 7, 1, 5, 5, 5), _torrent.CreationDate);
        }

        [Test]
        public void Ed2K()
        {
            Assert.IsTrue(Toolbox.ByteMatch(_torrent.ED2K,
                                            _sha.ComputeHash(
                                                System.Text.Encoding.UTF8.GetBytes("ed2k isn't a sha, but who cares"))));
        }

        [Test]
        public void Encoding()
        {
            Assert.IsTrue(_torrent.Encoding == "UTF-8");
        }

        [Test]
        public void Files()
        {
            Assert.AreEqual(4, _torrent.Files.Length);

            Assert.AreEqual("file1.txt", _torrent.Files[0].Path);
            Assert.AreEqual(50000, _torrent.Files[0].Length);

            Assert.AreEqual(Path.Combine("subfolder1", "file2.txt"), _torrent.Files[1].Path);
            Assert.AreEqual(60000, _torrent.Files[1].Length);

            Assert.AreEqual(Path.Combine(Path.Combine("subfolder1", "subfolder2"), "file3.txt"), _torrent.Files[2].Path);
            Assert.AreEqual(70000, _torrent.Files[2].Length);

            Assert.AreEqual(Path.Combine(Path.Combine("subfolder1", "subfolder2"), "file4.txt"), _torrent.Files[3].Path);
            Assert.AreEqual(80000, _torrent.Files[3].Length);
        }

        [Test]
        public void Name()
        {
            Assert.IsTrue(_torrent.Name == "MyBaseFolder");
        }

        [Test]
        public void PieceLength()
        {
            Assert.IsTrue(_torrent.PieceLength == 512);
        }

        [Test]
        public void Private()
        {
            Assert.AreEqual(true, _torrent.IsPrivate);
        }

        [Test]
        public void Publisher()
        {
            Assert.IsTrue(_torrent.Publisher == "MonoTorrent Inc.");
        }

        [Test]
        public void PublisherUrl()
        {
            Assert.AreEqual("http://www.iamthepublisher.com", _torrent.PublisherUrl);
        }

        [Test]
        public void SHA1()
        {
            Assert.IsTrue(Toolbox.ByteMatch(_torrent.SHA1,
                                            _sha.ComputeHash(
                                                System.Text.Encoding.UTF8.GetBytes("this is a sha1 hash string"))));
        }

        [Test]
        public void Size()
        {
            Assert.AreEqual((50000 + 60000 + 70000 + 80000), _torrent.Size);
        }

        [Test]
        public void Source()
        {
            Assert.IsTrue(_torrent.Source == "http://www.thisiswhohostedit.com");
        }

        [Test]
        public void StartEndIndices()
        {
            const int pieceLength = 32*32;

            var files = new[]
                            {
                                new TorrentFile("File0", 0),
                                new TorrentFile("File1", pieceLength),
                                new TorrentFile("File2", 0),
                                new TorrentFile("File3", pieceLength - 1),
                                new TorrentFile("File4", 1),
                                new TorrentFile("File5", 236),
                                new TorrentFile("File6", pieceLength*7)
                            };
            var t = TestRig.CreateMultiFile(files, pieceLength).Torrent;

            Assert.AreEqual(0, t.Files[0].StartPieceIndex, "#0a");
            Assert.AreEqual(0, t.Files[0].EndPieceIndex, "#0b");

            Assert.AreEqual(0, t.Files[1].StartPieceIndex, "#1");
            Assert.AreEqual(0, t.Files[1].EndPieceIndex, "#2");

            Assert.AreEqual(0, t.Files[2].StartPieceIndex, "#3");
            Assert.AreEqual(0, t.Files[2].EndPieceIndex, "#4");

            Assert.AreEqual(1, t.Files[3].StartPieceIndex, "#5");
            Assert.AreEqual(1, t.Files[3].EndPieceIndex, "#6");

            Assert.AreEqual(1, t.Files[4].StartPieceIndex, "#7");
            Assert.AreEqual(1, t.Files[4].EndPieceIndex, "#8");

            Assert.AreEqual(2, t.Files[5].StartPieceIndex, "#9");
            Assert.AreEqual(2, t.Files[5].EndPieceIndex, "#10");

            Assert.AreEqual(2, t.Files[6].StartPieceIndex, "#11");
            Assert.AreEqual(9, t.Files[6].EndPieceIndex, "#12");
        }

        [Test]
        public void StartEndIndices2()
        {
            const int pieceLength = 32*32;

            var files = new[]
                            {
                                new TorrentFile("File0", pieceLength),
                                new TorrentFile("File1", 0)
                            };
            var t = TestRig.CreateMultiFile(files, pieceLength).Torrent;

            Assert.AreEqual(0, t.Files[0].StartPieceIndex, "#1");
            Assert.AreEqual(0, t.Files[0].EndPieceIndex, "#2");

            Assert.AreEqual(0, t.Files[1].StartPieceIndex, "#3");
            Assert.AreEqual(0, t.Files[1].EndPieceIndex, "#4");
        }

        [Test]
        public void StartEndIndices3()
        {
            const int pieceLength = 32*32;

            var files = new[]
                            {
                                new TorrentFile("File0", pieceLength - 10),
                                new TorrentFile("File1", 10)
                            };
            var t = TestRig.CreateMultiFile(files, pieceLength).Torrent;

            Assert.AreEqual(0, t.Files[0].StartPieceIndex, "#1");
            Assert.AreEqual(0, t.Files[0].EndPieceIndex, "#2");

            Assert.AreEqual(0, t.Files[1].StartPieceIndex, "#3");
            Assert.AreEqual(0, t.Files[1].EndPieceIndex, "#4");
        }

        [Test]
        public void StartEndIndices4()
        {
            const int pieceLength = 32*32;
            var files = new[]
                            {
                                new TorrentFile("File0", pieceLength - 10),
                                new TorrentFile("File1", 11)
                            };
            var t = TestRig.CreateMultiFile(files, pieceLength).Torrent;

            Assert.AreEqual(0, t.Files[0].StartPieceIndex, "#1");
            Assert.AreEqual(0, t.Files[0].EndPieceIndex, "#2");

            Assert.AreEqual(0, t.Files[1].StartPieceIndex, "#3");
            Assert.AreEqual(1, t.Files[1].EndPieceIndex, "#4");
        }

        [Test]
        public void StartEndIndices5()
        {
            const int pieceLength = 32*32;
            var files = new[]
                            {
                                new TorrentFile("File0", pieceLength - 10),
                                new TorrentFile("File1", 10),
                                new TorrentFile("File1", 1)
                            };
            var t = TestRig.CreateMultiFile(files, pieceLength).Torrent;

            Assert.AreEqual(0, t.Files[0].StartPieceIndex, "#1");
            Assert.AreEqual(0, t.Files[0].EndPieceIndex, "#2");

            Assert.AreEqual(0, t.Files[1].StartPieceIndex, "#3");
            Assert.AreEqual(0, t.Files[1].EndPieceIndex, "#4");

            Assert.AreEqual(1, t.Files[2].StartPieceIndex, "#5");
            Assert.AreEqual(1, t.Files[2].EndPieceIndex, "#6");
        }
    }
}