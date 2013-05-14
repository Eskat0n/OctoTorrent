//
// BEncodingTest.cs
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

namespace OctoTorrent.Tests.Common
{
    using System.IO;
    using NUnit.Framework;
    using System.Text;
    using BEncoding;
    using OctoTorrent.Common;

    [TestFixture]
    public class BEncodeTest
    {
        #region Text encoding tests

        [Test]
        public void Utf8Test()
        {
            const string expected = "ã";
            BEncodedString actual = expected;

            Assert.AreEqual(expected, actual.Text);
        }

        //[Test]
        //public void EncodingUTF32()
        //{
        //    UTF8Encoding enc8 = new UTF8Encoding();
        //    UTF32Encoding enc32 = new UTF32Encoding();
        //    BEncodedDictionary val = new BEncodedDictionary();

        //    val.Add("Test", (BEncodedNumber)1532);
        //    val.Add("yeah", (BEncodedString)"whoop");
        //    val.Add("mylist", new BEncodedList());
        //    val.Add("mydict", new BEncodedDictionary());

        //    byte[] utf8Result = val.Encode();
        //    byte[] utf32Result = val.Encode(enc32);

        //    Assert.AreEqual(enc8.GetString(utf8Result), enc32.GetString(utf32Result));
        //}
        #endregion

        #region BEncodedString Tests
        [Test]
        public void benStringDecoding()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("21:this is a test string");
            using (MemoryStream stream = new MemoryStream(data))
            {
                BEncodedValue result = BEncodedValue.Decode(stream);
                Assert.AreEqual("this is a test string", result.ToString());
                Assert.AreEqual(result is BEncodedString, true);
                Assert.AreEqual(((BEncodedString)result).Text, "this is a test string");
            }
        }

        [Test]
        public void benStringEncoding()
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("22:this is my test string");

            BEncodedString benString = new BEncodedString("this is my test string");
            Assert.IsTrue(Toolbox.ByteMatch(data, benString.Encode()));
        }

        [Test]
        public void benStringEncoding2()
        {
            byte[] data = Encoding.UTF8.GetBytes("0:");

            var benString = new BEncodedString("");
            Assert.IsTrue(Toolbox.ByteMatch(data, benString.Encode()));
        }

        [Test]
        public void benStringEncodingBuffered()
        {
            var data = Encoding.UTF8.GetBytes("22:this is my test string");

            var benString = new BEncodedString("this is my test string");
            var result = new byte[benString.LengthInBytes()];
            benString.Encode(result, 0);
            Assert.IsTrue(Toolbox.ByteMatch(data, result));
        }

        [Test]
        public void benStringLengthInBytes()
        {
            const string text = "thisisateststring";

            BEncodedString str = text;
            int length = text.Length;
            length += text.Length.ToString().Length;
            length++;

            Assert.AreEqual(length, str.LengthInBytes());
        }

        [Test]
        [ExpectedException(typeof(BEncodingException))]
        public void corruptBenStringDecode()
        {
            const string testString = "50:i'm too short";

            BEncodedValue.Decode(Encoding.UTF8.GetBytes(testString));
        }

        [Test]
        [ExpectedException(typeof(BEncodingException))]
        public void CorruptBenStringDecode2()
        {
            const string s = "d8:completei2671e10:incompletei669e8:intervali1836e12min intervali918e5:peers0:e";

            BEncodedValue.Decode(Encoding.ASCII.GetBytes(s));
        }

        #endregion

        #region BEncodedNumber Tests

        [Test]
        public void benNumberDecoding()
        {
            byte[] data = Encoding.UTF8.GetBytes("i12412e");
            using (Stream stream = new MemoryStream(data))
            {
                BEncodedValue result = BEncodedValue.Decode(stream);
                Assert.AreEqual(result is BEncodedNumber, true);
                Assert.AreEqual(result.ToString(), "12412");
                Assert.AreEqual(((BEncodedNumber)result).Number, 12412);
            }
        }

        [Test]
        public void benNumberEncoding()
        {
            byte[] data = Encoding.UTF8.GetBytes("i12345e");
            BEncodedNumber number = 12345;
            Assert.IsTrue(Toolbox.ByteMatch(data, number.Encode()));
        }

        [Test]
        public void benNumberEncoding2()
        {
            byte[] data = Encoding.UTF8.GetBytes("i0e");
            BEncodedNumber number = 0;
            Assert.AreEqual(3, number.LengthInBytes());
            Assert.IsTrue(Toolbox.ByteMatch(data, number.Encode()));
        }

        [Test]
        public void benNumberEncoding3()
        {
            byte[] data = Encoding.UTF8.GetBytes("i1230e");
            BEncodedNumber number = 1230;
            Assert.AreEqual(6, number.LengthInBytes());
            Assert.IsTrue(Toolbox.ByteMatch(data, number.Encode()));
        }

        [Test]
        public void benNumberEncoding4()
        {
            byte[] data = Encoding.UTF8.GetBytes("i-1230e");
            BEncodedNumber number = -1230;
            Assert.AreEqual(7, number.LengthInBytes());
            Assert.IsTrue(Toolbox.ByteMatch(data, number.Encode()));
        }

        [Test]
        public void BenNumberEncoding5()
        {
            var data = Encoding.UTF8.GetBytes("i-123e");
            BEncodedNumber number = -123;
            Assert.AreEqual(6, number.LengthInBytes());
            Assert.IsTrue(Toolbox.ByteMatch(data, number.Encode()));
        }

        [Test]
        public void BenNumberEncoding6()
        {
            BEncodedNumber actual = -123;

            var number = BEncodedValue.Decode<BEncodedNumber>(actual.Encode());

            Assert.AreEqual(actual.Number, number.Number, "#1");
        }

        [Test]
        public void BenNumberEncodingBuffered()
        {
            var data = Encoding.UTF8.GetBytes("i12345e");
            BEncodedNumber number = 12345;
            var result = new byte[number.LengthInBytes()];
            number.Encode(result, 0);
            Assert.IsTrue(Toolbox.ByteMatch(data, result));
        }

        [Test]
        public void benNumberLengthInBytes()
        {
            const int number = 1635;
            BEncodedNumber num = number;
            Assert.AreEqual(number.ToString().Length + 2, num.LengthInBytes());
        }

        [Test]
        [ExpectedException(typeof(BEncodingException))]
        public void corruptBenNumberDecode()
        {
            const string testString = "i35212";
            BEncodedValue.Decode(Encoding.UTF8.GetBytes(testString));
        }

        #endregion

        #region BEncodedList Tests

        [Test]
        public void benListDecoding()
        {
            byte[] data = Encoding.UTF8.GetBytes("l4:test5:tests6:testede");
            using (Stream stream = new MemoryStream(data))
            {
                var result = BEncodedValue.Decode(stream);
                Assert.AreEqual(result.ToString(), "l4:test5:tests6:testede");
                Assert.AreEqual(result is BEncodedList, true);
                var list = (BEncodedList)result;

                Assert.AreEqual(list.Count, 3);
                Assert.AreEqual(list[0] is BEncodedString, true);
                Assert.AreEqual(((BEncodedString)list[0]).Text, "test");
                Assert.AreEqual(((BEncodedString)list[1]).Text, "tests");
                Assert.AreEqual(((BEncodedString)list[2]).Text, "tested");
            }
        }

        [Test]
        public void benListEncoding()
        {
            var data = Encoding.UTF8.GetBytes("l4:test5:tests6:testede");
            var list = new BEncodedList
                           {
                               new BEncodedString("test"),
                               new BEncodedString("tests"),
                               new BEncodedString("tested")
                           };

            Assert.IsTrue(Toolbox.ByteMatch(data, list.Encode()));
        }

        [Test]
        public void benListEncodingBuffered()
        {
            var data = Encoding.UTF8.GetBytes("l4:test5:tests6:testede");
            var list = new BEncodedList
                           {
                               new BEncodedString("test"),
                               new BEncodedString("tests"),
                               new BEncodedString("tested")
                           };
            var result = new byte[list.LengthInBytes()];
            list.Encode(result, 0);
            Assert.IsTrue(Toolbox.ByteMatch(data, result));
        }

        [Test]
        public void benListStackedTest()
        {
            const string benString = "l6:stringl7:stringsl8:stringedei23456eei12345ee";

            var data = Encoding.UTF8.GetBytes(benString);
            var list = (BEncodedList)BEncodedValue.Decode(data);
            var decoded = Encoding.UTF8.GetString(list.Encode());

            Assert.AreEqual(benString, decoded);
        }

        [Test]
        public void BenListLengthInBytes()
        {
            var data = Encoding.UTF8.GetBytes("l4:test5:tests6:testede");
            var list = (BEncodedList) BEncodedValue.Decode(data);

            Assert.AreEqual(data.Length, list.LengthInBytes());
        }

        [Test]
        [ExpectedException(typeof(BEncodingException))]
        public void corruptBenListDecode()
        {
            const string testString = "l3:3521:a3:ae";

            BEncodedValue.Decode(Encoding.UTF8.GetBytes(testString));
        }

        #endregion

        #region BEncodedDictionary Tests

        [Test]
        public void benDictionaryDecoding()
        {
            var data = Encoding.UTF8.GetBytes("d4:spaml1:a1:bee");
            using (Stream stream = new MemoryStream(data))
            {
                var result = BEncodedValue.Decode(stream);
                Assert.AreEqual(result.ToString(), "d4:spaml1:a1:bee");
                Assert.AreEqual(result is BEncodedDictionary, true);

                var dict = (BEncodedDictionary)result;
                Assert.AreEqual(dict.Count, 1);
                Assert.IsTrue(dict["spam"] is BEncodedList);

                var list = (BEncodedList)dict["spam"];
                Assert.AreEqual(((BEncodedString)list[0]).Text, "a");
                Assert.AreEqual(((BEncodedString)list[1]).Text, "b");
            }
        }

        [Test]
        public void benDictionaryEncoding()
        {
            var data = Encoding.UTF8.GetBytes("d4:spaml1:a1:bee");

            var dict = new BEncodedDictionary();
            var list = new BEncodedList
                           {
                               new BEncodedString("a"), 
                               new BEncodedString("b")
                           };

            dict.Add("spam", list);
            Assert.AreEqual(Encoding.UTF8.GetString(data), Encoding.UTF8.GetString(dict.Encode()));
            Assert.IsTrue(Toolbox.ByteMatch(data, dict.Encode()));
        }

        [Test]
        public void BenDictionaryEncodingBuffered()
        {
            var data = Encoding.UTF8.GetBytes("d4:spaml1:a1:bee");
            var dict = new BEncodedDictionary();
            var list = new BEncodedList
                           {
                               new BEncodedString("a"), 
                               new BEncodedString("b")
                           };
            dict.Add("spam", list);
            var result = new byte[dict.LengthInBytes()];
            dict.Encode(result, 0);

            Assert.IsTrue(Toolbox.ByteMatch(data, result));
        }

        [Test]
        public void BenDictionaryStackedTest()
        {
            const string benString = "d4:testd5:testsli12345ei12345ee2:tod3:tomi12345eeee";

            var data = Encoding.UTF8.GetBytes(benString);
            var dict = (BEncodedDictionary)BEncodedValue.Decode(data);
            var decoded = Encoding.UTF8.GetString(dict.Encode());

            Assert.AreEqual(benString, decoded);
        }

        [Test]
        public void BenDictionaryLengthInBytes()
        {
            var data = Encoding.UTF8.GetBytes("d4:spaml1:a1:bee");
            var dict = (BEncodedDictionary) BEncodedValue.Decode(data);

            Assert.AreEqual(data.Length, dict.LengthInBytes());
        }

        [Test]
        [ExpectedException(typeof(BEncodingException))]
        public void CorruptBenDictionaryDecode()
        {
            const string testString = "d3:3521:a3:aedddd";

            BEncodedValue.Decode(Encoding.UTF8.GetBytes(testString));
        }

        #endregion

        #region General Tests

        [Test]
        [ExpectedException(typeof(BEncodingException))]
        public void CorruptBenDataDecode()
        {
            const string testString = "corruption!";

            BEncodedValue.Decode(Encoding.UTF8.GetBytes(testString));
        }

        #endregion
    }
}