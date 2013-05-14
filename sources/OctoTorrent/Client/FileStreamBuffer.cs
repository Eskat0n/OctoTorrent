using System;
using System.Collections.Generic;
using OctoTorrent.Common;
using System.IO;

namespace OctoTorrent.Client
{
    using System.Linq;

    class FileStreamBuffer : IDisposable
    {
        // A list of currently open filestreams. Note: The least recently used is at position 0
        // The most recently used is at the last position in the array
        private readonly List<TorrentFileStream> _list;
        private readonly int _maxStreams;
		
		public int Count
		{
			get { return _list.Count; }
		}
		
		public List<TorrentFileStream> Streams
		{
			get { return _list; }
		}

        public FileStreamBuffer(int maxStreams)
        {
            this._maxStreams = maxStreams;
            _list = new List<TorrentFileStream>(maxStreams);
        }

        private void Add(TorrentFileStream stream)
        {
            Logger.Log (null, "Opening filestream: {0}", stream.Path);

            // If we have our maximum number of streams open, just dispose and dump the least recently used one
            if (_maxStreams != 0 && _list.Count >= _list.Capacity)
            {
                Logger.Log (null, "We've reached capacity: {0}", _list.Count);
                CloseAndRemove(_list[0]);
            }
            _list.Add(stream);
        }

        public TorrentFileStream FindStream(string path)
        {
            return _list.FirstOrDefault(stream => stream.Path == path);
        }

        internal TorrentFileStream GetStream(TorrentFile file, FileAccess access)
        {
            var fileStream = FindStream(file.FullPath);

            if (fileStream != null)
            {
                // If we are requesting write access and the current stream does not have it
                if (((access & FileAccess.Write) == FileAccess.Write) && !fileStream.CanWrite)
                {
                    Logger.Log (null, "Didn't have write permission - reopening");
                    CloseAndRemove(fileStream);
                    fileStream = null;
                }
                else
                {
                    // Place the filestream at the end so we know it's been recently used
                    _list.Remove(fileStream);
                    _list.Add(fileStream);
                }
            }

            if (fileStream == null)
            {
                if (File.Exists(file.FullPath) == false)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file.FullPath));
                    SparseFile.CreateSparse(file.FullPath, file.Length);
                }
                fileStream = new TorrentFileStream (file, FileMode.OpenOrCreate, access, FileShare.Read);

                // Ensure that we truncate existing files which are too large
                if (fileStream.Length > file.Length) {
                    if (!fileStream.CanWrite) {
                        fileStream.Close();
                        fileStream = new TorrentFileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                    }
                    fileStream.SetLength(file.Length);
                }

                Add(fileStream);
            }

            return fileStream;
        }

        #region IDisposable Members

        public void Dispose()
        {
            _list.ForEach(delegate (TorrentFileStream s) { s.Dispose(); }); 
            _list.Clear();
        }

        #endregion

        internal bool CloseStream(string path)
        {
            TorrentFileStream s = FindStream(path);
            if (s != null)
                CloseAndRemove(s);

            return s != null;
        }

        private void CloseAndRemove(TorrentFileStream s)
        {
            Logger.Log (null, "Closing and removing: {0}", s.Path);
            _list.Remove(s);
            s.Dispose();
        }
    }
}
