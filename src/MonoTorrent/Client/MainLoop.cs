//
// MainLoop.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;
using System.Threading;
using Mono.Ssdp.Internal;
using OctoTorrent.Common;

namespace OctoTorrent.Client
{
	public delegate void MainLoopResult (object result);
    public delegate object MainLoopJob();
    public delegate void MainLoopTask();
    public delegate bool TimeoutTask();

    public class MainLoop : IDisposable
    {
        public string Name { get; private set; }

        private class DelegateTask : ICacheable
        {
            private ManualResetEvent handle;
            private bool isBlocking;
            private MainLoopJob job;
            private object jobResult;
            private Exception storedException;
            private MainLoopTask task;
            private TimeoutTask timeout;
            private bool timeoutResult;

            public bool IsBlocking
            {
                get { return isBlocking; }
                set { isBlocking = value; }
            }

            public MainLoopJob Job
            {
                get { return job; }
                set { job = value; }
            }

            public Exception StoredException
            {
                get { return storedException; }
                set { storedException = value; }
            }

            public MainLoopTask Task
            {
                get { return task; }
                set { task = value; }
            }

            public TimeoutTask Timeout
            {
                get { return timeout; }
                set { timeout = value; }
            }

            public object JobResult
            {
                get { return jobResult; }
            }

            public bool TimeoutResult
            {
                get { return timeoutResult; }
            }

            public ManualResetEvent WaitHandle
            {
                get { return handle; }
            }

            public DelegateTask()
            {
                handle = new ManualResetEvent(false);
            }
            
            public void Execute()
            {
                try
                {
                    if (job != null)
                        jobResult = job();
                    else if (task != null)
                        task();
                    else if (timeout != null)
                        timeoutResult = timeout();
                }
                catch (Exception ex)
                {
                    storedException = ex;

                    // FIXME: I assume this case can't happen. The only user interaction
                    // with the mainloop is with blocking tasks. Internally it's a big bug
                    // if i allow an exception to propagate to the mainloop.
                    if (!IsBlocking)
                        throw;
                }
                finally
                {
                    handle.Set();
                }
            }

            public void Initialise()
            {
                isBlocking = false;
                job = null;
                jobResult = null;
                storedException = null;
                task = null;
                timeout = null;
                timeoutResult = false;
            }
        }

        private readonly TimeoutDispatcher _dispatcher = new TimeoutDispatcher();
        private readonly AutoResetEvent _handle = new AutoResetEvent(false);
        private readonly ICache<DelegateTask> _cache = new Cache<DelegateTask>(true).Synchronize();
        private readonly Queue<DelegateTask> _tasks = new Queue<DelegateTask>();
        internal readonly Thread Thread;

        private bool _disposed;
        private bool _disposeEnqueued;

        public MainLoop(string name)
        {
            Name = name;

            Thread = new Thread(Loop) {IsBackground = true};
            Thread.Start();
        }

        void Loop()
        {
            while (true)
            {
                DelegateTask task = null;
                
                lock (_tasks)
                {
                    if (_tasks.Count > 0)
                        task = _tasks.Dequeue();
                }

                if (task == null)
                {
                    if (_disposeEnqueued)
                    {
                        _disposed = true;
                        _disposeEnqueued = false;
                        break;
                    }

                    _handle.WaitOne();
                }
                else
                {
                    task.Execute();
                    if (!task.IsBlocking)
                        _cache.Enqueue(task);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposeEnqueued = true;
        }

        private void Queue(DelegateTask task)//, Priority priority = Priority.Normal)
        {
            if (task == null)
                return;

            lock (_tasks)
            {                
                _tasks.Enqueue(task);
                _handle.Set();
            }
        }

        public void Queue(MainLoopTask task)
        {
            var dTask = _cache.Dequeue();
            dTask.Task = task;
            Queue(dTask);
        }

        public void QueueWait(MainLoopTask task)
        {
            var dTask = _cache.Dequeue();
            dTask.Task = task;
            try
            {
                QueueWait(dTask);
            }
            finally
            {
                _cache.Enqueue(dTask);
            }
        }

        public object QueueWait(MainLoopJob task)
        {
            var dTask = _cache.Dequeue();
            dTask.Job = task;

            try
            {
                QueueWait(dTask);
                return dTask.JobResult;
            }
            finally
            {
                _cache.Enqueue(dTask);
            }
        }

        private void QueueWait(DelegateTask task)
        {
            task.WaitHandle.Reset();
            task.IsBlocking = true;
            if (Thread.CurrentThread == Thread)
                task.Execute();
            else
                Queue(task);//, Priority.Highest);

            task.WaitHandle.WaitOne();

            if (task.StoredException != null)
                throw new TorrentException("Exception in mainloop", task.StoredException);
        }

        public uint QueueTimeout(TimeSpan span, TimeoutTask task)
        {
            var dTask = _cache.Dequeue();
            dTask.Timeout = task;

            return _dispatcher.Add(span, delegate {
                QueueWait(dTask);
                return dTask.TimeoutResult;
            });
        }

        public AsyncCallback Wrap(AsyncCallback callback)
        {
            return result => Queue(() => callback(result));
        }
    }
}