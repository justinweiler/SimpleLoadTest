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
using System.Text;
using System.Threading;


namespace SimpleLoadTest
{
    public interface ISimpleLoadTest
    {
        void Stop();
        void DoTest(string testDescription = null, bool pause = false);
        void OutputSettingsToConsole();
        ISimpleLoadTestSettings Settings { get; }
    }

    public class SimpleLoadTest<T> : ISimpleLoadTest where T : UserRequest, new()
    {
        private List<UserRequest>             _userRequestList = new List<UserRequest>();
        private Dictionary<int, object>       _threadStorage   = new Dictionary<int, object>();
        private WorkerThreadPool<UserRequest> _wtp;
        private DateTime                      _lastMeteredTime;
        private string                        _testDescription;
        private Thread                        _feederThread;

        public SimpleLoadTest()
        {
            try
            {
                var dummyInstance = new T();
                dummyInstance.OverrideSettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public ISimpleLoadTestSettings Settings
        {
            get
            {
                return new SimpleLoadTestSettings<T>();
            }
        }

        public void Stop()
        {
            try
            {
                if (_feederThread != null)
                {
                    _feederThread.Abort();
                }

                if (string.IsNullOrWhiteSpace(_testDescription) == false)
                {
                    Console.WriteLine("Test {0} has been stopped: {1}", _testDescription, DateTime.Now);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            SimpleLoadTestCNCBase.TellIdle();

            _feederThread = null;
        }

        public void DoTest(string testDescription = null, bool pause = false)
        {
            try
            {
                _testDescription = testDescription;

                _threadStorage = new Dictionary<int,object>();

                if (string.IsNullOrWhiteSpace(_testDescription) == false)
                {
                    Console.WriteLine("Test {0} has started: {1}", _testDescription, DateTime.Now);
                }

                // optional TellWait here

                SimpleLoadTestCNCBase.TellPrep();

                var dummyInstance = new T();
                dummyInstance.PrepForTest();

                _wtp = new WorkerThreadPool<UserRequest>(SimpleLoadTestSettings<T>.Workers, _workStartCallback, null, _workCompleteCallback, SimpleLoadTestSettings<T>.MaxAllowedLatency, null, null);
                _wtp.Open();

                SimpleLoadTestCNCBase.TellRun();

                var threadProc = SimpleLoadTestSettings<T>.RequestMode == RequestModes.ByMessage ? new Action(_feederProcRequests) : new Action(_feederProcUsers);
                _feederThread  = new Thread(new ThreadStart(threadProc));
                _feederThread.Start();

                _feederThread.Join();
                _feederThread = null;

                _wtp.Close();
                _wtp = null;

                foreach (var threadStorageObject in _threadStorage.Values)
                {
                    if (threadStorageObject is IDisposable)
                    {
                        (threadStorageObject as IDisposable).Dispose();
                    }
                }
                
                _threadStorage = null;

                if (string.IsNullOrWhiteSpace(_testDescription) == false)
                {
                    Console.WriteLine("Test {0} has completed: {1}", _testDescription, DateTime.Now);
                }

                SimpleLoadTestCNCBase.TellStop();

                dummyInstance.FinishTest();

                if (pause == true)
                {
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            SimpleLoadTestCNCBase.TellIdle();
        }

        public void OutputSettingsToConsole()
        {
           SimpleLoadTestSettings<T>.OutputSettingsToConsole();
        }

        private void _feederProcRequests()
        {
            try
            {
                var startTicks   = StopwatchHelpers.GetTicks();
                var numRequests  = SimpleLoadTestSettings<T>.TotalRequests;
                _lastMeteredTime = DateTime.Now;

                while (numRequests > 0)
                {
                    if (_wtp.GetAvailableCapacity() > 0)
                    {
                        var request = new T();
                        request.Reset<T>(StopwatchHelpers.GetTicks());

                        request.RequestState = UserRequest.State.InProgress;
                        _wtp.Enqueue(request);

                        numRequests--;
                    }
                    else
                    {
                        Thread.Yield();
                    }

                    _getMetrics(false);
                }

                while (_wtp.GetPendingWorkItems() > 0)
                {
                    Thread.Yield();
                    _getMetrics(false);
                }

                Thread.Sleep(10000);
                _getMetrics(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void _feederProcUsers()
        {
            try
            {
                long startTicks  = StopwatchHelpers.GetTicks();
                _lastMeteredTime = DateTime.Now;

                while (StopwatchHelpers.GetElapsedMS(startTicks, StopwatchHelpers.GetTicks()) < SimpleLoadTestSettings<T>.Duration * 1000)
                {
                    long nowTicks = StopwatchHelpers.GetTicks();

                    if (_userRequestList.Count < SimpleLoadTestSettings<T>.Users)
                    {
                        double nowMS          = StopwatchHelpers.GetElapsedMS(startTicks, nowTicks);
                        double percentEngaged = nowMS / (SimpleLoadTestSettings<T>.RampUp * 1000);
                        int numUsersRequired  = (int)((double)SimpleLoadTestSettings<T>.Users * percentEngaged);

                        if (numUsersRequired <= SimpleLoadTestSettings<T>.Users)
                        {
                            for (int i = _userRequestList.Count; i < numUsersRequired; i++)
                            {
                                var request = new T();
                                request.Reset<T>(nowTicks);
                                _userRequestList.Add(request);
                            }
                        }
                    }

                    foreach (var request in _userRequestList)
                    {
                        if (_wtp.GetAvailableCapacity() > 0)
                        {
                            if (request.RequestState == UserRequest.State.Pending && StopwatchHelpers.GetElapsedMS(request.WaitStartTicks, nowTicks) > request.WaitUntilMS)
                            {
                                request.RequestState = UserRequest.State.InProgress;
                                _wtp.Enqueue(request);
                            }
                            else if (request.RequestState == UserRequest.State.Done)
                            {
                                request.Reset<T>(nowTicks);
                            }
                        }
                        else
                        {
                            break;
                        }
                    }

                    Thread.Yield();
                    _getMetrics(false);
                }

                Thread.Sleep(10000);
                _getMetrics(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void _getMetrics(bool finished)
        {
            if (DateTime.Now - _lastMeteredTime >= TimeSpan.FromSeconds(5) || finished == true)
            {
                var dummyInstance = new T();
                var metricsLookup = dummyInstance.GetMetrics();

                if (metricsLookup != null)
                {
                    StringBuilder xmlBuilder = new StringBuilder();

                    Console.WriteLine("Meter Time : {0}", DateTime.Now);
               
                    foreach (var kvp in metricsLookup)
                    {
                        Console.WriteLine("{0} : {1}", kvp.Key, kvp.Value);

                        if (SimpleLoadTestSettings<T>.IsSlaveMode == true)
                        {
                            xmlBuilder.Append(string.Format("<Metric name=\"{0}\">{1}</Metric>", kvp.Key, kvp.Value));
                        }
                    }

                    if (xmlBuilder.Length > 0)
                    {
                        var xmlPacket = string.Format("M<Test time=\"{0}\" finished=\"{1}\"><Class>{2}</Class>{3}</Test>", DateTime.UtcNow, finished, typeof(T).Name, xmlBuilder);
                        UDPCommManager.SquawkMulticastPacket(SimpleLoadTestSettings<T>.MulticastIP, xmlPacket);
                    }

                    if (finished == true)
                    {
                        Console.WriteLine("Test is ending");
                    }
                }

                _lastMeteredTime = DateTime.Now;
            }
        }

        private void _workStartCallback(UserRequest request)
        {
            object threadInfo = null;

            lock (_threadStorage)
            {
                if (_threadStorage.ContainsKey(Thread.CurrentThread.ManagedThreadId) == false)
                {
                    _threadStorage[Thread.CurrentThread.ManagedThreadId] = null;
                }

                threadInfo = _threadStorage[Thread.CurrentThread.ManagedThreadId];
            }

            request.WorkStartTicks = StopwatchHelpers.GetTicks();

            threadInfo = request.DoRequest(threadInfo);

            lock (_threadStorage)
            {
                _threadStorage[Thread.CurrentThread.ManagedThreadId] = threadInfo;
            }

            request.RequestState = UserRequest.State.Done;
        }

        private void _workCompleteCallback(bool exceptionThrown, UserRequest request, string exceptionMessage)
        {
            request.WorkEndTicks = StopwatchHelpers.GetTicks();

            if (exceptionThrown == true)
            {
                request.ExceptionThrown(exceptionMessage);
            }

            if (SimpleLoadTestSettings<T>.RequestRate > 0)
            {
                var workCostMS = StopwatchHelpers.GetElapsedMS(request.WorkStartTicks, request.WorkEndTicks);
                var needMS     = (double)(1000 * SimpleLoadTestSettings<T>.Workers) / (double)SimpleLoadTestSettings<T>.RequestRate;

                if (needMS > workCostMS)
                {
                    while (true)
                    {
                        var nowTicks = StopwatchHelpers.GetTicks();

                        if (StopwatchHelpers.GetElapsedMS(request.WorkStartTicks, nowTicks) >= needMS)
                        {
                            break;
                        }

                        Thread.Yield();
                    }
                }
            }
        }
    }    
}
