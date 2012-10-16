#if !DISABLE_DHT
using System;
using System.Collections.Generic;
using System.Text;

namespace OctoTorrent.Dht
{
    interface ITask
    {
        event EventHandler<TaskCompleteEventArgs> Completed;

        bool Active { get; }
        void Execute();
    }
}
#endif