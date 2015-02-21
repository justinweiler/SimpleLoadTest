/*
    Copyright (C) 2012 by Justin Robert Weiler

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using ThreadState=System.Threading.ThreadState;


#pragma warning disable 612,618 

namespace SimpleLoadTest
{
    public class WorkerThreadPool<TWorkerState> where TWorkerState : class
    {
        #region Delegates

        public delegate long GetCurrentPriorityCallback(long currentPriority);
        public delegate void WorkerCallback(TWorkerState state);
        public delegate void WorkStartedCallback(TWorkerState state);
        public delegate void WorkCompleteCallback(bool exceptionThrown, TWorkerState state, string exceptionMessage);

        #endregion

        private readonly List<WaitHandle>       _asleepHandles = new List<WaitHandle>();
        private readonly AutoResetEvent         _newItem = new AutoResetEvent(false);
        private readonly Queue<TWorkerState>    _workerStateQueue = new Queue<TWorkerState>();
        private readonly List<_workerThread>    _workerThreadList = new List<_workerThread>();
        private bool                            _adhereToStrictPriorityRules;
        private double                          _currentDispatchRate;
        private long                            _currentPriority;
        private Thread                          _dispatcher;
        private Thread                          _dispatcherSleepBuddy;
        private bool                            _exiting;
        private GetCurrentPriorityCallback      _getCurrentPriorityCallback;
        private bool                            _isPrioritized;
        private long                            _lastRateCheckTicks = StopwatchHelpers.GetTicks();
        private int                             _maxThreads;
        private long                            _numberDispatched;
        private int                             _peakUsage;
        private int                             _pendingCap = int.MaxValue;
        private int                             _priorityCheckInterval = 100;

        #region Public Implementation

        public WorkerThreadPool(int maxThreads, WorkerCallback workerCallback, WorkStartedCallback workStartedCallback, WorkCompleteCallback workCompleteCallback, int? responseTimeout, ThreadPriority? threadPriority, int? pendingCap)
        {
            _workerThreadPool(maxThreads, workerCallback, workStartedCallback, workCompleteCallback, responseTimeout, threadPriority, pendingCap, null);
        }

        public WorkerThreadPool(int maxThreads, WorkerCallback workerCallback, WorkStartedCallback workStartedCallback, WorkCompleteCallback workCompleteCallback, int? responseTimeout, ThreadPriority? threadPriority, int? pendingCap, Queue<TWorkerState> preLoad)
        {
            _workerThreadPool(maxThreads, workerCallback, workStartedCallback, workCompleteCallback, responseTimeout, threadPriority, pendingCap, preLoad);
        }

        ~WorkerThreadPool()
        {
            if (_newItem != null)
                _newItem.Close();

            for (int i = 0; i < _asleepHandles.Count; i++)
            {
                WaitHandle asleepHandle = _asleepHandles[i];

                if (asleepHandle != null)
                    asleepHandle.Close();

                _asleepHandles[i] = null;
            }
        }

        public GetCurrentPriorityCallback CurrentPriorityCallback
        {
            get { return _getCurrentPriorityCallback; }
            set { _getCurrentPriorityCallback = value; }
        }

        public long CurrentPriority
        {
            get { return _currentPriority; }
        }

        public int PriorityCheckInterval
        {
            get
            {
                return _priorityCheckInterval;
            }
            set
            {
                _priorityCheckInterval = value;
            }
        }

        /// <summary>
        ///  if you have pre-loaded work, call this function to start processing
        /// </summary>
        public void Open()
        {
            if (_workerStateQueue.Count > 0)
                _newItem.Set();
        }

        /// <summary>
        /// Call this function to shut down
        /// </summary>
        public void Close()
        {
            Purge();

            try
            {
                Debug.WriteLine("[WorkerThreadPool] Closing WorkerThreadPool");

                _exiting = true;

                if (_adhereToStrictPriorityRules == true)
                {
                    _dispatcherSleepBuddy.Interrupt();
                    _dispatcherSleepBuddy.Join(10000);
                }

                _dispatcher.Interrupt();
                _dispatcher.Join(10000);

                foreach (_workerThread workerThread in _workerThreadList)
                    workerThread.close();

                foreach (_workerThread workerThread in _workerThreadList)
                    workerThread.join();

                foreach (_workerThread workerThread in _workerThreadList)
                    workerThread.dispose();

                _workerThreadList.Clear();
            }
            catch (Exception ex)
            {
                if (!(ex is ThreadInterruptedException))
                    Debug.WriteLine("[WorkerThreadPool] " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message);
            }
        }

        public int GetPendingWorkItems()
        {
            bool lockTaken = false;

            try
            {
                Monitor.Enter(_workerStateQueue, ref lockTaken);

                return _workerStateQueue.Count;
            }
            finally
            {
                if (lockTaken == true)
                {
                    Monitor.Exit(_workerStateQueue);
                }
            }
        }

        public TWorkerState[] GetPendingWorkItemsList()
        {
            TWorkerState[] response = null;
            bool lockTaken = false;

            try
            {
                Monitor.Enter(_workerStateQueue, ref lockTaken);

                response = new TWorkerState[_workerStateQueue.Count];
                _workerStateQueue.CopyTo(response, 0);
            }
            finally
            {
                if (lockTaken == true)
                {
                    Monitor.Exit(_workerStateQueue);
                }
            }

            return response;
        }

        public int GetTotalCapacity()
        {
            return _pendingCap;
        }

        public int GetAvailableCapacity()
        {
            bool lockTaken = false;

            try
            {
                Monitor.Enter(_workerStateQueue, ref lockTaken);

                return _pendingCap - _workerStateQueue.Count;
            }
            finally
            {
                if (lockTaken == true)
                {
                    Monitor.Exit(_workerStateQueue);
                }
            }
        }

        public int GetPeakUsage()
        {
            return _peakUsage;
        }

        public int GetTotalThreads()
        {
            return _workerThreadList.Count;
        }

        // 0: running, 1: blocked, 2: suspended, 3: total
        public int[] GetAllThreads()
        {
            int[] totals = new int[4] {0, 0, 0, 0};

            for (int i = _workerThreadList.Count - 1; i >= 0; i--)
            {
                _workerThread workerThread = _workerThreadList[i];
                
                if (workerThread.state == ThreadState.Running)
                    totals[0]++;

                if (workerThread.state == ThreadState.WaitSleepJoin)
                    totals[1]++;

                if (workerThread.state == ThreadState.Suspended)
                    totals[2]++;
            }

            totals[3] = _workerThreadList.Count;

            return totals;
        }

        public int GetActiveThreads()
        {
            int total = 0;

            for (int i = _workerThreadList.Count - 1; i >= 0; i--)
            {
                _workerThread workerThread = _workerThreadList[i];
                
                if (workerThread.state == ThreadState.Running)
                    total++;
            }

            return total;
        }

        public int GetBlockedThreads()
        {
            int total = 0;

            for (int i = _workerThreadList.Count - 1; i >= 0; i--)
            {
                _workerThread workerThread = _workerThreadList[i];

                if (workerThread.state == ThreadState.WaitSleepJoin)
                    total++;
            }

            return total;
        }

        public int GetSuspendedThreads()
        {
            int total = 0;

            for (int i = _workerThreadList.Count - 1; i >= 0; i--)
            {
                _workerThread workerThread = _workerThreadList[i];

                if (workerThread.state == ThreadState.Suspended)
                    total++;
            }

            return total;
        }

        public int[] GetActiveThreadIds()
        {
            var activeIDs = new List<int>();

            for (int i = _workerThreadList.Count - 1; i >= 0; i--)
            {
                _workerThread workerThread = _workerThreadList[i];

                if (workerThread.state == ThreadState.Running)
                    activeIDs.Add(workerThread.id);
            }

            return activeIDs.ToArray();
        }

        public double GetCurrentDispatchRate()
        {
            return _currentDispatchRate;
        }

        /// <summary>
        /// Enqueues the ws.
        /// </summary>
        /// <param name="newWorkerState">the worker state to enqueue</param>
        /// <returns>bool</returns>
        public bool Enqueue(TWorkerState newWorkerState)
        {   
            bool lockTaken = false;

            try
            {
                Monitor.Enter(_workerStateQueue, ref lockTaken);

                return _enqueue(newWorkerState, false);
            }
            catch (Exception ex)
            {
                if (!(ex is ThreadInterruptedException))
                    Debug.WriteLine("[WorkerThreadPool] " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message);

                return false;
            }
            finally
            {
                if (lockTaken == true)
                {
                    Monitor.Exit(_workerStateQueue);
                }
            }
        }

        /// <summary>
        /// Trys to replace the ws first, then enqueues the ws if there was no replacement.
        /// </summary>
        /// <param name="newWorkerState">the worker state to enqueue</param>
        /// <returns>bool</returns>
        public bool EnqueueReplace(TWorkerState newWorkerState)
        {
            bool lockTaken = false;

            try
            {
                Monitor.Enter(_workerStateQueue, ref lockTaken);

                return _enqueue(newWorkerState, true);
            }
            catch (Exception ex)
            {
                if (!(ex is ThreadInterruptedException))
                    Debug.WriteLine("[WorkerThreadPool] " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message);

                return false;
            }
            finally
            {
                if (lockTaken == true)
                {
                    Monitor.Exit(_workerStateQueue);
                }
            }
        }

        /// <summary>
        /// removes all ws from queue
        /// </summary>
        public void Purge()
        {
            bool lockTaken = false;

            try
            {
                Monitor.Enter(_workerStateQueue, ref lockTaken);

                _workerStateQueue.Clear();
            }
            catch (Exception ex)
            {
                if (!(ex is ThreadInterruptedException))
                    Debug.WriteLine("[WorkerThreadPool] " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message);
            }
            finally
            {
                if (lockTaken == true)
                {
                    Monitor.Exit(_workerStateQueue);
                }
            }
        }

        /// <summary>
        /// Removes the ws if found
        /// </summary>
        /// <param name="workerState">the worker state to remove</param>
        /// <returns>bool</returns>
        public bool Remove(TWorkerState workerState)
        {
            bool lockTaken = false;

            try
            {
                Monitor.Enter(_workerStateQueue, ref lockTaken);

                return _remove(workerState);
            }
            catch (Exception ex)
            {
                if (!(ex is ThreadInterruptedException))
                    Debug.WriteLine("[WorkerThreadPool] " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message);

                return false;
            }
            finally
            {
                if (lockTaken == true)
                {
                    Monitor.Exit(_workerStateQueue);
                }
            }
        }

        /// <summary>
        ///  Peeks (does not remove)
        /// </summary>
        /// <returns>TWorkerState</returns>
        public TWorkerState Peek()
        {
            bool lockTaken = false;

            try
            {
                Monitor.Enter(_workerStateQueue, ref lockTaken);

                if (_workerStateQueue.Count == 0)
                    return null;

                TWorkerState workerState = _workerStateQueue.Peek();
                return workerState;
            }
            catch (Exception ex)
            {
                if (!(ex is ThreadInterruptedException))
                    Debug.WriteLine("[WorkerThreadPool] " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message);

                return null;
            }
            finally
            {
                if (lockTaken == true)
                {
                    Monitor.Exit(_workerStateQueue);
                }
            }
        }

        #endregion

        #region Private Implementation

        private void _workerThreadPool(int maxThreads, WorkerCallback workerCallback, WorkStartedCallback workStartedCallback, WorkCompleteCallback workCompleteCallback, int? responseTimeout, ThreadPriority? threadPriority, int? pendingCap, Queue<TWorkerState> preLoad)
        {
            Debug.WriteLine("[WorkerThreadPool] Initializing WorkerThreadPool");

            _adhereToStrictPriorityRules = typeof(TWorkerState).GetInterface("IPrioritizeStrict") != null;
            _isPrioritized               = typeof(TWorkerState).GetInterface("IPrioritize") != null;
            _maxThreads                  = maxThreads;

            if (_maxThreads <= 0 || _maxThreads > 100)
                throw new Exception("WorkerThreadPool: Maximum Threads must be between 1 - 100");

            if (pendingCap != null)
                _pendingCap = (int)pendingCap;

            if (preLoad != null)
            {
                for (int i = 0; i < preLoad.Count; i++)
                {
                    TWorkerState dequeuedWorkerState = preLoad.Dequeue();
                    _workerStateQueue.Enqueue(dequeuedWorkerState);
                    preLoad.Enqueue(dequeuedWorkerState);
                }
            }

            for (int i = 0; i < _maxThreads; i++)
            {
                var workerThread = new _workerThread(workerCallback, workStartedCallback, workCompleteCallback, _dequeue, responseTimeout, threadPriority);
                _asleepHandles.Add(workerThread.asleep);
                _workerThreadList.Add(workerThread);
            }

            _dispatcher          = new Thread(_dispatchProc);
            _dispatcher.Name     = "WorkerThreadPool.Dispatcher";
            _dispatcher.Start();

            if (_adhereToStrictPriorityRules == true)
            {
                _dispatcherSleepBuddy = new Thread(_dispatcherSleepBuddyProc);
                _dispatcherSleepBuddy.Name = "WorkerThreadPool.DispatcherSleepBuddy";
                _dispatcherSleepBuddy.Start();
            }

            Thread.Sleep(1000);
        }

        private bool _remove(TWorkerState workerState)
        {
            bool found = false;

            if (workerState is IComparable<TWorkerState>)
            {
                int cnt = _workerStateQueue.Count;

                for (int i = 0; i < cnt; i++)
                {
                    TWorkerState dequeuedWorkerState = _workerStateQueue.Dequeue();
                    var comparableWorkerState = dequeuedWorkerState as IComparable<TWorkerState>;

                    if (comparableWorkerState != null && comparableWorkerState.CompareTo(workerState) == 0)
                    {
                        found = true;
                        continue; // found it... now lose it
                    }
                    else
                    {
                        _workerStateQueue.Enqueue(dequeuedWorkerState);
                    }
                }
            }

            return found;
        }

        private bool _enqueue(TWorkerState newWorkerState, bool doRemoveFirst)
        {
            if (doRemoveFirst == true)
                _remove(newWorkerState);

            if (_workerStateQueue.Count == _pendingCap)
                return false;

            bool added = false;

            if (_isPrioritized == true)
            {
                int cnt = _workerStateQueue.Count;

                var newPrioritizedWorkerState = newWorkerState as IPrioritize;

                long newPriority = long.MaxValue;

                if (newPrioritizedWorkerState != null)
                    newPriority = newPrioritizedWorkerState.GetPriority();

                for (int i = 0; i < cnt; i++)
                {
                    TWorkerState dequeuedWorkerState = _workerStateQueue.Dequeue();

                    if (added == false)
                    {
                        var prioritizedWorkerState = dequeuedWorkerState as IPrioritize;

                        if (prioritizedWorkerState != null)
                        {
                            long itemPriority = prioritizedWorkerState.GetPriority();

                            if (newPriority < itemPriority)
                            {
                                _workerStateQueue.Enqueue(newWorkerState);
                                added = true;
                            }
                        }
                    }

                    _workerStateQueue.Enqueue(dequeuedWorkerState);
                }

                if (newPriority < _currentPriority)
                    _currentPriority = newPriority;
            }

            if (added == false)
                _workerStateQueue.Enqueue(newWorkerState);

            if (_peakUsage < _workerStateQueue.Count)
                _peakUsage = _workerStateQueue.Count;

            _newItem.Set();

            return true;
        }

        private TWorkerState _dequeueAndLogRate()
        {
            long nowTicks = StopwatchHelpers.GetTicks();
            double dispatchRateCheckSample = StopwatchHelpers.GetElapsedMS(_lastRateCheckTicks, nowTicks);

            if (dispatchRateCheckSample >= 1000)
            {
                _currentDispatchRate = ((_numberDispatched + 1) * 1000) / dispatchRateCheckSample;
                _numberDispatched = 0;
                _lastRateCheckTicks = nowTicks;
            }
            else
            {
                _numberDispatched++;
            }

            return _workerStateQueue.Dequeue();
        }

        private TWorkerState _dequeue()
        {
            bool lockTaken = false;

            try
            {
                Monitor.Enter(_workerStateQueue, ref lockTaken);

                if (_workerStateQueue.Count == 0)
                    return null;

                if (_adhereToStrictPriorityRules == true)
                {
                    var workerState = _workerStateQueue.Peek() as IPrioritize;

                    if (workerState != null)
                    {
                        if (workerState.GetPriority() <= _currentPriority)
                            return _dequeueAndLogRate();
                    }

                    return null;
                }
                else
                {
                    return _dequeueAndLogRate();
                }
            }
            catch (Exception ex)
            {
                if (!(ex is ThreadInterruptedException))
                    Debug.WriteLine("[WorkerThreadPool] " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message);

                return null;
            }
            finally
            {
                if (lockTaken == true)
                {
                    Monitor.Exit(_workerStateQueue);
                }
            }
        }

        private void _dispatchProc()
        {
            Debug.WriteLine("[WorkerThreadPool] _dispatchProc running");

            int startAt = 0;

            while (_exiting == false)
            {
                try
                {
                    _newItem.WaitOne(10);

                    if (_exiting == true)
                        return;

                    if (_workerStateQueue.Count > 0)
                    {
                        int index = startAt;

                        for (int i = 0; i < _maxThreads; i++)
                        {
                            index = (startAt + i) % _maxThreads;

                            if (_workerStateQueue.Count == 0)
                                break;

                            if (_workerThreadList[index].state == ThreadState.Suspended)
                            {
                                TWorkerState workerStateData = _dequeue();

                                if (workerStateData != null)
                                {
                                    _workerThreadList[index].queueThread(workerStateData);
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }

                        startAt = index;
                    }
                    else
                    {
                        Thread.Yield();
                    }
                }
                catch (Exception ex)
                {
                    if (!(ex is ThreadInterruptedException))
                        Debug.WriteLine(MethodBase.GetCurrentMethod().Name + ": " + ex.Message);
                }
            }

            Debug.WriteLine("[WorkerThreadPool] _dispatchProc terminating");
        }

        private void _dispatcherSleepBuddyProc()
        {
            Debug.WriteLine("[WorkerThreadPool] _dispatcherSleepBuddyProc running");

            bool lockTaken;

            while (_exiting == false)
            {
                try
                {
                    WaitHandle.WaitAny(_asleepHandles.ToArray());

                    if (_exiting == true)
                        break;

                    lockTaken = false;
        
                    try
                    {
                        Monitor.Enter(_workerStateQueue, ref lockTaken);

                        int cnt = _workerStateQueue.Count;

                        long targetPriority = long.MaxValue;

                        if (_getCurrentPriorityCallback != null)
                            targetPriority = _getCurrentPriorityCallback(_currentPriority);

                        if (cnt > 0)
                        {
                            IEnumerator<TWorkerState> enmr = _workerStateQueue.GetEnumerator();

                            while (enmr.MoveNext() == true)
                            {
                                var workerState = enmr.Current as IPrioritize;

                                if (workerState != null)
                                {
                                    long itemPriority = workerState.GetPriority();

                                    if (itemPriority < targetPriority)
                                        targetPriority = itemPriority;
                                }
                            }

                            _currentPriority = targetPriority;
                            _newItem.Set();
                        }
                        else
                        {
                            _currentPriority = targetPriority;
                        }

                        Thread.Sleep(_priorityCheckInterval);
                    }
                    finally
                    {
                        if (lockTaken == true)
                        {
                            Monitor.Exit(_workerStateQueue);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!(ex is ThreadInterruptedException))
                        Debug.WriteLine(MethodBase.GetCurrentMethod().Name + ": " + ex.Message);
                }
            }

            Debug.WriteLine("[WorkerThreadPool] _dispatcherSleepBuddyProc terminating");
        }

        #endregion

        #region Nested type: _getMoreCallback

        private delegate TWorkerState _getMoreCallback();

        #endregion

        #region Nested type: _workerThread

        private class _workerThread
        {
            private readonly WorkerCallback         _workerCallback;
            private readonly WorkStartedCallback    _workStartedCallback;
            private readonly WorkCompleteCallback   _workCompleteCallback;
            private readonly _getMoreCallback       _getMore;
            private readonly ManualResetEvent       _threadAsleep = new ManualResetEvent(false);
            private readonly Timer                  _timer;
            private int                             _defaultResponseTimeout;
            private int                             _responseTimeout;
            private ThreadPriority                  _defaultThreadPriority;
            private ThreadPriority                  _threadPriority;
            private bool                            _exiting;
            private Thread                          _thread;
            private TWorkerState                    _workerStateData;

            internal _workerThread(WorkerCallback workerCallback, WorkStartedCallback workStartedCallback, WorkCompleteCallback workCompleteCallback, _getMoreCallback getMore, int? responseTimeout, ThreadPriority? threadPriority)
            {
                try
                {
                    _timer                  = new Timer(_workerTimeout, this, Timeout.Infinite, Timeout.Infinite);
                    _defaultResponseTimeout = responseTimeout ?? Timeout.Infinite;
                    _defaultThreadPriority  = threadPriority ?? ThreadPriority.Normal;
                    _workerCallback         = workerCallback;
                    _workStartedCallback    = workStartedCallback;
                    _workCompleteCallback   = workCompleteCallback;
                    _getMore                = getMore;
                    _thread                 = new Thread(_workerProc);
                    _thread.Name            = "WorkerThreadPool.Worker";

                    _thread.Start();
                }
                catch (Exception ex)
                {
                    if (!(ex is ThreadInterruptedException))
                        Debug.WriteLine("[WorkerThreadPool] " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message);
                }
            }

            internal WaitHandle asleep
            {
                get { return _threadAsleep; }
            }

            internal ThreadState state
            {
                get { return _thread.ThreadState; }
            }

            internal int id
            {
                get { return _thread.ManagedThreadId; }
            }

            internal void close()
            {
                try
                {
                    _exiting = true;
                    _wakeUp();
                    _thread.Interrupt();
                }
                catch (Exception ex)
                {
                    if (!(ex is ThreadInterruptedException))
                        Debug.WriteLine("[WorkerThreadPool] " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message);
                }
                finally
                {
                    _threadAsleep.Close();
                }
            }

            internal void join()
            {
                try
                {
                    _thread.Join(10000);
                }
                catch (Exception ex)
                {
                    if (!(ex is ThreadInterruptedException))
                        Debug.WriteLine("[WorkerThreadPool] " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message);
                }
            }

            internal void dispose()
            {
                if (_timer != null)
                    _timer.Dispose();
            }

            internal void queueThread(TWorkerState workerStateData)
            {
                try
                {
                    _workerStateData = workerStateData;
                    
                    var haveTimeout        = _workerStateData as IHaveTimeout;
                    var haveThreadPriority = _workerStateData as IHaveThreadPriority;
                    
                    if (haveTimeout != null)
                        _responseTimeout = haveTimeout.TimeoutMilliseconds;
                    else
                        _responseTimeout = _defaultResponseTimeout;

                    if (haveThreadPriority != null)
                        _threadPriority = haveThreadPriority.ThreadPriority;
                    else
                        _threadPriority = _defaultThreadPriority;

                    _wakeUp();
                }
                catch (Exception ex)
                {
                    if (!(ex is ThreadInterruptedException))
                        Debug.WriteLine("[WorkerThreadPool] " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message);
                }
            }

            private void _deepSleep()
            {
                try
                {
                    if (_thread.ThreadState != ThreadState.Suspended &&
                        _thread.ThreadState != ThreadState.SuspendRequested)
                    {
                        if (_thread.ThreadState == ThreadState.Running)
                        {
                            _threadAsleep.Set();                            
                            _thread.Suspend();
                        }
                        else
                        {
                            throw new Exception("Thread in strange state when attempting DeepSleep");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!(ex is ThreadInterruptedException))
                        Debug.WriteLine("[WorkerThreadPool] " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message);
                }
            }

            private void _wakeUp()
            {
                try
                {
                    if (_thread.ThreadState == ThreadState.Suspended)
                    {
                        _thread.Resume();
                        _threadAsleep.Reset();
                    }
                }
                catch (Exception ex)
                {
                    if (!(ex is ThreadInterruptedException))
                        Debug.WriteLine("[WorkerThreadPool] " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message);
                }
            }

            private void _workerTimeout(object obj)
            {
                try
                {
                    Debug.WriteLine(string.Format("[WorkerThreadPool] Worker {0} Timed Out", _thread.ManagedThreadId));
                    _thread.Abort();
                    
                    _thread          = new Thread(_workerProc);
                    _thread.Name     = "WorkerThreadPool.Worker";
                    _workerStateData = null;

                    _thread.Start();
                }
                catch (Exception ex)
                {
                    if (!(ex is ThreadInterruptedException))
                        Debug.WriteLine("[WorkerThreadPool] " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message);
                }
            }

            private void _workerProc(object tsd)
            {
                try
                {
                    _deepSleep();

                    while (_exiting == false)
                    {
                        try
                        {
                            if (_workerStateData != null)
                            {
                                // copy so that it doesnt get changed from underneath us
                                var workerStateData = _workerStateData;

                                try
                                {
                                    if (_workStartedCallback != null)
                                        _workStartedCallback(workerStateData);
                                }
                                catch (Exception ex)
                                {
                                    if (ex is ThreadAbortException)
                                        throw;

                                    Debug.WriteLine("[WorkerThreadPool] " + _workStartedCallback.Method.Name + ": " + ex.Message);
                                }

                                bool exceptionThrown    = false;
                                string exceptionMessage = null;

                                try
                                {
                                    _timer.Change(_responseTimeout, Timeout.Infinite);
                                    _thread.Priority = _threadPriority;
                                    _workerCallback(workerStateData);
                                }
                                catch (Exception ex)
                                {
                                    exceptionThrown  = true;
                                    exceptionMessage = ex.Message;

                                    if (ex is ThreadAbortException)
                                        throw;

                                    Debug.WriteLine("[WorkerThreadPool] " + _workerCallback.Method.Name + ": " + ex.Message);
                                }
                                finally
                                {
                                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                                    _threadPriority = ThreadPriority.Normal;

                                    try
                                    {
                                        if (_workCompleteCallback != null)
                                            _workCompleteCallback(exceptionThrown, workerStateData, exceptionMessage);
                                    }
                                    catch (Exception ex)
                                    {
                                        if (ex is ThreadAbortException)
                                            throw;

                                        Debug.WriteLine("[WorkerThreadPool] " + _workCompleteCallback.Method.Name + ": " + ex.Message);
                                    }
                                }
                            }

                            _workerStateData = _getMore();

                            if (_workerStateData == null)
                                _deepSleep();
                            else
                                queueThread(_workerStateData);
                        }
                        catch (Exception ex)
                        {
                            _workerStateData = null;

                            if (ex is ThreadAbortException)
                                throw;

                            if (!(ex is ThreadInterruptedException))
                                Debug.WriteLine("[WorkerThreadPool] " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!(ex is ThreadInterruptedException))
                    {
                        Debug.WriteLine(string.Format("[WorkerThreadPool] {0}({1}):{2}", MethodBase.GetCurrentMethod().Name,
                            Thread.CurrentThread.ManagedThreadId, ex.Message));
                    }
                }
            }
        }

        #endregion
    }
}
