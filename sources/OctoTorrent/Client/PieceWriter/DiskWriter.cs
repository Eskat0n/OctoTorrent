namespace OctoTorrent.Client.PieceWriters
{
    using System;
    using Common;
    using System.IO;

    public class DiskWriter : PieceWriter
    {
        private readonly FileStreamBuffer _streamsBuffer;

        public int OpenFiles
        {
            get { return _streamsBuffer.Count; }
        }

        public DiskWriter()
            : this(10)
        {

        }

        public DiskWriter(int maxOpenFiles)
        {
            _streamsBuffer = new FileStreamBuffer(maxOpenFiles);
        }

        public override void Close(TorrentFile file)
        {
            _streamsBuffer.CloseStream(file.FullPath);
        }

        public override void Dispose()
        {
            _streamsBuffer.Dispose();
            base.Dispose();
        }

        private TorrentFileStream GetStream(TorrentFile file, FileAccess access)
        {
            return _streamsBuffer.GetStream(file, access);
        }

        public override void Move(string oldPath, string newPath, bool ignoreExisting)
        {
            _streamsBuffer.CloseStream(oldPath);
            if (ignoreExisting)
                File.Delete(newPath);
            File.Move(oldPath, newPath);
        }

        public override int Read(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            Check.File(file);
            Check.Buffer(buffer);

            if (offset < 0 || offset + count > file.Length)
                throw new ArgumentOutOfRangeException("offset");

            var s = GetStream(file, FileAccess.Read);
            if (s.Length < offset + count)
                return 0;
            s.Seek(offset, SeekOrigin.Begin);
            return s.Read(buffer, bufferOffset, count);
        }

        public override void Write(TorrentFile file, long offset, byte[] buffer, int bufferOffset, int count)
        {
            Check.File(file);
            Check.Buffer(buffer);

            if (offset < 0 || offset + count > file.Length)
                throw new ArgumentOutOfRangeException("offset");

            var stream = GetStream(file, FileAccess.ReadWrite);
            stream.Seek(offset, SeekOrigin.Begin);
            stream.Write(buffer, bufferOffset, count);
        }

        public override bool Exists(TorrentFile file)
        {
            return File.Exists(file.FullPath);
        }

        public override void Flush(TorrentFile file)
        {
            var s = _streamsBuffer.FindStream(file.FullPath);
            if (s != null)
                s.Flush();
        }
    }
}
