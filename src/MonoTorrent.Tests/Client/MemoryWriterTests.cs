namespace MonoTorrent.Client
{
    using System.Linq;
    using Common;
    using NUnit.Framework;
    using PieceWriters;

    public class MemoryWriterTests
    {
        private byte[] _buffer;
        private MemoryWriter _level1;
        private MemoryWriter _level2;

        private TorrentFile[] _multiFile;

        private int _pieceLength;
        private TorrentFile _singleFile;
        private long _torrentSize;

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            _pieceLength = Piece.BlockSize*2;
            _singleFile = new TorrentFile("path", Piece.BlockSize*5);
            _multiFile = new[]
                            {
                                new TorrentFile("first", Piece.BlockSize - 550),
                                new TorrentFile("second", 100),
                                new TorrentFile("third", Piece.BlockSize)
                            };
            _buffer = new byte[Piece.BlockSize];
            _torrentSize = _multiFile.Sum(x => x.Length);
        }

        [SetUp]
        public void Setup()
        {
            Initialize(_buffer, 1);
            _level2 = new MemoryWriter(new NullWriter(), Piece.BlockSize*3);
            _level1 = new MemoryWriter(_level2, Piece.BlockSize*3);
        }

        [Test]
        public void FillFirstBuffer()
        {
            // Write 4 blocks to the stream and then verify they can all be read
            for (var i = 0; i < 4; i++)
            {
                Initialize(_buffer, (byte) (i + 1));
                _level1.Write(_singleFile, Piece.BlockSize*i, _buffer, 0, _buffer.Length);
            }

            // Read them all back out and verify them
            for (var i = 0; i < 4; i++)
            {
                _level1.Read(_singleFile, Piece.BlockSize*i, _buffer, 0, Piece.BlockSize);
                Verify(_buffer, (byte) (i + 1));
            }
        }

        [Test]
        public void ReadWriteBlock()
        {
            _level1.Write(_singleFile, 0, _buffer, 0, _buffer.Length);
            _level1.Read(_singleFile, 0, _buffer, 0, _buffer.Length);
            Verify(_buffer, 1);
        }

        [Test]
        public void ReadWriteBlockChangeOriginal()
        {
            _level1.Write(_singleFile, 0, _buffer, 0, _buffer.Length);
            Initialize(_buffer, 5);
            _level1.Read(_singleFile, 0, _buffer, 0, _buffer.Length);
            Verify(_buffer, 1);
        }

        [Test]
        public void ReadWriteSpanningBlock()
        {
            // Write one block of data to the memory stream. 
            var file1 = (int) _multiFile[0].Length;
            var file2 = (int) _multiFile[1].Length;
            var file3 = Piece.BlockSize - file1 - file2;

            Initialize(_buffer, 1);
            _level1.Write(_multiFile[0], 0, _buffer, 0, file1);

            Initialize(_buffer, 2);
            _level1.Write(_multiFile[1], 0, _buffer, 0, file2);

            Initialize(_buffer, 3);
            _level1.Write(_multiFile[2], 0, _buffer, 0, file3);

            // Read the block from the memory stream
            _level1.Read(_multiFile, 0, _buffer, 0, Piece.BlockSize, _pieceLength, _torrentSize);

            // Ensure that the data is in the buffer exactly as expected.
            Verify(_buffer, 0, file1, 1);
            Verify(_buffer, file1, file2, 2);
            Verify(_buffer, file1 + file2, file3, 3);
        }

        private static void Initialize(byte[] buffer, byte value)
        {
            for (var i = 0; i < buffer.Length; i++)
                buffer[i] = value;
        }

        private static void Verify(byte[] buffer, byte expected)
        {
            Verify(buffer, 0, buffer.Length, expected);
        }

        private static void Verify(byte[] buffer, int startOffset, int count, byte expected)
        {
            for (var i = startOffset; i < startOffset + count; i++)
                Assert.AreEqual(buffer[i], expected, "#" + i);
        }
    }
}