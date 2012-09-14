using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using MonoTorrent.Common;
using MonoTorrent.Client;
using System.Net;
using System.Diagnostics;
using System.Threading;
using MonoTorrent.BEncoding;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Dht;
using MonoTorrent.Dht.Listeners;

namespace MonoTorrent
{
    using System.Linq;

    class main
    {
        static string _dhtNodeFile;
        static string _basePath;
        static string _downloadsPath;
        static string _fastResumeFile;
        static string _torrentsPath;
        static ClientEngine _engine;				// The engine used for downloading
        static List<TorrentManager> torrents;	// The list where all the torrentManagers will be stored that the engine gives us
        static Top10Listener listener;			// This is a subclass of TraceListener which remembers the last 20 statements sent to it

        static void Main(string[] args)
        {
            /* Generate the paths to the folder we will save .torrent files to and where we download files to */
            _basePath = Environment.CurrentDirectory;						// This is the directory we are currently in
            _torrentsPath = Path.Combine(_basePath, "Torrents");				// This is the directory we will save .torrents to
            _downloadsPath = Path.Combine(_basePath, "Downloads");			// This is the directory we will save downloads to
            _fastResumeFile = Path.Combine(_torrentsPath, "fastresume.data");
            _dhtNodeFile = Path.Combine(_basePath, "DhtNodes");
            torrents = new List<TorrentManager>();							// This is where we will store the torrentmanagers
            listener = new Top10Listener(10);

            // We need to cleanup correctly when the user closes the window by using ctrl-c
            // or an unhandled exception happens
            Console.CancelKeyPress += delegate { Shutdown(); };
            AppDomain.CurrentDomain.ProcessExit += delegate { Shutdown(); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { Console.WriteLine(e.ExceptionObject); Shutdown(); };
            Thread.GetDomain().UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { Console.WriteLine(e.ExceptionObject); Shutdown(); };

            StartEngine();
        }

        private static void StartEngine()
        {
            int port;
            Torrent torrent = null;
            // Ask the user what port they want to use for incoming connections
            Console.Write(Environment.NewLine + "Choose a listen port: ");
            while (!Int32.TryParse(Console.ReadLine(), out port)) { }



            // Create the settings which the engine will use
            // downloadsPath - this is the path where we will save all the files to
            // port - this is the port we listen for connections on
            EngineSettings engineSettings = new EngineSettings(_downloadsPath, port);
            engineSettings.PreferEncryption = false;
            engineSettings.AllowedEncryption = EncryptionTypes.All;

            //engineSettings.GlobalMaxUploadSpeed = 30 * 1024;
            //engineSettings.GlobalMaxDownloadSpeed = 100 * 1024;
            //engineSettings.MaxReadRate = 1 * 1024 * 1024;


            // Create the default settings which a torrent will have.
            // 4 Upload slots - a good ratio is one slot per 5kB of upload speed
            // 50 open connections - should never really need to be changed
            // Unlimited download speed - valid range from 0 -> int.Max
            // Unlimited upload speed - valid range from 0 -> int.Max
            TorrentSettings torrentDefaults = new TorrentSettings(4, 150, 0, 0);

            // Create an instance of the engine.
            _engine = new ClientEngine(engineSettings);
            _engine.ChangeListenEndpoint(new IPEndPoint(IPAddress.Any, port));
            byte[] nodes = null;
            try
            {
                nodes = File.ReadAllBytes(_dhtNodeFile);
            }
            catch
            {
                Console.WriteLine("No existing dht nodes could be loaded");
            }

            DhtListener dhtListner = new DhtListener (new IPEndPoint (IPAddress.Any, port));
            DhtEngine dht = new DhtEngine (dhtListner);
            _engine.RegisterDht(dht);
            dhtListner.Start();
            _engine.DhtEngine.Start(nodes);
            
            // If the SavePath does not exist, we want to create it.
            if (!Directory.Exists(_engine.Settings.SavePath))
                Directory.CreateDirectory(_engine.Settings.SavePath);

            // If the torrentsPath does not exist, we want to create it
            if (!Directory.Exists(_torrentsPath))
                Directory.CreateDirectory(_torrentsPath);

            BEncodedDictionary fastResume;
            try
            {
                fastResume = BEncodedValue.Decode<BEncodedDictionary>(File.ReadAllBytes(_fastResumeFile));
            }
            catch
            {
                fastResume = new BEncodedDictionary();
            }

            // For each file in the torrents path that is a .torrent file, load it into the engine.
            foreach (string file in Directory.GetFiles(_torrentsPath))
            {
                if (file.EndsWith(".torrent"))
                {
                    try
                    {
                        // Load the .torrent from the file into a Torrent instance
                        // You can use this to do preprocessing should you need to
                        torrent = Torrent.Load(file);
                        Console.WriteLine(torrent.InfoHash.ToString());
                    }
                    catch (Exception e)
                    {
                        Console.Write("Couldn't decode {0}: ", file);
                        Console.WriteLine(e.Message);
                        continue;
                    }
                    // When any preprocessing has been completed, you create a TorrentManager
                    // which you then register with the engine.
                    TorrentManager manager = new TorrentManager(torrent, _downloadsPath, torrentDefaults);
                    if (fastResume.ContainsKey(torrent.InfoHash.ToHex ()))
                        manager.LoadFastResume(new FastResume ((BEncodedDictionary)fastResume[torrent.infoHash.ToHex ()]));
                    _engine.Register(manager);

                    // Store the torrent manager in our list so we can access it later
                    torrents.Add(manager);
                    manager.PeersFound += ManagerPeersFound;
                }
            }

            // If we loaded no torrents, just exist. The user can put files in the torrents directory and start
            // the client again
            if (torrents.Count == 0)
            {
                Console.WriteLine("No torrents found in the Torrents directory");
                Console.WriteLine("Exiting...");
                _engine.Dispose();
                return;
            }

            // For each torrent manager we loaded and stored in our list, hook into the events
            // in the torrent manager and start the engine.
            foreach (TorrentManager manager in torrents)
            {
                // Every time a piece is hashed, this is fired.
                manager.PieceHashed += delegate(object o, PieceHashedEventArgs e) {
                    lock (listener)
                        listener.WriteLine(string.Format("Piece Hashed: {0} - {1}", e.PieceIndex, e.HashPassed ? "Pass" : "Fail"));
                };

                // Every time the state changes (Stopped -> Seeding -> Downloading -> Hashing) this is fired
                manager.TorrentStateChanged += delegate (object o, TorrentStateChangedEventArgs e) {
                    lock (listener)
                        listener.WriteLine("OldState: " + e.OldState.ToString() + " NewState: " + e.NewState.ToString());
                };

                // Every time the tracker's state changes, this is fired
                foreach (var tracker in manager.TrackerManager.SelectMany(tier => tier.Trackers))
                {
                    tracker.AnnounceComplete += (sender, e) => listener.WriteLine(string.Format("{0}: {1}", e.Successful,
                                                                                          e.Tracker.ToString()));
                }
                // Start the torrentmanager. The file will then hash (if required) and begin downloading/seeding
                manager.Start();
            }

            // While the torrents are still running, print out some stats to the screen.
            // Details for all the loaded torrent managers are shown.
            var i = 0;
            var running = true;
            var sb = new StringBuilder(1024);
            while (running)
            {
                if ((i++) % 10 == 0)
                {
                    sb.Remove(0, sb.Length);
                    running = torrents.Exists(delegate(TorrentManager m) { return m.State != TorrentState.Stopped; });

                    AppendFormat(sb, "Total Download Rate: {0:0.00}kB/sec", _engine.TotalDownloadSpeed / 1024.0);
                    AppendFormat(sb, "Total Upload Rate:   {0:0.00}kB/sec", _engine.TotalUploadSpeed / 1024.0);
                    AppendFormat(sb, "Disk Read Rate:      {0:0.00} kB/s", _engine.DiskManager.ReadRate / 1024.0);
                    AppendFormat(sb, "Disk Write Rate:     {0:0.00} kB/s", _engine.DiskManager.WriteRate / 1024.0);
                    AppendFormat(sb, "Total Read:         {0:0.00} kB", _engine.DiskManager.TotalRead / 1024.0);
                    AppendFormat(sb, "Total Written:      {0:0.00} kB", _engine.DiskManager.TotalWritten / 1024.0);
                    AppendFormat(sb, "Open Connections:    {0}", _engine.ConnectionManager.OpenConnections);
                    
                    foreach (var manager in torrents)
                    {
                        AppendSeperator(sb);
                        AppendFormat(sb, "State:           {0}", manager.State);
                        AppendFormat(sb, "Name:            {0}", manager.Torrent == null ? "MetaDataMode" : manager.Torrent.Name);
                        AppendFormat(sb, "Progress:           {0:0.00}", manager.Progress);
                        AppendFormat(sb, "Download Speed:     {0:0.00} kB/s", manager.Monitor.DownloadSpeed / 1024.0);
                        AppendFormat(sb, "Upload Speed:       {0:0.00} kB/s", manager.Monitor.UploadSpeed / 1024.0);
                        AppendFormat(sb, "Total Downloaded:   {0:0.00} MB", manager.Monitor.DataBytesDownloaded / (1024.0 * 1024.0));
                        AppendFormat(sb, "Total Uploaded:     {0:0.00} MB", manager.Monitor.DataBytesUploaded / (1024.0 * 1024.0));
                        var tracker = manager.TrackerManager.CurrentTracker;
                        //AppendFormat(sb, "Tracker Status:     {0}", tracker == null ? "<no tracker>" : tracker.State.ToString());
                        AppendFormat(sb, "Warning Message:    {0}", tracker == null ? "<no tracker>" : tracker.WarningMessage);
                        AppendFormat(sb, "Failure Message:    {0}", tracker == null ? "<no tracker>" : tracker.FailureMessage);
                        if (manager.PieceManager != null)
                            AppendFormat(sb, "Current Requests:   {0}", manager.PieceManager.CurrentRequestCount());
                        
                        foreach (var peerId in manager.GetPeers())
                            AppendFormat(sb, "\t{2} - {1:0.00}/{3:0.00}kB/sec - {0}", peerId.Peer.ConnectionUri,
                                                                                      peerId.Monitor.DownloadSpeed / 1024.0,
                                                                                      peerId.AmRequestingPiecesCount,
                                                                                      peerId.Monitor.UploadSpeed/ 1024.0);
                       
                        AppendFormat(sb, "", null);
                        if (manager.Torrent != null)
                            foreach (var file in manager.Torrent.Files)
                                AppendFormat(sb, "{1:0.00}% - {0}", file.Path, file.BitField.PercentComplete);
                    }
                    Console.Clear();
                    Console.WriteLine(sb.ToString());
                    listener.ExportTo(Console.Out);
                }

                Thread.Sleep(500);
            }
        }

        static void ManagerPeersFound(object sender, PeersAddedEventArgs e)
        {
            lock (listener)
                listener.WriteLine(string.Format("Found {0} new peers and {1} existing peers", e.NewPeers, e.ExistingPeers ));//throw new Exception("The method or operation is not implemented.");
        }

        private static void AppendSeperator(StringBuilder sb)
        {
            AppendFormat(sb, "", null);
            AppendFormat(sb, "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - -", null);
            AppendFormat(sb, "", null);
        }
		private static void AppendFormat(StringBuilder sb, string str, params object[] formatting)
		{
            if (formatting != null)
                sb.AppendFormat(str, formatting);
            else
                sb.Append(str);
			sb.AppendLine();
		}

		private static void Shutdown()
		{
            var fastResume = new BEncodedDictionary();
            foreach (var torrentManager in torrents)
            {
                torrentManager.Stop();
                while (torrentManager.State != TorrentState.Stopped)
                {
                    Console.WriteLine("{0} is {1}", torrentManager.Torrent.Name, torrentManager.State);
                    Thread.Sleep(250);
                }

                fastResume.Add(torrentManager.Torrent.InfoHash.ToHex (), torrentManager.SaveFastResume().Encode());
            }

#if !DISABLE_DHT
            File.WriteAllBytes(_dhtNodeFile, _engine.DhtEngine.SaveNodes());
#endif
            File.WriteAllBytes(_fastResumeFile, fastResume.Encode());
            _engine.Dispose();

			foreach (TraceListener lst in Debug.Listeners)
			{
				lst.Flush();
				lst.Close();
			}

            Thread.Sleep(2000);
		}
	}
}
