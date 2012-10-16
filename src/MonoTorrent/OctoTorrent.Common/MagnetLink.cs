namespace OctoTorrent
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class MagnetLink
    {
        private readonly ICollection<string> _webseeds = new List<string>();

        public MagnetLink(string url)
        {
            Check.Url(url);
            AnnounceUrls = new RawTrackerTier();

            ParseMagnetLink(url);
        }

        public RawTrackerTier AnnounceUrls { get; private set; }

        public InfoHash InfoHash { get; private set; }

        public string Name { get; private set; }

        public IEnumerable<string> Webseeds { get { return _webseeds; }}

        private void ParseMagnetLink(string url)
        {
            var splitStr = url.Split('?');
            if (splitStr.Length == 0 || splitStr[0] != "magnet:")
                throw new FormatException("The magnet link must start with 'magnet:?'.");

            if (splitStr.Length == 1)
                return; //no parametter

            var keyValuePairs = splitStr[1].Split('&', ';')
                .Select(x => x.Split('='))
                .Select(x => new {Key = x[0], Value = x[1], x.Length});

            foreach (var keyValue in keyValuePairs)
            {
                if (keyValue.Length != 2)
                    throw new FormatException("A field-value pair of the magnet link contain more than one equal'.");
                switch (keyValue.Key.Substring(0, 2))
                {
                    case "xt": //exact topic
                        if (InfoHash != null)
                            throw new FormatException("More than one infohash in magnet link is not allowed.");

                        if (keyValue.Value.Length != 49 && keyValue.Value.Length != 41)
                            throw new FormatException("Infohash must be base32 or hex encoded.");

                        var val = keyValue.Value.Substring(9);
                        switch (keyValue.Value.Substring(0, 9))
                        {
                            case "urn:sha1:": //base32 hash
                            case "urn:btih:":
                                if (val.Length == 32)
                                    InfoHash = InfoHash.FromBase32(val);
                                else if (val.Length == 40)
                                    InfoHash = InfoHash.FromHex(val);
                                break;
                        }
                        break;
                    case "tr": //address tracker
                        AnnounceUrls.Add(keyValue.Value);
                        break;
                    case "as": //Acceptable Source
                        _webseeds.Add(keyValue.Value);
                        break;
                    case "dn": //display name
                        Name = keyValue.Value;
                        break;
                    case "xl": //exact length
                    case "xs": // eXact Source - P2P link.
                    case "kt": //keyword topic
                    case "mt": //manifest topic
                        //not supported for moment
                        break;
                        //not supported
                }
            }
        }
    }
}