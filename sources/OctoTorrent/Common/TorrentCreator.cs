//
// TorrentCreator.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006-2007 Gregor Burger and Alan McGovern
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

namespace OctoTorrent.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;
    using BEncoding;
    using OctoTorrent.Client;
    using OctoTorrent.Client.PieceWriters;

    public class TorrentCreator : EditableTorrent
    {
        public event EventHandler<TorrentCreatorEventArgs> Hashed;

        private readonly List<string> _getrightHttpSeeds;
        private TorrentCreatorAsyncResult _asyncResult;

        public TorrentCreator()
        {
            _getrightHttpSeeds = new List<string>();
            CanEditSecureMetadata = true;
            CreatedBy = string.Format(VersionInfo.HumanReadableId);
        }

        public IEnumerable<string> GetrightHttpSeeds
        {
            get { return _getrightHttpSeeds; }
        }

        public bool StoreMD5 { get; set; }

        public static int RecommendedPieceSize(long totalSize)
        {
            // Check all piece sizes that are multiples of 32kB and
            // choose the smallest piece size which results in a
            // .torrent file smaller than 60kb
            for (var i = 32768; i < 4*1024*1024; i *= 2)
            {
                var pieces = (int) (totalSize/i) + 1;
                if ((pieces*20) < (60*1024))
                    return i;
            }

            // If we get here, we're hashing a massive file, so lets limit
            // to a max of 4MB pieces.
            return 4*1024*1024;
        }

        public static int RecommendedPieceSize(IEnumerable<string> files)
        {
            var total = files.Sum(x => new FileInfo(x).Length);
            return RecommendedPieceSize(total);
        }

        public static int RecommendedPieceSize(IEnumerable<TorrentFile> files)
        {
            var total = files.Sum(x => x.Length);
            return RecommendedPieceSize(total);
        }

        public static int RecommendedPieceSize(IEnumerable<FileMapping> files)
        {
            var total = files.Sum(x => new FileInfo(x.Source).Length);
            return RecommendedPieceSize(total);
        }

        public void AbortCreation()
        {
            if (_asyncResult != null)
                _asyncResult.Aborted = true;
        }

        private void AddCommonStuff(BEncodedDictionary torrent)
        {
            if (Announces.Count > 0 && Announces[0].Count > 0)
                Announce = Announces[0][0];

            if (_getrightHttpSeeds.Count > 0)
            {
                var seedlist = new BEncodedList();
                seedlist.AddRange(_getrightHttpSeeds.ConvertAll<BEncodedValue>(s => (BEncodedString) s));
                torrent["url-list"] = seedlist;
            }

            var span = DateTime.Now - new DateTime(1970, 1, 1);
            torrent["creation date"] = new BEncodedNumber((long) span.TotalSeconds);
        }

        public IAsyncResult BeginCreate(ITorrentFileSource fileSource, AsyncCallback callback, object asyncState)
        {
            return BeginCreate(() => Create(fileSource), callback, asyncState);
        }

        private IAsyncResult BeginCreate(MainLoopJob task, AsyncCallback callback, object asyncState)
        {
            if (_asyncResult != null)
                throw new InvalidOperationException("Two asynchronous operations cannot be executed simultaenously");

            _asyncResult = new TorrentCreatorAsyncResult(callback, asyncState);
            ThreadPool.QueueUserWorkItem(delegate
                                             {
                                                 try
                                                 {
                                                     _asyncResult.Dictionary = (BEncodedDictionary) task();
                                                 }
                                                 catch (Exception ex)
                                                 {
                                                     _asyncResult.SavedException = ex;
                                                 }
                                                 _asyncResult.Complete();
                                             });
            return _asyncResult;
        }

        private byte[] CalcPiecesHash(List<TorrentFile> files, PieceWriter writer)
        {
            var bufferRead = 0;
            long overallRead = 0;
            MD5 md5Hasher = null;

            var shaHasher = HashAlgoFactory.Create<SHA1>();
            var torrentHashes = new List<byte>();
            var overallTotal = files.Sum(x => x.Length);

            var pieceLength = PieceLength;
            var buffer = new byte[pieceLength];

            if (StoreMD5)
                md5Hasher = HashAlgoFactory.Create<MD5>();

            try
            {
                foreach (var file in files)
                {
                    long fileRead = 0;
                    if (md5Hasher != null)
                        md5Hasher.Initialize();

                    while (fileRead < file.Length)
                    {
                        var toRead = (int) Math.Min(buffer.Length - bufferRead, file.Length - fileRead);
                        var read = writer.Read(file, fileRead, buffer, bufferRead, toRead);

                        if (md5Hasher != null)
                            md5Hasher.TransformBlock(buffer, bufferRead, read, buffer, bufferRead);
                        shaHasher.TransformBlock(buffer, bufferRead, read, buffer, bufferRead);

                        bufferRead += read;
                        fileRead += read;
                        overallRead += read;

                        if (bufferRead == buffer.Length)
                        {
                            bufferRead = 0;
                            shaHasher.TransformFinalBlock(buffer, 0, 0);
                            torrentHashes.AddRange(shaHasher.Hash);
                            shaHasher.Initialize();
                        }
                        RaiseHashed(new TorrentCreatorEventArgs(file.Path, fileRead, file.Length, overallRead,
                                                                overallTotal));
                    }

                    if (md5Hasher == null) 
                        continue;

                    md5Hasher.TransformFinalBlock(buffer, 0, 0);
                    md5Hasher.Initialize();
                    file.MD5 = md5Hasher.Hash;
                }
                if (bufferRead > 0)
                {
                    shaHasher.TransformFinalBlock(buffer, 0, 0);
                    torrentHashes.AddRange(shaHasher.Hash);
                }
            }
            finally
            {
                if (shaHasher != null)
                    shaHasher.Clear();
                if (md5Hasher != null)
                    md5Hasher.Clear();
            }
            return torrentHashes.ToArray();
        }

        public BEncodedDictionary Create(ITorrentFileSource fileSource)
        {
            Check.FileSource(fileSource);

            var mappings = new List<FileMapping>(fileSource.Files);
            if (mappings.Count == 0)
                throw new ArgumentException("The file source must contain one or more files", "fileSource");

            mappings.Sort((left, right) => string.Compare(left.Destination, right.Destination, System.StringComparison.Ordinal));
            Validate(mappings);

            var maps = fileSource.Files
                .Select(ToTorrentFile)
                .ToList();
            return Create(fileSource.TorrentName, maps);
        }

        public void Create(ITorrentFileSource fileSource, Stream stream)
        {
            Check.Stream(stream);

            var data = Create(fileSource).Encode();
            stream.Write(data, 0, data.Length);
        }

        public void Create(ITorrentFileSource fileSource, string savePath)
        {
            Check.SavePath(savePath);

            File.WriteAllBytes(savePath, Create(fileSource).Encode());
        }

        internal BEncodedDictionary Create(string name, List<TorrentFile> files)
        {
            if (PieceLength == 0)
                PieceLength = RecommendedPieceSize(files);

            var torrent = BEncodedValue.Clone(Metadata);
            var info = (BEncodedDictionary) torrent["info"];

            info["name"] = (BEncodedString) name;
            AddCommonStuff(torrent);

            using (var reader = CreateReader())
            {
                info["pieces"] = (BEncodedString) CalcPiecesHash(files, reader);

                if (files.Count == 1 && files[0].Path == name)
                    CreateSingleFileTorrent(torrent, files, reader, name);
                else
                    CreateMultiFileTorrent(torrent, files, reader, name);
            }

            return torrent;
        }

        protected virtual PieceWriter CreateReader()
        {
            return new DiskWriter();
        }

        public BEncodedDictionary EndCreate(IAsyncResult result)
        {
            Check.Result(result);

            if (result != _asyncResult)
                throw new ArgumentException("The supplied async result does not correspond to currently active async result");

            try
            {
                if (!result.IsCompleted)
                    result.AsyncWaitHandle.WaitOne();

                if (_asyncResult.SavedException != null)
                    throw _asyncResult.SavedException;

                return _asyncResult.Aborted ? null : _asyncResult.Dictionary;
            }
            finally
            {
                _asyncResult = null;
            }
        }

        public void EndCreate(IAsyncResult result, string path)
        {
            Check.PathNotEmpty(path);

            var dict = EndCreate(result);
            File.WriteAllBytes(path, dict.Encode());
        }

        public void EndCreate(IAsyncResult result, Stream stream)
        {
            Check.Stream(stream);

            var buffer = EndCreate(result).Encode();
            stream.Write(buffer, 0, buffer.Length);
        }

        private void RaiseHashed(TorrentCreatorEventArgs e)
        {
            Toolbox.RaiseAsyncEvent(Hashed, this, e);
        }

        private static void CreateSingleFileTorrent(BEncodedDictionary dictionary, List<TorrentFile> mappings,
                                                    PieceWriter writer, string name)
        {
            var infoDict = (BEncodedDictionary) dictionary["info"];
            infoDict.Add("length", new BEncodedNumber(mappings[0].Length));
            if (mappings[0].MD5 != null)
                infoDict["md5sum"] = (BEncodedString) mappings[0].MD5;
        }

        private static void CreateMultiFileTorrent(BEncodedDictionary dictionary, List<TorrentFile> mappings,
                                                   PieceWriter writer, string name)
        {
            var info = (BEncodedDictionary) dictionary["info"];
            var files = mappings.ConvertAll(ToFileInfoDict);
            info.Add("files", new BEncodedList(files));
        }

        private static TorrentFile ToTorrentFile(FileMapping mapping)
        {
            var info = new FileInfo(mapping.Source);
            return new TorrentFile(mapping.Destination, info.Length, mapping.Source);
        }

        private static BEncodedValue ToFileInfoDict(TorrentFile file)
        {
            var fileDict = new BEncodedDictionary();

            var filePath = new BEncodedList();
            var splittetPath = file.Path.Split(new[] {Path.DirectorySeparatorChar},
                                               StringSplitOptions.RemoveEmptyEntries);
            foreach (var s in splittetPath)
                filePath.Add(new BEncodedString(s));

            fileDict["length"] = new BEncodedNumber(file.Length);
            fileDict["path"] = filePath;
            if (file.MD5 != null)
                fileDict["md5sum"] = (BEncodedString) file.MD5;

            return fileDict;
        }

        private static void Validate(IList<FileMapping> maps)
        {
            // Make sure the user doesn't try to overwrite system files. Ensure
            // that the path is relative and doesn't try to access its parent folder
            const string sepLinux = "/";
            const string sepWindows = "\\";
            const string dropLinux = "../";
            const string dropWindows = "..\\";

            foreach (var map in maps)
            {
                if (map.Destination.StartsWith(sepLinux))
                    throw new ArgumentException("The destination path cannot start with the '{0}' character", sepLinux);
                if (map.Destination.StartsWith(sepWindows))
                    throw new ArgumentException("The destination path cannot start with the '{0}' character", sepWindows);

                if (map.Destination.Contains(dropLinux))
                    throw new ArgumentException("The destination path cannot contain '{0}'", dropLinux);
                if (map.Destination.Contains(dropWindows))
                    throw new ArgumentException("The destination path cannot contain '{0}'", dropWindows);
            }

            // Ensure all the destination files are unique too. The files should already be sorted.
            for (var i = 1; i < maps.Count; i++)
                if (maps[i - 1].Destination == maps[i].Destination)
                    throw new ArgumentException(
                        string.Format("Files '{0}' and '{1}' both map to the same destination '{2}'",
                                      maps[i - 1].Source,
                                      maps[i].Source,
                                      maps[i].Destination));
        }
    }
}