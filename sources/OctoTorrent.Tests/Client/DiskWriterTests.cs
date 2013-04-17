//
// DiskWriterTests.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
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


namespace OctoTorrent.Tests.Client
{
    using System;
    using OctoTorrent.Client;
    using OctoTorrent.Client.PieceWriters;
    using NUnit.Framework;
    using System.Threading;
    using OctoTorrent.Common;

    public class ExceptionWriter : PieceWriter
    {
        public bool exist, close, flush, move, read, write;

        public override bool Exists(TorrentFile file)
        {
            if (exist)
                throw new Exception("exists");
            return true;
        }

        public override void Close(TorrentFile file)
        {
            if (close)
                throw new Exception("close");
        }

        public override void Flush(TorrentFile file)
        {
            if (flush)
                throw new Exception("flush");
        }

        public override void Move(string oldPath, string newPath, bool ignoreExisting)
        {
            if (move)
                throw new Exception("move");
        }

        public override int Read(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            if (read)
                throw new Exception("read");
            return count;
        }

        public override void Write(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            if (write)
                throw new Exception("write");
        }
    }

    [TestFixture]
    [Category("Integration")]
    public class DiskWriterTests
    {
        private readonly byte[] _data = new byte[Piece.BlockSize];
        private DiskManager _diskManager;
        private ManualResetEvent _handle;
        private TestRig _rig;
        private ExceptionWriter _writer;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            _rig = TestRig.CreateMultiFile();
            _diskManager = _rig.Engine.DiskManager;
        }

        [SetUp]
        public void Setup()
        {
            _writer = new ExceptionWriter();
            _diskManager.Writer = _writer;
            _handle = new ManualResetEvent(false);
            _rig.Manager.Stop();
        }

        [TearDown]
        public void Teardown()
        {
            _handle.Close();
        }

        [TestFixtureTearDown]
        public void FixtureTeardown()
        {
            _rig.Dispose();
        }

        [Test]
        public void CloseFail()
        {
            _writer.close = true;
            Hookup();
            _diskManager.CloseFileStreams(_rig.Manager);
            CheckFail();
        }

        [Test]
        public void FlushFail()
        {
            _writer.flush = true;
            Hookup();
            _diskManager.QueueFlush(_rig.Manager, 0);
            CheckFail();
        }

        [Test]
        public void MoveFail()
        {
            _writer.move = true;
            Hookup();
            _diskManager.MoveFiles(_rig.Manager, "root", true);
            CheckFail();
        }

        [Test]
        public void ReadFail()
        {
            var called = false;
            _writer.read = true;
            Hookup();
            _diskManager.QueueRead(_rig.Manager, 0, _data, _data.Length, delegate { called = true; });
            CheckFail();
            Assert.IsTrue (called, "#delegate called");
        }

        [Test]
        public void WriteFail()
        {
            var called = false;
            _writer.write = true;
            Hookup();
            _diskManager.QueueWrite(_rig.Manager, 0, _data, _data.Length, delegate { called = true; });
            CheckFail();
            Assert.IsTrue (called, "#delegate called");
        }

        private void Hookup()
        {
            _rig.Manager.TorrentStateChanged += delegate {
                if (_rig.Manager.State == TorrentState.Error)
                    _handle.Set();
            };
        }

        void CheckFail()
        {
            Assert.IsTrue(_handle.WaitOne(5000, true), "Failure was not handled");
        }
    }
}
