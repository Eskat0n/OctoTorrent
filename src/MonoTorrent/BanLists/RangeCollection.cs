//
// RangeCollection.cs
//
// Author:
//   Alan McGovern <alan.mcgovern@gmail.com>
//
// Copyright (C) 2009 Alan McGovern.
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

namespace OctoTorrent.Client
{
    using System;
    using System.Collections.Generic;

    public struct RangeComparer : IComparer<AddressRange>
    {
        public int Compare(AddressRange x, AddressRange y)
        {
            return x.Start.CompareTo(y.Start);
        }
    }

    public class RangeCollection
    {
        private readonly List<AddressRange> _ranges = new List<AddressRange>();

        public int Count
        {
            get { return _ranges.Count; }
        }

        internal List<AddressRange> Ranges
        {
            get { return _ranges; }
        }

        public void Add(AddressRange item)
        {
            int index;
            if (_ranges.Count == 0 || item.Start > _ranges[_ranges.Count - 1].Start)
            {
                index = _ranges.Count;
            }
            else
            {
                index = _ranges.BinarySearch(item, new RangeComparer());
                if (index < 0)
                    index = ~index;
            }
            var mergedLeft = MergeLeft(item, index);
            var mergedRight = MergeRight(item, index);

            if (mergedLeft || mergedRight)
            {
                if (index > 0)
                    index--;

                while ((index + 1) < _ranges.Count)
                {
                    if (_ranges[index].End <= _ranges[index + 1].Start &&
                        _ranges[index].End + 1 != _ranges[index + 1].Start)
                        break;

                    _ranges[index] = new AddressRange(_ranges[index].Start,
                                                      Math.Max(_ranges[index].End, _ranges[index + 1].End));
                    _ranges.RemoveAt(index + 1);
                }
            }
            else
                _ranges.Insert(index, item);
        }

        public void AddRange(IEnumerable<AddressRange> ranges)
        {
            var list = new List<AddressRange>(ranges);
            list.Sort((x, y) => x.Start.CompareTo(y.Start));

            foreach (var r in list)
                Add(new AddressRange(r.Start, r.End));
        }

        private bool MergeLeft(AddressRange range, int position)
        {
            if (position > 0)
                position--;

            if (_ranges.Count > position && position >= 0)
            {
                var leftRange = _ranges[position];
                if (leftRange.Contains(range.Start))
                {
                    _ranges[position] = new AddressRange(leftRange.Start, Math.Max(leftRange.End, range.End));
                    return true;
                }
                if (leftRange.End + 1 == range.Start)
                {
                    _ranges[position] = new AddressRange(leftRange.Start, range.End);
                    return true;
                }
                if (leftRange.Start - 1 == range.End)
                {
                    _ranges[position] = new AddressRange(range.Start, leftRange.End);
                    return true;
                }
            }
            return false;
        }

        private bool MergeRight(AddressRange range, int position)
        {
            if (position == _ranges.Count)
                position--;
            if (position >= 0 && position < _ranges.Count)
            {
                var rightRange = _ranges[position];
                if (rightRange.Contains(range.End))
                {
                    _ranges[position] = new AddressRange(Math.Min(range.Start, rightRange.Start), rightRange.End);
                    return true;
                }
                if (range.Contains(rightRange))
                {
                    _ranges[position] = range;
                    return true;
                }
                if (rightRange.Contains(range.Start))
                {
                    _ranges[position] = new AddressRange(rightRange.Start, Math.Max(range.End, rightRange.End));
                    return true;
                }
            }
            return false;
        }

        internal bool Contains(AddressRange range)
        {
            var index = _ranges.BinarySearch(range, new RangeComparer());
            
            // The start of this range is smaller than the start of any range in the list
            if (index == -1)
                return false;

            // An element in the collection has the same 'Start' as 'range' 
            if (index >= 0)
                return range.End <= _ranges[index].End;

            index = ~index;
            var r = _ranges[index - 1];
            return r.Contains(range);
        }

        internal void Remove(AddressRange item)
        {
            if (_ranges.Count == 0)
                return;

            for (var i = item.Start; i <= item.End; i++)
            {
                var addressRange = new AddressRange(i, i);
                var index = _ranges.BinarySearch(addressRange, new RangeComparer());
                if (index < 0)
                {
                    index = Math.Max((~index) - 1, 0);

                    var range = _ranges[index];
                    if (addressRange.Start < range.Start || addressRange.Start > range.End)
                        continue;

                    if (addressRange.Start == range.Start)
                    {
                        _ranges[index] = new AddressRange(range.Start + 1, range.End);
                    }
                    else if (addressRange.End == range.End)
                    {
                        _ranges[index] = new AddressRange(range.Start, range.End - 1);
                    }
                    else
                    {
                        _ranges[index] = new AddressRange(range.Start, addressRange.Start - 1);
                        _ranges.Insert(index + 1, new AddressRange(addressRange.Start + 1, range.End));
                    }
                }
                else
                {
                    var range = _ranges[index];
                    if (range.Contains(addressRange))
                    {
                        if (range.Start == range.End)
                            _ranges.RemoveAt(index);
                        else
                            _ranges[index] = new AddressRange(range.Start + 1, range.End);
                    }
                }
            }
        }

        internal void Validate()
        {
            for (var i = 1; i < _ranges.Count; i++)
            {
                var left = _ranges[i - 1];
                var right = _ranges[i];

                if (left.Start > left.End)
                    throw new Exception();
                if (left.End >= right.Start)
                    throw new Exception();
            }
        }
    }
}
