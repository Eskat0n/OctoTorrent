namespace SampleClient.Dht
{
    using System;
    using System.Net;
    using System.IO;
    using OctoTorrent;
    using OctoTorrent.Dht;
    using OctoTorrent.Dht.Listeners;

    internal static class Program
    {
        private static void Main()
        {
            var listener = new DhtListener(new IPEndPoint(IPAddress.Parse("192.168.0.6"), 15000));
            var engine = new DhtEngine(listener);

            byte[] nodes = null;
            if (File.Exists("mynodes"))
                nodes = File.ReadAllBytes("mynodes");

            listener.Start();
            engine.PeersFound += (o, e) =>
                                     {
                                         Console.WriteLine("I FOUND PEERS: {0}", e.Peers.Count);
                                         engine.Start(nodes);

                                         var random = new Random(5);
                                         var bytes = new byte[20];
                                         lock (random)
                                             random.NextBytes(bytes);

                                         while (Console.ReadLine() != "q")
                                         {
                                             for (var i = 0; i < 30; i++)
                                             {
                                                 Console.WriteLine("Waiting: {0} seconds left", (30 - i));
                                                 System.Threading.Thread.Sleep(1000);
                                             }

                                             engine.GetPeers(bytes);
                                             random.NextBytes(bytes);
                                         }

                                         File.WriteAllBytes("mynodes", engine.SaveNodes());
                                     };
        }
    }
}