//
// BitFieldTest.cs
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

using System;
using NUnit.Framework;
using OctoTorrent.Client;
using System.Collections.Generic;
using System.Linq;

namespace OctoTorrent.Common
{
    [TestFixture]
    public class BitFieldTest
    {
        private BitField _bf;
        private bool[] _initalValues;
        private byte[] _initialByteValues;
        private bool[] _secondValues;

        [SetUp]
        public void SetUp()
        {
            // The bool[] must be kept in sync with the byte[] constructor. They represent exactly the same thing.
            _initalValues = new[] { true, false, true, false, true, false, true, true, true, false, false, true };
            _secondValues = new[] { true, true, false, false, true, false, true, false, true, false, false, true };
            _initialByteValues = new byte[] { 171, 144 };
            _bf = new BitField(_initalValues);
        }

        [Test]
        public void ConstructorIntTest()
        {
            var bf2 = new BitField(_initialByteValues, _initalValues.Length);
            Assert.AreEqual(_bf, bf2, "#1");
            Assert.AreEqual(_initalValues.Count(x => x), bf2.TrueCount, "#1");
        }

        [Test]
        public void ConstructorBoolTest()
        {
            for (var i = 0; i < _initalValues.Length; i++)
                Assert.AreEqual(_initalValues[i], _bf[i], "#1:{0}", i);

            Assert.AreEqual(_initalValues.Count(x => x), _bf.TrueCount, "#1");
        }

        [Ignore("This is deliberately broken to work around bugs in azureus")]
        public void InvalidBitfieldTest()
        {
            // Set each of the 4 trailing bits to 1 to force a decode error
            for (byte i = 8; i > 0; i /= 2)
            {
                try
                {
                    _initialByteValues[1] += i;
                    _bf = new BitField(_initialByteValues, _initalValues.Length);
                    Assert.Fail("The bitfield was corrupt but decoded correctly: Loop {0}", i);
                }
                catch (MessageException) { _initialByteValues[1] -= i; }
            }
        }

        [Test]
        public void FirstTrue()
        {
            Assert.AreEqual(0, _bf.FirstTrue(0, _bf.Length));
            Assert.AreEqual(0, _bf.FirstTrue(0, 0));
            Assert.AreEqual(-1, _bf.FirstTrue(_bf.Length, _bf.Length));
            Assert.AreEqual(11, _bf.FirstTrue(_bf.Length - 1, _bf.Length - 1));
            Assert.AreEqual(11, _bf.FirstTrue(_bf.Length - 1, _bf.Length));
            Assert.AreEqual(11, _bf.FirstTrue(9, _bf.Length));
        }

        [Test]
        public void FirstTrue2()
        {
            var b = new BitField(1025);
            b[1024] = true;
            Assert.AreEqual(1024, b.FirstTrue(0, 1025));
        }

        [Test]
        public void LongByteArrayTest()
        {
            var list = new List<byte>(_initialByteValues)
                           {
                               byte.MaxValue,
                               byte.MaxValue,
                               byte.MaxValue,
                               byte.MaxValue,
                               byte.MaxValue,
                               byte.MaxValue,
                               byte.MaxValue,
                               byte.MaxValue
                           };

            var b = new BitField(list.ToArray(), _initalValues.Length);

            Assert.AreEqual(b, _bf, "#1");
        }

        [Test]
        public void ToByteArray()
        {
            var first = new BitField(new[] { true, false, true, false, true, false, true, true, true, false, false });
            var second = new BitField(first.ToByteArray(), first.Length);

            for (var i = 0; i < first.Length; i++)
                Assert.AreEqual(first[i], second[i], "#" + i);
        }

        [Test]
        public void ToByteArray2()
        {
            var first = new BitField(new[] { true, false, true, false, true, false, true, true, true, false, false, true });
            var second = new BitField(first.ToByteArray(), first.Length);

            for (var i = 0; i < first.Length; i++)
                Assert.AreEqual(first[i], second[i], "#" + i);
        }

        [Test]
        public void ToByteArray3()
        {
            var first = new BitField(new[] { true, false, true, false, true, false, true, true, true, false, false, true, false });
            var second = new BitField(first.ToByteArray(), first.Length);

            for (var i = 0; i < first.Length; i++)
                Assert.AreEqual(first[i], second[i], "#" + i);
        }

        [Test]
        public void ToByteArray4()
        {
            var first = new BitField(new[]
                                         {
                                             true, false, true, false, true, false, true, false,
                                             false, false, true, false, true, false, false, false,
                                             true, false, false, false, true, true, true, false,
                                             true, false, false, true, false, false, true, false
                                         });
            var second = new BitField(first.ToByteArray(), first.Length);

            for (var i = 0; i < first.Length; i++)
                Assert.AreEqual(first[i], second[i], "#" + i);
        }

        [Test]
        public void ToByteArray5()
        {
            var first = new BitField(new[]
                                         {
                                             true, false, true, false, true, false, true, false,
                                             false, false, true, false, true, false, false, false,
                                             true, false, false, false, true, true, true, false,
                                             true, false, false, true, false, false, true
                                         });
            var second = new BitField(first.ToByteArray(), first.Length);

            for (var i = 0; i < first.Length; i++)
                Assert.AreEqual(first[i], second[i], "#" + i);
        }

        [Test]
        public void ToByteArray6()
        {
            var first = new BitField(new[]
                                         {
                                             true, false, true, false, true, false, true, false, true,
                                             false, false, true, false, true, false, true, false,
                                             true, false, false, false, true, true, true, false, true,
                                             true, false, false, true, false, false, true
                                         });
            var second = new BitField(first.ToByteArray(), first.Length);

            for (var i = 0; i < first.Length; i++)
                Assert.AreEqual(first[i], second[i], "#" + i);
        }


        [Test]
        public void Clone()
        {
            var clone = _bf.Clone();
            Assert.AreEqual(_bf, clone);
        }

        [Test]
        public void LargeBitfield()
        {
            var bf = new BitField(1000);
            bf.SetAll(true);
            Assert.AreEqual(1000, bf.TrueCount);
        }

        [Test]
        public void Length()
        {
            Assert.AreEqual(_initalValues.Length, _bf.Length);
        }

        [Test]
        public void LengthInBytes()
        {
            Assert.AreEqual(1, new BitField(1).LengthInBytes, "#1");
            Assert.AreEqual(1, new BitField(8).LengthInBytes, "#2");
            Assert.AreEqual(2, new BitField(9).LengthInBytes, "#3");
            Assert.AreEqual(2, new BitField(15).LengthInBytes, "#4");
            Assert.AreEqual(2, new BitField(16).LengthInBytes, "#5");
            Assert.AreEqual(3, new BitField(17).LengthInBytes, "#6");
        }

        [Test]
        public void And()
        {
            var bf2 = new BitField(_secondValues);
            _bf.And(bf2);

            Assert.AreEqual(new BitField(_secondValues), bf2, "#1: bf2 should be unmodified");
            for (var i = 0; i < _bf.Length; i++)
                Assert.AreEqual(_initalValues[i] && _secondValues[i], _bf[i], "#2");

            var count = _initalValues
                .Where((x, i) => x && _secondValues[i])
                .Count();

            Assert.AreEqual(count, _bf.TrueCount, "#3");
        }

        [Test]
        public void And2()
        {
            var random = new Random ();
            var a = new byte [100];
            var b = new byte [100];

            random.NextBytes (a);
            random.NextBytes (b);

            for (var i = 0; i < a.Length * 8; i++) {
                var first = new BitField (a, i);
                var second = new BitField (b, i);

                first.And(second);
            }
        }

        [Test]
        public void Or()
        {
            var bf2 = new BitField(_secondValues);
            _bf.Or(bf2);

            Assert.AreEqual(new BitField(_secondValues), bf2, "#1: bf2 should be unmodified");
            for (var i = 0; i < _bf.Length; i++)
                Assert.AreEqual(_initalValues[i] || _secondValues[i], _bf[i], "#2");

            var count = _initalValues
                .Where((x, i) => x || _secondValues[i])
                .Count();

            Assert.AreEqual(count, _bf.TrueCount, "#3");
        }

        [Test]
        public void Not()
        {
            _bf.Not();
            for (var i = 0; i < _bf.Length; i++)
                Assert.AreEqual(!_initalValues[i], _bf[i], "#1");

            Assert.AreEqual(_initalValues.Count(b => !b), _bf.TrueCount, "#2");
        }

        [Test]
        public void Xor()
        {
            var bf2 = new BitField(_secondValues);
            _bf.Xor(bf2);

            Assert.AreEqual(new BitField(_secondValues), bf2, "#1: bf2 should be unmodified");
            for (var i = 0; i < _bf.Length; i++)
                Assert.AreEqual((_initalValues[i] || _secondValues[i]) && !(_initalValues[i] && _secondValues[i]), _bf[i], "#2");

            var count = _initalValues
                .Where((x, i) => (x || _secondValues[i]) && !(x && _secondValues[i]))
                .Count();

            Assert.AreEqual(count, _bf.TrueCount, "#3");
        }

        [Test]
        public void From()
        {
            var b = new BitField(31);
            b.SetAll(true);
            Assert.AreEqual(31, b.TrueCount, "#1");
            Assert.IsTrue(b.AllTrue, "#1b");

            b = new BitField(32);
            b.SetAll(true);
            Assert.AreEqual(32, b.TrueCount, "#2");
            Assert.IsTrue(b.AllTrue, "#2b");

            b = new BitField(33);
            b.SetAll(true);
            Assert.AreEqual(33, b.TrueCount, "#3");
            Assert.IsTrue(b.AllTrue, "#3b");
        }
    }
}