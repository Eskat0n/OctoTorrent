namespace TrackerApp
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Threading;
    using OctoTorrent;
    using OctoTorrent.Common;

    public class StressTest
    {
        private readonly List<string> _hashes = new List<string>();
        private readonly Random _random = new Random(1);
        private readonly SpeedMonitor _requests = new SpeedMonitor();
        private readonly int _threadSleepTime;
        private readonly Thread[] _threads;

        public StressTest(int torrents, int peers, int requests)
        {
            for (int i = 0; i < torrents; i++)
            {
                var infoHash = new byte[20];
                _random.NextBytes(infoHash);
                _hashes.Add(new InfoHash(infoHash).UrlEncode());
            }

            _threadSleepTime = Math.Max((int) (20000.0/requests + 0.5), 1);
            _threads = new Thread[20];
        }

        public int RequestRate
        {
            get { return _requests.Rate; }
        }

        public long TotalTrackerRequests
        {
            get { return _requests.Total; }
        }

        public void Start(string trackerAddress)
        {
            for (var i = 0; i < _threads.Length; i++)
            {
                _threads[i] = new Thread(() =>
                                            {
                                                var sb = new StringBuilder();
                                                var torrent = 0;
                                                while (true)
                                                {
                                                    sb.Remove(0, sb.Length);

                                                    var ipaddress = _random.Next(0, _hashes.Count);

                                                    sb.Append(trackerAddress);
                                                    sb.Append("?info_hash=");
                                                    sb.Append(_hashes[torrent++]);
                                                    sb.Append("&peer_id=");
                                                    sb.Append("12345123451234512345");
                                                    sb.Append("&port=");
                                                    sb.Append("5000");
                                                    sb.Append("&uploaded=");
                                                    sb.Append("5000");
                                                    sb.Append("&downloaded=");
                                                    sb.Append("5000");
                                                    sb.Append("&left=");
                                                    sb.Append("5000");
                                                    sb.Append("&compact=");
                                                    sb.Append("1");

                                                    var request = WebRequest.Create(sb.ToString());
                                                    request.BeginGetResponse(r =>
                                                                                 {
                                                                                     try
                                                                                     {
                                                                                         request.EndGetResponse(r)
                                                                                             .Close();
                                                                                         _requests.AddDelta(1);
                                                                                     }
                                                                                     catch
                                                                                     {
                                                                                     }
                                                                                     finally
                                                                                     {
                                                                                         _requests.Tick();
                                                                                     }
                                                                                 }, null);

                                                    Thread.Sleep(_threadSleepTime);
                                                }
                                            });
                _threads[i].Start();
            }
        }
    }
}