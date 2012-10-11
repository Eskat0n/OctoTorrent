namespace MonoTorrent.Client
{
    using System;
    using Common;
    using System.IO;
    using BEncoding;

    public class FastResume
    {
        private static readonly BEncodedString VersionKey = "version";
        private static readonly BEncodedString InfoHashKey = "infohash";
        private static readonly BEncodedString BitfieldKey = "bitfield";
        private static readonly BEncodedString BitfieldLengthKey = "bitfield_length";

        public BitField Bitfield { get; private set; }
        public InfoHash Infohash { get; private set; }

        public FastResume(InfoHash infoHash, BitField bitfield)
        {
            if (infoHash == null)
                throw new ArgumentNullException("infoHash");
            if (bitfield == null)
                throw new ArgumentNullException("bitfield");

            Infohash = infoHash;
            Bitfield = bitfield;
        }

        public FastResume(BEncodedDictionary dict)
        {
            CheckContent(dict, VersionKey, 1);
            CheckContent(dict, InfoHashKey);
            CheckContent(dict, BitfieldKey);
            CheckContent(dict, BitfieldLengthKey);

            Infohash = new InfoHash(((BEncodedString)dict[InfoHashKey]).TextBytes);
            Bitfield = new BitField((int)((BEncodedNumber)dict[BitfieldLengthKey]).Number);
            var data = ((BEncodedString)dict[BitfieldKey]).TextBytes;
            Bitfield.FromArray(data, 0, data.Length);
        }

        public static FastResume Decode(byte[] data)
        {
            var dictionary = (BEncodedDictionary) BEncodedValue.Decode(data);
            return new FastResume(dictionary);
        }

        public BEncodedDictionary Encode()
        {
            var dict = new BEncodedDictionary
                           {
                               {VersionKey, (BEncodedNumber) 1},
                               {InfoHashKey, new BEncodedString(Infohash.Hash)},
                               {BitfieldKey, new BEncodedString(Bitfield.ToByteArray())},
                               {BitfieldLengthKey, (BEncodedNumber) Bitfield.Length}
                           };
            return dict;
        }

        public void Encode(Stream stream)
        {
            var data = Encode().Encode();
            stream.Write(data, 0, data.Length);
        }

        private static void CheckContent(BEncodedDictionary dict, BEncodedString key, BEncodedNumber value)
        {
            CheckContent(dict, key);
            if (dict[key].Equals(value) == false)
                throw new TorrentException(string.Format("Invalid FastResume data. The value of '{0}' was '{1}' instead of '{2}'", key, dict[key], value));
        }

        private static void CheckContent(BEncodedDictionary dict, BEncodedString key)
        {
            if (dict.ContainsKey(key) == false)
                throw new TorrentException(string.Format("Invalid FastResume data. Key '{0}' was not present", key));
        }
    }
}
