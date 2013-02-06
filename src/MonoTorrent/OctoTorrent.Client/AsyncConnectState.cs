namespace OctoTorrent.Client
{
    using Common;
    using Connections;

    static partial class NetworkIO
    {
        #region Nested type: AsyncConnectState

        private class AsyncConnectState : ICacheable
        {
            public IConnection Connection { get; private set; }

            public AsyncIOCallback Callback { get; private set; }

            public object State { get; private set; }

            #region ICacheable Members

            public void Initialise()
            {
                Initialise(null, null, null);
            }

            #endregion

            public AsyncConnectState Initialise(IConnection connection, AsyncIOCallback callback, object state)
            {
                Connection = connection;
                Callback = callback;
                State = state;
                return this;
            }
        }

        #endregion
    }
}