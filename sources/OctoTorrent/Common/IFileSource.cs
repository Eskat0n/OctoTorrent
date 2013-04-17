using System;
using System.Collections.Generic;
using System.Text;

namespace OctoTorrent.Common
{
    public interface ITorrentFileSource
    {
        IEnumerable<FileMapping> Files { get; }
        string TorrentName { get; }
    }
}
