namespace MonoTorrent.Client
{
    using System;

    public class CriticalExceptionEventArgs : EventArgs
    {
        private readonly ClientEngine _engine;
        private readonly Exception _ex;

        public CriticalExceptionEventArgs(Exception ex, ClientEngine engine)
        {
            if (ex == null)
                throw new ArgumentNullException("ex");
            if (engine == null)
                throw new ArgumentNullException("engine");

            _engine = engine;
            _ex = ex;
        }

        public ClientEngine Engine
        {
            get { return _engine; }
        }

        public Exception Exception
        {
            get { return _ex; }
        }
    }
}