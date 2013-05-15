namespace OctoTorrent.Tests.Common
{
    using System;
    using NUnit.Framework;
    using OctoTorrent.Common;

    [TestFixture]
	public class UriQueryBuilderTest
	{

		[Test]
        public void TestToString ()
        {
            var builder = new UriQueryBuilder("http://mytest.com/announce.aspx?key=1");
            builder.Add ("key", 2);
            builder.Add ("foo", 2);
            builder.Add ("foo", "bar");
            Assert.AreEqual(new Uri ("http://mytest.com/announce.aspx?key=2&foo=bar"), builder.ToUri (),"#1");
            
            builder = new UriQueryBuilder("http://mytest.com/announce.aspx?passkey=1");
            builder.Add ("key", 2);
            Assert.AreEqual(new Uri ("http://mytest.com/announce.aspx?passkey=1&key=2"), builder.ToUri (),"#2");
            
            builder = new UriQueryBuilder("http://mytest.com/announce.aspx");
            Assert.AreEqual(new Uri ("http://mytest.com/announce.aspx"), builder.ToUri (),"#3");
            
            builder = new UriQueryBuilder("http://mytest.com/announce.aspx");
            var infoHash = new byte[] {0x01, 0x47, 0xff, 0xaa, 0xbb, 0xcc};
            builder.Add ("key", UriHelper.UrlEncode(infoHash));

            Assert.AreEqual(new Uri ("http://mytest.com/announce.aspx?key=%01G%ff%aa%bb%cc"), builder.ToUri (),"#4");
        }
        
        [Test]
        public void ContainQuery ()
        {
            var builder = new UriQueryBuilder("http://mytest.com/announce.aspx?key=1&foo=bar");

            Assert.IsTrue(builder.Contains("key"), "#1");
            Assert.IsTrue(builder.Contains("foo"), "#2");
            Assert.IsFalse(builder.Contains("bar"), "#3");
        }

        [Test]
        public void CaseInsensitiveTest ()
        {
            var builder = new UriQueryBuilder ("http://www.example.com?first=1&second=2&third=4");

            Assert.IsTrue(builder.Contains("FiRsT"));
            Assert.AreEqual(builder["FiRst"], "1");
        }

        [Test]
        public void AddParams ()
        {
            var builder = new UriQueryBuilder ("http://example.com");
            builder ["Test"] = "2";
            builder ["Test"] = "7";

            Assert.AreEqual ("7", builder ["Test"], "#1");
        }
	}
}