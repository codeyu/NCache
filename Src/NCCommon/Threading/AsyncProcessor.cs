// Copyright (c) 2017 Alachisoft
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;
using System.Threading;
using Alachisoft.NCache.Common.Util;
using Alachisoft.NCache.Common.Logger;
using Alachisoft.NCache.Common.Stats;

namespace Alachisoft.NCache.Common.Threading
{
	/// <summary>
	/// A class that helps doing things that can be done asynchronously
	/// </summary>
    public class AsyncProcessor
    {
        /// <summary>
        /// Interface to be implemented by all async events.
        /// </summary>
        public interface IAsyncTask
        {
            /// <summary> Process itself. </summary>
            void Process();
        }

        /// <summary> The worker thread. </summary>
        private Thread[] _workerThreads;

        /// <summary> The queue of events. </summary>
        private Queue _eventsHi, _eventsLow;

        
        private int _numProcessingThreads = 1;
        private Boolean _started;
        ILogger NCacheLog;
        bool _isShutdown = false;
        object _shutdownMutex = new object();

        /// <summary>
        /// Constructor
        /// </summary>
        public AsyncProcessor(ILogger NCacheLog)
            : this()
        {
            this.NCacheLog = NCacheLog;
        }

        public AsyncProcessor()
            : this(1)
        {
        }

        public AsyncProcessor(int numProcessingThread):this(numProcessingThread,null)
        {
        }

        public AsyncProcessor(int numProcessingThread,ILogger logger)
        {
            this.NCacheLog = NCacheLog;
            if (numProcessingThread < 1) numProcessingThread = 1;

            _numProcessingThreads = numProcessingThread;
            _eventsHi = new Queue(256);
            _eventsLow = new Queue(256);
        }

        /// <summary>
        /// Add a low priority event to the event queue
        /// </summary>
        /// <param name="evnt">event</param>
        public void Enqueue(IAsyncTask evnt)
        {
            lock (this)
            {
                _eventsHi.Enqueue(evnt);
                Monitor.Pulse(this);
            }
        }

        /// <summary>
        /// Add a low priority event to the event queue
        /// </summary>
        /// <param name="evnt">event</param>
        public void EnqueueLowPriority(IAsyncTask evnt)
        {
            lock (this)
            {
                _eventsLow.Enqueue(evnt);
                Monitor.Pulse(this);
            }
        }

        /// <summary>
        /// Start processing
        /// </summary>
        public void Start()
        {
            try
            {
                lock (this)
                {
                    if (!_started)
                    {
                        _workerThreads = new Thread[_numProcessingThreads];
                        _started = true;

                        for (int i = 0; i < _workerThreads.Length; i++)
                        {
                            Thread thread = new Thread(new ThreadStart(Run));
                            thread.IsBackground = true;
                            thread.Name = "AsyncProcessor.workerThread # " + i;
                            thread.Start();
                            _workerThreads[i] = thread;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (NCacheLog != null)
                {
                    NCacheLog.Error("AsyncProcessor.Start()", ex.Message);
                }
            }
        }

        /// <summary>
        /// Stop processing.
        /// </summary>
        public void Stop()
        {
            try
            {
                lock (this)
                {
                    if (_started)
                    {
                        for (int i = 0; i < _workerThreads.Length; i++)
                        {
                            Thread thread = _workerThreads[i];

                            if (thread != null && thread.IsAlive)
                            {
                                thread.Abort();
                                _workerThreads[i] = null;
                            }
                        }
                        _started = false;
                    }
                }
            }
            catch (Exception ex)
            {
                if (NCacheLog != null)
                {
                    NCacheLog.Error("AsyncProcessor.Stop()", ex.Message);
                }
            }
        }

        /// <summary>
        /// Thread function, keeps running.
        /// </summary>
        protected void Run()
        {
            while (_started)
            {
                IAsyncTask evnt = null;
                try
                {
                    lock (this)
                    {
                        if ((_eventsHi.Count < 1) && (_eventsLow.Count < 1) && !_isShutdown)
                            Monitor.Wait(this);
                        if ((_eventsHi.Count < 1) && _isShutdown)
                        {
                            lock (_shutdownMutex)
                            {
                                Monitor.PulseAll(_shutdownMutex);
                                break;
                            }
                        }

                        if (_eventsHi.Count > 0)
                        {
                            evnt = (IAsyncTask)_eventsHi.Dequeue();
                        }
                        else if (_eventsLow.Count > 0)
                        {
                            evnt = (IAsyncTask)_eventsLow.Dequeue();
                        }
                    }
                    if (evnt == null && _eventsHi.Count < 1 && _isShutdown)
                    {
                        lock (_shutdownMutex)
                        {
                            Monitor.PulseAll(_shutdownMutex);
                            break;
                        }
                    }
                    if (evnt == null) continue;

                    evnt.Process();
                }
                catch (ThreadAbortException e)
                {
                    if (NCacheLog != null)
                    {
                        NCacheLog.Flush();
                    }
                    break;
                }
                catch (NullReferenceException nr) { }
                catch (Exception e)
                { 
                    string exceptionString = e.ToString();
                    if (exceptionString != "ChannelNotConnectedException" && exceptionString != "ChannelClosedException")
                    {
                        if (NCacheLog != null)
                        {
                            NCacheLog.Error("AsyncProcessor.Run()", "Task name: " + evnt.GetType().FullName + " Exception: " + exceptionString);

                        }
                    }
                }
            }

        }
    }
}
