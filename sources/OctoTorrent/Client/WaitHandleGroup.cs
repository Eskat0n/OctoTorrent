namespace OctoTorrent.Client
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;

    internal class WaitHandleGroup : WaitHandle
    {
        private readonly List<WaitHandle> _handles;
        private readonly List<string> _names;

        public WaitHandleGroup()
        {
            _handles = new List<WaitHandle>();
            _names = new List<string>();
        }

        public void AddHandle(WaitHandle handle, string name)
        {
            _handles.Add(handle);
            _names.Add(name);
        }

        public override void Close()
        {
            foreach (var handle in _handles)
                handle.Close();
        }

        public override bool WaitOne()
        {
            if (_handles.Count == 0)
                return true;
            return WaitAll(_handles.ToArray());
        }

        public override bool WaitOne(int millisecondsTimeout)
        {
            if (_handles.Count == 0)
                return true;
            return WaitAll(_handles.ToArray(), millisecondsTimeout);
        }

        public override bool WaitOne(TimeSpan timeout)
        {
            if (_handles.Count == 0)
                return true;
            return WaitAll(_handles.ToArray(), timeout);
        }

        public override bool WaitOne(int millisecondsTimeout, bool exitContext)
        {
            if (_handles.Count == 0)
                return true;
            return WaitAll(_handles.ToArray(), millisecondsTimeout, exitContext);
        }

        public override bool WaitOne(TimeSpan timeout, bool exitContext)
        {
            if (_handles.Count == 0)
                return true;
            return WaitAll(_handles.ToArray(), timeout, exitContext);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            for (var i = 0; i < _handles.Count; i ++)
            {
                sb.Append("WaitHandle: ");
                sb.Append(_names[i]);
                sb.Append(". State: ");
                sb.Append(_handles[i].WaitOne(0) ? "Signalled" : "Unsignalled");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}