namespace OctoTorrent.Tests.Client
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using OctoTorrent.Client;
    using OctoTorrent.Common;

    [TestFixture]
    public class PriorityPickerTests
    {
        private PeerId _id;
        private PriorityPicker _picker;
        private TestRig _rig;
        private TestPicker _tester;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            _rig = TestRig.CreateMultiFile();
            _id = new PeerId(new Peer(new string('a', 20), new Uri("tcp://BLAH")), _rig.Manager);
            _id.BitField.SetAll(true);
        }

        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            _rig.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            _id.BitField.SetAll(true);
            _tester = new TestPicker();
            _picker = new PriorityPicker(_tester);
            _picker.Initialise(_rig.Manager.Bitfield, _rig.Torrent.Files, new List<Piece>());
            foreach (TorrentFile file in _rig.Torrent.Files)
                file.Priority = Priority.Normal;
        }

        [Test]
        public void AllAllowed()
        {
            _picker.PickPiece(_id, _id.BitField, new List<PeerId>(), 1, 0, _rig.Pieces);
            Assert.AreEqual(1, _tester.PickPieceBitfield.Count, "#1");
            Assert.IsTrue(_tester.PickPieceBitfield[0].AllTrue, "#2");
        }

        [Test]
        public void HighPriority()
        {
            _rig.Torrent.Files[0].Priority = Priority.High;
            _rig.Torrent.Files[1].Priority = Priority.High;

            _picker.PickPiece(_id, _id.BitField, new List<PeerId>(), 1, 0, _rig.Pieces);
            Assert.AreEqual(2, _tester.PickPieceBitfield.Count, "#1");
            for (int i = 0; i < _rig.Pieces; i++)
            {
                if (i <= _rig.Torrent.Files[1].EndPieceIndex)
                    Assert.IsTrue(_tester.PickPieceBitfield[0][i], "#2");
                else
                    Assert.IsFalse(_tester.PickPieceBitfield[0][i], "#2");
            }

            for (int i = 0; i < _rig.Pieces; i++)
            {
                if (i < _rig.Torrent.Files[1].EndPieceIndex)
                    Assert.IsFalse(_tester.PickPieceBitfield[1][i], "#2");
                else
                    Assert.IsTrue(_tester.PickPieceBitfield[1][i], "#2");
            }
        }

        [Test]
        public void DoNotDownload()
        {
            _rig.Torrent.Files[0].Priority = Priority.DoNotDownload;
            _rig.Torrent.Files[1].Priority = Priority.DoNotDownload;

            _picker.PickPiece(_id, _id.BitField, new List<PeerId>(), 1, 0, _rig.Pieces);
            Assert.AreEqual(1, _tester.PickPieceBitfield.Count, "#1");
            for (int i = 0; i < _rig.Pieces; i++)
            {
                if (i < _rig.Torrent.Files[1].EndPieceIndex)
                    Assert.IsFalse(_tester.PickPieceBitfield[0][i], "#2");
                else
                    Assert.IsTrue(_tester.PickPieceBitfield[0][i], "#2");
            }
        }

        [Test]
        public void PriorityMix()
        {
            _rig.Torrent.Files[0].Priority = Priority.Immediate;
            _rig.Torrent.Files[1].Priority = Priority.Low;
            _rig.Torrent.Files[2].Priority = Priority.DoNotDownload;
            _rig.Torrent.Files[3].Priority = Priority.High;

            _picker.PickPiece(_id, _id.BitField, new List<PeerId>(), 1, 0, _rig.Pieces);

            Assert.AreEqual(3, _tester.PickPieceBitfield.Count, "#1");

            var bitField = _tester.PickPieceBitfield[0];
            var torrentFile = _rig.Torrent.Files[0];
            for (var i = 0; i < _rig.Pieces; i++)
            {
                if (i >= torrentFile.StartPieceIndex && i <= torrentFile.EndPieceIndex)
                    Assert.IsTrue(bitField[i]);
                else
                    Assert.IsFalse(bitField[i]);
            }

            bitField = _tester.PickPieceBitfield[1];
            torrentFile = _rig.Torrent.Files[3];
            for (var i = 0; i < _rig.Pieces; i++)
            {
                if (i >= torrentFile.StartPieceIndex && i <= torrentFile.EndPieceIndex)
                    Assert.IsTrue(bitField[i]);
                else
                    Assert.IsFalse(bitField[i]);
            }

            bitField = _tester.PickPieceBitfield[2];
            torrentFile = _rig.Torrent.Files[1];
            for (int i = 0; i < _rig.Pieces; i++)
            {
                if (i >= torrentFile.StartPieceIndex && i <= torrentFile.EndPieceIndex)
                    Assert.IsTrue(bitField[i]);
                else
                    Assert.IsFalse(bitField[i]);
            }
        }

        [Test]
        public void SingleFileDoNotDownload()
        {
            _picker.Initialise(_rig.Manager.Bitfield, new TorrentFile[] { _rig.Torrent.Files[0] }, new List<Piece>());
            _rig.Torrent.Files[0].Priority = Priority.DoNotDownload;
            
            _picker.PickPiece(_id, _id.BitField, new List<PeerId>(), 1, 0, _rig.Pieces);
            Assert.AreEqual(0, _tester.PickPieceBitfield.Count, "#1");
        }

        [Test]
        public void SingleFileNoneAvailable()
        {
            _picker.Initialise(_rig.Manager.Bitfield, new TorrentFile[] { _rig.Torrent.Files[0] }, new List<Piece>());
            _id.BitField.SetAll(false);

            _picker.PickPiece(_id, _id.BitField, new List<PeerId>(), 1, 0, _rig.Pieces);
            Assert.AreEqual(0, _tester.PickPieceBitfield.Count, "#1");
        }

        [Test]
        public void MultiFileNoneAvailable()
        {
            _picker.Initialise(_rig.Manager.Bitfield, _rig.Torrent.Files, new List<Piece>());
            _id.BitField.SetAll(false);

            _picker.PickPiece(_id, _id.BitField, new List<PeerId>(), 1, 0, _rig.Pieces);
            Assert.AreEqual(0, _tester.PickPieceBitfield.Count, "#1");
        }

        [Test]
        public void MultiFileAllNoDownload()
        {
            foreach (TorrentFile file in _rig.Torrent.Files)
                file.Priority = Priority.DoNotDownload;

            _picker.PickPiece(_id, _id.BitField, new List<PeerId>(), 1, 0, _rig.Pieces);
            Assert.AreEqual(0, _tester.PickPieceBitfield.Count, "#1");
        }

        [Test]
        public void MultiFileOneAvailable()
        {
            foreach (TorrentFile file in _rig.Torrent.Files)
                file.Priority = Priority.DoNotDownload;
            _rig.Torrent.Files[0].Priority = Priority.High;
            _id.BitField.SetAll(false);   
            _picker.PickPiece(_id, _id.BitField, new List<PeerId>(), 1, 0, _rig.Pieces);
            Assert.AreEqual(0, _tester.PickPieceBitfield.Count, "#1");
        }

        [Test]
        public void IsInteresting()
        {
            foreach (TorrentFile file in _rig.Torrent.Files)
                file.Priority = Priority.DoNotDownload;
            _rig.Torrent.Files[1].Priority = Priority.High;
            _id.BitField.SetAll(false).Set(0, true);
            Assert.IsTrue(_picker.IsInteresting(_id.BitField));
        }
    }
}