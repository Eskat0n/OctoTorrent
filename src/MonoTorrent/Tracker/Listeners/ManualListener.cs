using System;
using System.Collections.Generic;
using System.Text;
using OctoTorrent.BEncoding;
using System.Net;

namespace OctoTorrent.Tracker.Listeners
{
    public class ManualListener : ListenerBase
    {
        private bool running;

        public override bool Running
        {
            get { return running; }
        }

        public override void Start()
        {
            running = true;
        }

        public override void Stop()
        {
            running = false;
        }

        public BEncodedValue Handle(string rawUrl, IPAddress remoteAddress)
        {
            if (rawUrl == null)
                throw new ArgumentNullException("rawUrl");
            if (remoteAddress == null)
                throw new ArgumentOutOfRangeException("remoteAddress");

            rawUrl = rawUrl.Substring(rawUrl.LastIndexOf('/'));
            bool isScrape = rawUrl.StartsWith("/scrape", StringComparison.OrdinalIgnoreCase);
            return Handle(rawUrl, remoteAddress, isScrape);
        }
    }
}
