namespace MonoTorrent.Common
{
    using System;
    using System.Collections.Generic;

    public class MonoTorrentCollection<T> : List<T>, ICloneable
    {
        public MonoTorrentCollection()
        {
        }

        public MonoTorrentCollection(IEnumerable<T> collection)
            : base(collection)
        {
        }

        public MonoTorrentCollection(int capacity)
            : base(capacity)
        {
        }

        #region ICloneable Members

        object ICloneable.Clone()
        {
            return Clone();
        }

        #endregion

        private MonoTorrentCollection<T> Clone()
        {
            return new MonoTorrentCollection<T>(this);
        }

        public T Dequeue()
        {
            var result = this[0];
            RemoveAt(0);
            return result;
        }
    }
}