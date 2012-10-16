//
// BitField.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    ///   This class is for represting the Peer's bitfield
    /// </summary>
    public class BitField : ICloneable, IEnumerable<bool>
    {
        #region Member Variables

        private readonly int[] _array;
        private readonly int _length;

        internal bool AllFalse
        {
            get { return TrueCount == 0; }
        }

        internal bool AllTrue
        {
            get { return TrueCount == _length; }
        }

        public int Length
        {
            get { return _length; }
        }

        public double PercentComplete
        {
            get { return (double) TrueCount/_length*100.0; }
        }

        #endregion

        #region Constructors

        public BitField(byte[] array, int length)
            : this(length)
        {
            FromArray(array, 0, array.Length);
        }

        public BitField(int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException("length");

            this._length = length;
            _array = new int[(length + 31)/32];
        }

        public BitField(bool[] array)
        {
            _length = array.Length;
            this._array = new int[(array.Length + 31)/32];
            for (var i = 0; i < array.Length; i++)
                Set(i, array[i]);
        }

        #endregion

        #region Methods BitArray

        public bool this[int index]
        {
            get { return Get(index); }
            internal set { Set(index, value); }
        }

        public int LengthInBytes
        {
            get { return (_length + 7)/8; } //8 bits in a byte.
        }

        public int TrueCount { get; private set; }

        #region ICloneable Members

        object ICloneable.Clone()
        {
            return Clone();
        }

        #endregion

        #region IEnumerable<bool> Members

        public IEnumerator<bool> GetEnumerator()
        {
            for (var i = 0; i < _length; i++)
                yield return Get(i);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        public BitField Clone()
        {
            var b = new BitField(_length);
            Buffer.BlockCopy(_array, 0, b._array, 0, _array.Length*4);
            b.TrueCount = TrueCount;
            return b;
        }

        public BitField From(BitField value)
        {
            Check(value);
            Buffer.BlockCopy(value._array, 0, _array, 0, _array.Length*4);
            TrueCount = value.TrueCount;
            return this;
        }

        public BitField Not()
        {
            for (var i = 0; i < _array.Length; i++)
                _array[i] = ~_array[i];

            TrueCount = _length - TrueCount;
            return this;
        }

        public BitField And(BitField value)
        {
            Check(value);

            for (var i = 0; i < _array.Length; i++)
                _array[i] &= value._array[i];

            Validate();
            return this;
        }

        internal BitField NAnd(BitField value)
        {
            Check(value);

            for (var i = 0; i < _array.Length; i++)
                _array[i] &= ~value._array[i];

            Validate();
            return this;
        }

        public BitField Or(BitField value)
        {
            Check(value);

            for (var i = 0; i < _array.Length; i++)
                _array[i] |= value._array[i];

            Validate();
            return this;
        }

        public BitField Xor(BitField value)
        {
            Check(value);

            for (var i = 0; i < _array.Length; i++)
                _array[i] ^= value._array[i];

            Validate();
            return this;
        }

        public override bool Equals(object obj)
        {
            var bf = obj as BitField;

            if (bf == null || _array.Length != bf._array.Length || TrueCount != bf.TrueCount)
                return false;

            return !_array.Where((t, i) => t != bf._array[i]).Any();
        }

        public int FirstTrue()
        {
            return FirstTrue(0, _length);
        }

        public int FirstTrue(int startIndex, int endIndex)
        {
            // If the number of pieces is an exact multiple of 32, we need to decrement by 1 so we don't overrun the array
            // For the case when endIndex == 0, we need to ensure we don't go negative
            var loopEnd = Math.Min((endIndex/32), _array.Length - 1);
            for (var i = (startIndex/32); i <= loopEnd; i++)
            {
                if (_array[i] == 0) // This one has no true values
                    continue;

                var start = i*32;
                var end = start + 32;
                start = (start < startIndex) ? startIndex : start;
                end = (end > _length) ? _length : end;
                end = (end > endIndex) ? endIndex : end;
                if (end == Length && end > 0)
                    end--;

                for (var j = start; j <= end; j++)
                    if (Get(j)) // This piece is true
                        return j;
            }

            return -1; // Nothing is true
        }

        public int FirstFalse()
        {
            return FirstFalse(0, Length);
        }

        public int FirstFalse(int startIndex, int endIndex)
        {
            // If the number of pieces is an exact multiple of 32, we need to decrement by 1 so we don't overrun the array
            // For the case when endIndex == 0, we need to ensure we don't go negative
            var loopEnd = Math.Min((endIndex/32), _array.Length - 1);
            for (var i = (startIndex/32); i <= loopEnd; i++)
            {
                if (_array[i] == ~0) // This one has no false values
                    continue;

                var start = i*32;
                var end = start + 32;
                start = (start < startIndex) ? startIndex : start;
                end = (end > _length) ? _length : end;
                end = (end > endIndex) ? endIndex : end;
                if (end == Length && end > 0)
                    end--;

                for (var j = start; j <= end; j++)
                    if (!Get(j)) // This piece is true
                        return j;
            }

            return -1; // Nothing is true
        }

        internal void FromArray(byte[] buffer, int offset, int length)
        {
            var end = Length/32;
            for (var i = 0; i < end; i++)
                _array[i] = (buffer[offset++] << 24) |
                           (buffer[offset++] << 16) |
                           (buffer[offset++] << 8) |
                           (buffer[offset++] << 0);

            var shift = 24;
            for (var i = end*32; i < Length; i += 8)
            {
                _array[_array.Length - 1] |= buffer[offset++] << shift;
                shift -= 8;
            }
            Validate();
        }

        private bool Get(int index)
        {
            if (index < 0 || index >= _length)
                throw new ArgumentOutOfRangeException("index");

            return (_array[index >> 5] & (1 << (31 - (index & 31)))) != 0;
        }

        public override int GetHashCode()
        {
            return _array.Sum();
        }

        public BitField Set(int index, bool value)
        {
            if (index < 0 || index >= _length)
                throw new ArgumentOutOfRangeException("index");

            if (value)
            {
                if ((_array[index >> 5] & (1 << (31 - (index & 31)))) == 0) // If it's not already true
                    TrueCount++; // Increase true count
                _array[index >> 5] |= (1 << (31 - index & 31));
            }
            else
            {
                if ((_array[index >> 5] & (1 << (31 - (index & 31)))) != 0) // If it's not already false
                    TrueCount--; // Decrease true count
                _array[index >> 5] &= ~(1 << (31 - (index & 31)));
            }

            return this;
        }

        internal BitField SetTrue(params int[] indices)
        {
            foreach (var index in indices)
                Set(index, true);
            return this;
        }

        internal BitField SetFalse(params int[] indices)
        {
            foreach (var index in indices)
                Set(index, false);
            return this;
        }

        internal BitField SetAll(bool value)
        {
            if (value)
            {
                for (var i = 0; i < _array.Length; i++)
                    _array[i] = ~0;
                Validate();
            }

            else
            {
                for (var i = 0; i < _array.Length; i++)
                    _array[i] = 0;
                TrueCount = 0;
            }

            return this;
        }

        internal byte[] ToByteArray()
        {
            var data = new byte[LengthInBytes];
            ToByteArray(data, 0);
            return data;
        }

        internal void ToByteArray(byte[] buffer, int offset)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            ZeroUnusedBits();
            var end = Length/32;
            for (var i = 0; i < end; i++)
            {
                buffer[offset++] = (byte) (_array[i] >> 24);
                buffer[offset++] = (byte) (_array[i] >> 16);
                buffer[offset++] = (byte) (_array[i] >> 8);
                buffer[offset++] = (byte) (_array[i] >> 0);
            }

            var shift = 24;
            for (var i = end*32; i < Length; i += 8)
            {
                buffer[offset++] = (byte) (_array[_array.Length - 1] >> shift);
                shift -= 8;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder(_array.Length*16);
            for (var i = 0; i < Length; i++)
            {
                sb.Append(Get(i) ? 'T' : 'F');
                sb.Append(' ');
            }

            return sb.ToString(0, sb.Length - 1);
        }

        private void Validate()
        {
            ZeroUnusedBits();

            // Update the population count
            uint count = 0;
            for (var i = 0; i < _array.Length; i++)
            {
                var v = (uint) _array[i];
                v = v - ((v >> 1) & 0x55555555);
                v = (v & 0x33333333) + ((v >> 2) & 0x33333333);
                count += (((v + (v >> 4) & 0xF0F0F0F)*0x1010101)) >> 24;
            }
            TrueCount = (int) count;
        }

        private void ZeroUnusedBits()
        {
            if (_array.Length == 0)
                return;

            // Zero the unused bits
            var shift = 32 - _length%32;
            if (shift != 0)
                _array[_array.Length - 1] &= (-1 << shift);
        }

        private void Check(BitField value)
        {
            OctoTorrent.Check.Value(value);
            if (_length != value._length)
                throw new ArgumentException("BitFields are of different lengths", "value");
        }

        #endregion
    }
}