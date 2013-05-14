using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using OctoTorrent.Client.Connections;

namespace OctoTorrent.Client
{
    public static class Logger
    {
        private static readonly List<TraceListener> Listeners;

        static Logger()
        {
            Listeners = new List<TraceListener>();
        }

        public static void AddListener(TraceListener listener)
        {
            if (listener == null)
                throw new ArgumentNullException("listener");

            lock (Listeners)
                Listeners.Add(listener);
        }
		
		public static void Flush()
		{
			lock (Listeners)
				Listeners.ForEach (listener => listener.Flush());
		}
        /*
        internal static void Log(PeerIdInternal id, string message)
        {
            Log(id.PublicId, message);
        }

        internal static void Log(PeerId id, string message)
        {
            lock (listeners)
                for (int i = 0; i < listeners.Count; i++)
                    listeners[i].WriteLine(id.GetHashCode().ToString() + ": " + message);
        }

        internal static void Log(string p)
        {
            lock (listeners)
                for (int i = 0; i < listeners.Count; i++)
                    listeners[i].WriteLine(p);
        }*/

        private static readonly StringBuilder StringBuilder = new StringBuilder();

        [Conditional("DO_NOT_ENABLE")]
        internal static void Log(IConnection connection, string message, params object[] formatting)
        {
            lock (Listeners)
            {
                StringBuilder.Remove(0, StringBuilder.Length);
                StringBuilder.Append(Environment.TickCount);
                StringBuilder.Append(": ");

                if (connection != null)
                    StringBuilder.Append(connection.EndPoint);

                if (formatting != null && formatting.Length != 0)
                    StringBuilder.Append(string.Format(message, formatting));
                else
                    StringBuilder.Append(message);

				var s = StringBuilder.ToString();
                Listeners.ForEach(listener => listener.WriteLine(s));
            }
        }
    }
}
