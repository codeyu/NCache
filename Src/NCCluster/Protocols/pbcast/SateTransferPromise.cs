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
using System.Threading;

namespace Alachisoft.NGroups.Protocols.pbcast
{
    public class SateTransferPromise
    {
        /// <summary>The result of the request</summary>
        Object _result = null;
        /// <summary>Used to wait on the result</summary>
        Object _mutex = new Object();
        /// <summary>How many responses expected</summary>
        int _countExpected;
        /// <summary>How many responses received</summary>
        int _countReceived;

        bool _receiveFuther = true;

        public SateTransferPromise(int count)
        {
            this._countExpected = count;
        }

        /// <summary>
        /// If result was already submitted, returns it immediately, else blocks until
        /// results becomes available. 
        /// </summary>
        /// <param name="timeout">Maximum time to wait for result.</param>
        /// <returns>Promise result</returns>
        public Object WaitResult(long timeout)
        {
            Object ret = null;

            lock (_mutex)
            {
                if (_result != null && ((_countExpected == _countReceived) || (bool)_result == true))
                {
                    ret = _result;
                    _result = null;
                    return ret;
                }

                if (timeout <= 0)
                {
                    try
                    {
                        Monitor.Wait(_mutex);
                    }
                    catch (Exception ex)
                    {
                        Trace.error("Promise.WaitResult", "exception=" + ex.ToString());
                    }
                }
                else
                {
                    try
                    {
                        Monitor.Wait(_mutex, (int)timeout, true);
                    }
                    catch (Exception ex)
                    {
                        Trace.error("Promise.WaitResult:", "exception=" + ex.ToString());
                    }
                }



                // SAL: Cosider Trace
                if (_result != null && ((_countExpected == _countReceived) || (bool)_result == true))
                {
                    ret = _result;
                    _result = null;
                    return ret;
                }
                return null;
            }
        }

        /// <summary>
        /// Sets the result and notifies any threads waiting for it
        /// </summary>
        /// <param name="obj">Result of request</param>
        public void SetResult(Object obj)
        {
            lock (_mutex)
            {
                if (_receiveFuther)
                {
                    _result = obj;

                    if ((bool)_result == true)
                    {
                        _countReceived++;
                        //We got a positive answer, no need to accept results from other nodes.
                        _receiveFuther = false;
                        Monitor.PulseAll(_mutex);
                    }

                    _countReceived++;
                    if (_countExpected == _countReceived)
                    {
                        Monitor.PulseAll(_mutex);
                    }
                }
            }
        }


        /// <summary>
        /// Clears the result and causes all waiting threads to return
        /// </summary>
        public void Reset()
        {
            lock (_mutex)
            {
                _result = null;
                Monitor.PulseAll(_mutex);
            }
        }

        /// <summary>
        /// String representation of the result
        /// </summary>
        /// <returns>String representation of the result</returns>
        public override String ToString()
        {
            return "result=" + _result + " countReceived=" + _countReceived + " countExpected=" + _countExpected;
        }

        /// <summary>
        /// Checks whether all the nodes responded
        /// </summary>
        public bool AllResultsReceived()
        {
            return _countExpected == _countReceived;
        }

    }
}
