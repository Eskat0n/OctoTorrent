namespace MonoTorrent.Client
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Security.Cryptography;
    using System.IO;
    using System.Linq;
    using Common;
    using PieceWriters;

    public delegate void DiskIOCallback(bool successful);

    public partial class DiskManager : IDisposable
    {
        private static readonly MainLoop IOLoop = new MainLoop("Disk IO");

        #region Member Variables

        private readonly object _bufferLock = new object();
        private readonly Queue<BufferedIO> _bufferedReads;
        private readonly Queue<BufferedIO> _bufferedWrites;
        private readonly ICache<BufferedIO> _cache;
        private bool _disposed;
        private readonly ClientEngine _engine;
        private readonly MainLoopTask _loopTask;

        private readonly SpeedMonitor _readMonitor;
        private readonly SpeedMonitor _writeMonitor;

        internal readonly RateLimiter ReadLimiter;
        internal readonly RateLimiter WriteLimiter;
        private PieceWriter _writer;

        #endregion Member Variables

        #region Properties

        public bool Disposed
        {
            get { return _disposed; }
        }

        public int QueuedWrites
        {
            get { return _bufferedWrites.Count; }
        }

        public int ReadRate
        {
            get { return _readMonitor.Rate; }
        }

        public int WriteRate
        {
            get { return _writeMonitor.Rate; }
        }

        public long TotalRead
        {
            get { return _readMonitor.Total; }
        }

        public long TotalWritten
        {
            get { return _writeMonitor.Total; }
        }

        internal PieceWriter Writer
        {
            get { return _writer; }
            set { _writer = value; }
        }

        #endregion Properties

        #region Constructors

        internal DiskManager(ClientEngine engine, PieceWriter writer)
        {
            _bufferedReads = new Queue<BufferedIO>();
            _bufferedWrites = new Queue<BufferedIO>();
            _cache = new Cache<BufferedIO>(true).Synchronize();
            _engine = engine;
            ReadLimiter = new RateLimiter();
            _readMonitor = new SpeedMonitor();
            _writeMonitor = new SpeedMonitor();
            WriteLimiter = new RateLimiter();
            _writer = writer;

            _loopTask = delegate {
                if (_disposed)
                    return;

                while (_bufferedWrites.Count > 0 && WriteLimiter.TryProcess(_bufferedWrites.Peek ().buffer.Length / 2048))
                {
                    BufferedIO write;
                    lock (_bufferLock)
                        write = _bufferedWrites.Dequeue();
                    try
                    {
                        PerformWrite(write);
                        _cache.Enqueue (write);
                    }
                    catch (Exception ex)
                    {
                        if (write.Manager != null)
                            SetError(write.Manager, Reason.WriteFailure, ex);
                    }
                }

                while (_bufferedReads.Count > 0 && ReadLimiter.TryProcess(_bufferedReads.Peek().Count / 2048))
                {
                    BufferedIO read;
                    lock (_bufferLock)
                        read = _bufferedReads.Dequeue();

                    try
                    {
                        PerformRead(read);
                        _cache.Enqueue(read);
                    }
                    catch (Exception ex)
                    {
                        if (read.Manager != null)
                            SetError(read.Manager, Reason.ReadFailure, ex);
                    }
                }
            };

            IOLoop.QueueTimeout(TimeSpan.FromSeconds(1), () =>
                                                             {
                                                                 if (_disposed)
                                                                     return false;

                                                                 _readMonitor.Tick();
                                                                 _writeMonitor.Tick();
                                                                 _loopTask();
                                                                 return true;
                                                             });
        }

        #endregion Constructors

        #region Methods

        internal WaitHandle CloseFileStreams(TorrentManager manager)
        {
            var handle = new ManualResetEvent(false);

            IOLoop.Queue(delegate {
				// Process all pending reads/writes then close any open streams
				try
				{
					_loopTask();
					_writer.Close(manager.Torrent.Files);
				}
                catch (Exception ex)
                {
                    SetError (manager, Reason.WriteFailure, ex);
                }
				finally
				{
					handle.Set();
				}
            });

            return handle;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            // FIXME: Ensure everything is written to disk before killing the mainloop.
            IOLoop.QueueWait((MainLoopTask)_writer.Dispose);
        }

        public void Flush()
        {
            IOLoop.QueueWait(() =>
                                 {
                                     foreach (var manager in _engine.Torrents)
                                         _writer.Flush(manager.Torrent.Files);
                                 });
        }

        public void Flush(TorrentManager manager)
        {
            Check.Manager(manager);
            IOLoop.QueueWait(() => _writer.Flush(manager.Torrent.Files));
        }

        private void PerformWrite(BufferedIO io)
        {
            try {
                // Perform the actual write
                _writer.Write(io.Files, io.Offset, io.buffer, 0, io.Count, io.PieceLength, io.Manager.Torrent.Size);
                _writeMonitor.AddDelta(io.Count);
            } finally {
                io.Complete = true;
                if (io.Callback != null)
                    io.Callback(true);
            }
        }

        private void PerformRead(BufferedIO io)
        {
            try
            {
                io.ActualCount = _writer.Read(io.Files, io.Offset, io.buffer, 0, io.Count,
                                             io.PieceLength,
                                             io.Manager.Torrent.Size)
                                     ? io.Count
                                     : 0;
                _readMonitor.AddDelta(io.ActualCount);
            }
            finally {
                io.Complete = true;
                if (io.Callback != null)
                    io.Callback(io.ActualCount == io.Count);
            }
        }

        internal void QueueFlush(TorrentManager manager, int index)
        {
            IOLoop.Queue(() =>
                             {
                                 try
                                 {
                                     foreach (
                                         var file in
                                             manager.Torrent.Files.Where(
                                                 file => file.StartPieceIndex >= index && file.EndPieceIndex <= index))
                                         _writer.Flush(file);
                                 }
                                 catch (Exception ex)
                                 {
                                     SetError(manager, Reason.WriteFailure, ex);
                                 }
                             });
        }

        internal void QueueRead(TorrentManager manager, long offset, byte[] buffer, int count, DiskIOCallback callback)
        {
            var io = _cache.Dequeue();
            io.Initialise(manager, buffer, offset, count, manager.Torrent.PieceLength, manager.Torrent.Files);
            QueueRead(io, callback);
        }

		void QueueRead(BufferedIO io, DiskIOCallback callback)
		{
			io.Callback = callback;
			if (Thread.CurrentThread == IOLoop.thread) {
				PerformRead(io);
				_cache.Enqueue (io);
			}
			else
				lock (_bufferLock)
				{
					_bufferedReads.Enqueue(io);
                    if (_bufferedReads.Count == 1)
                        IOLoop.Queue(_loopTask);
				}
		}

        internal void QueueWrite(TorrentManager manager, long offset, byte[] buffer, int count, DiskIOCallback callback)
        {
            var io = _cache.Dequeue();
            io.Initialise(manager, buffer, offset, count, manager.Torrent.PieceLength, manager.Torrent.Files);
            QueueWrite(io, callback);
        }

		void QueueWrite(BufferedIO io, DiskIOCallback callback)
		{
			io.Callback = callback;
			if (Thread.CurrentThread == IOLoop.thread) {
				PerformWrite(io);
				_cache.Enqueue (io);
			}
			else
				lock (_bufferLock)
				{
					_bufferedWrites.Enqueue(io);
                    if (_bufferedWrites.Count == 1)
                        IOLoop.Queue(_loopTask);
				}
		}

        internal bool CheckAnyFilesExist(TorrentManager manager)
        {
            var result = false;
            IOLoop.QueueWait(() =>
                                 {
                                     try
                                     {
                                         for (var i = 0; i < manager.Torrent.Files.Length && !result; i++)
                                             result = _writer.Exists(manager.Torrent.Files[i]);
                                     }
                                     catch (Exception ex)
                                     {
                                         SetError(manager, Reason.ReadFailure, ex);
                                     }
                                 });
            return result;
        }

        internal bool CheckFileExists(TorrentManager manager, TorrentFile file)
        {
            var result = false;
            IOLoop.QueueWait(() =>
                                 {
                                     try
                                     {
                                         result = _writer.Exists(file);
                                     }
                                     catch (Exception ex)
                                     {
                                         SetError(manager, Reason.ReadFailure, ex);
                                     }
                                 });
            return result;
        }

        static void SetError(TorrentManager manager, Reason reason, Exception ex)
        {
            ClientEngine.MainLoop.Queue(() =>
                                            {
                                                if (manager.Mode is ErrorMode)
                                                    return;

                                                manager.Error = new Error(reason, ex);
                                                manager.Mode = new ErrorMode(manager);
                                            });
        }

        internal void BeginGetHash(TorrentManager manager, int pieceIndex, MainLoopResult callback)
        {
            var count = 0;
            var offset = (long) manager.Torrent.PieceLength * pieceIndex;
            var endOffset = Math.Min(offset + manager.Torrent.PieceLength, manager.Torrent.Size);

            var hashBuffer = BufferManager.EmptyBuffer;
            ClientEngine.BufferManager.GetBuffer(ref hashBuffer, Piece.BlockSize);

            var hasher = HashAlgoFactory.Create<SHA1>();
            hasher.Initialize();

            DiskIOCallback readCallback = null;
            readCallback = successful =>
                               {
                                   if (successful)
                                       hasher.TransformBlock(hashBuffer, 0, count, hashBuffer, 0);
                                   offset += count;

                                   if (!successful || offset == endOffset)
                                   {
                                       object hash = null;
                                       if (successful)
                                       {
                                           hasher.TransformFinalBlock(hashBuffer, 0, 0);
                                           hash = hasher.Hash;
                                       }
                                       ((IDisposable) hasher).Dispose();
                                       ClientEngine.BufferManager.FreeBuffer(ref hashBuffer);
                                       ClientEngine.MainLoop.Queue(() => callback(hash));
                                   }
                                   else
                                   {
                                       count = (int) Math.Min(Piece.BlockSize, endOffset - offset);
                                       QueueRead(manager, offset, hashBuffer, count, readCallback);
                                   }
                               };

            count = (int)Math.Min(Piece.BlockSize, endOffset - offset);
            QueueRead(manager, offset, hashBuffer, count, readCallback);
        }

        #endregion

        internal void MoveFile(TorrentManager manager, TorrentFile file, string path)
        {
            IOLoop.QueueWait(() =>
                                 {
                                     try
                                     {
                                         path = Path.GetFullPath(path);
                                         _writer.Move(file.FullPath, path, false);
                                         file.FullPath = path;
                                     }
                                     catch (Exception ex)
                                     {
                                         SetError(manager, Reason.WriteFailure, ex);
                                     }
                                 });
        }

        internal void MoveFiles(TorrentManager manager, string newRoot, bool overWriteExisting)
        {
            IOLoop.QueueWait(() =>
                                 {
                                     try
                                     {
                                         _writer.Move(newRoot, manager.Torrent.Files, overWriteExisting);
                                     }
                                     catch (Exception ex)
                                     {
                                         SetError(manager, Reason.WriteFailure, ex);
                                     }
                                 });
        }
    }
}
