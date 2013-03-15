namespace OctoTorrent.SampleClient
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;

    /// <summary>
    ///   Keeps track of the X most recent number of events recorded by the listener. X is specified in the constructor
    /// </summary>
    public class Top10Listener : TraceListener
    {
        private readonly int _capacity;
        private readonly LinkedList<string> _traces;

        public Top10Listener(int capacity)
        {
            _capacity = capacity;
            _traces = new LinkedList<string>();
        }

        public override void Write(string message)
        {
            lock (_traces)
                _traces.Last.Value += message;
        }

        public override void WriteLine(string message)
        {
            lock (_traces)
            {
                if (_traces.Count >= _capacity)
                    _traces.RemoveFirst();

                _traces.AddLast(message);
            }
        }

        public void ExportTo(TextWriter output)
        {
            lock (_traces)
                foreach (var s in _traces)
                    output.WriteLine(s);
        }
    }
}