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
using System.Threading;
using SimpleLoadTest;
using System.Xml;


namespace SimpleLoadTest
{
    public abstract class BasicSetGetMessageRequest<T> : UserRequest where T : UserRequest
    {
        public Guid UserID = Guid.NewGuid();

        private string      _doingStep;

        private static bool _canSet;
        private static bool _canGet;
        private static long _getTicks;
        private static long _setTicks;
        private static long _getIterations;
        private static long _setIterations;
        private static long _oldGetIterations;
        private static long _oldSetIterations;
        private static long _oldGetMeteredTicks;
        private static long _oldSetMeteredTicks;
        private static long _startingGetMeteredTicks;
        private static long _startingSetMeteredTicks;

        public bool CanGet
        {
            get
            {
                return _canGet;
            }
        }

        public bool CanSet
        {
            get
            {
                return _canSet;
            }
        }

        public static double AverageGetTime
        {
            get
            {
                if (_getIterations > 0)
                {
                    var totalMS = StopwatchHelpers.GetElapsedMS(0, _getTicks);
                    return totalMS / _getIterations;
                }
                else
                {
                    return double.NaN;
                }
            }
        }

        public static double AverageSetTime
        {
            get
            {
                if (_setIterations > 0)
                {
                    var totalMS = StopwatchHelpers.GetElapsedMS(0, _setTicks);
                    return totalMS / _setIterations;
                }
                else
                {
                    return double.NaN;
                }
            }
        }

        public static double AverageGetsPerSecond
        {
            get
            {
                var getMeteredTicks = StopwatchHelpers.GetTicks();

                if (_startingGetMeteredTicks > 0)
                {
                    var totalS = StopwatchHelpers.GetElapsedMS(_startingGetMeteredTicks, getMeteredTicks) / 1000;
                    return _getIterations / totalS;
                }
                else
                {
                    return double.NaN;
                }
            }
        }

        public static double AverageSetsPerSecond
        {
            get
            {
                var setMeteredTicks = StopwatchHelpers.GetTicks();

                if (_startingSetMeteredTicks > 0)
                {
                    var totalS = StopwatchHelpers.GetElapsedMS(_startingSetMeteredTicks, setMeteredTicks) / 1000;
                    return _setIterations / totalS;
                }
                else
                {
                    return double.NaN;
                }
            }
        }

        public static double GetsPerSecond
        {
            get
            {
                if (_getIterations > 0)
                {
                    var getMeteredTicks = StopwatchHelpers.GetTicks();
                    var getIterations   = _getIterations;
                    var totalS          = StopwatchHelpers.GetElapsedMS(_oldGetMeteredTicks, getMeteredTicks) / 1000.0;
                    var tps             = (getIterations - _oldGetIterations) / totalS;
                    _oldGetIterations   = getIterations;
                    _oldGetMeteredTicks = getMeteredTicks;

                    return tps;
                }
                else
                {
                    return double.NaN;
                }
            }
        }

        public static double SetsPerSecond
        {
            get
            {
                if (_setIterations > 0)
                {
                    var setMeteredTicks = StopwatchHelpers.GetTicks();
                    var setIterations   = _setIterations;
                    var totalS          = StopwatchHelpers.GetElapsedMS(_oldSetMeteredTicks, setMeteredTicks) / 1000.0;
                    var tps             = (setIterations - _oldSetIterations) / totalS;
                    _oldSetIterations   = setIterations;
                    _oldSetMeteredTicks = setMeteredTicks;
                    
                    return tps;
                }
                else
                {
                    return double.NaN;
                }
            }
        }

        public static string GetStartTestPacket(DateTime    dateTime, 
                                                string      className,
                                                string      scenario,
                                                string      connectionString, 
                                                int         messageMin, 
                                                int         messageMax, 
                                                int         totalRequests, 
                                                int         requestRate, 
                                                int         workers,
                                                string      targetIP = "unused",
                                                string      userData = "unused",
                                                string      triggerIP = "unused")
        {            
            return string.Format("S<Test time=\"{0}\" targetIP=\"{1}\"><Class>{2}</Class><Scenario>{3}</Scenario><Connection>{4}</Connection>" +
                                 "<MessageMin>{5}</MessageMin><MessageMax>{6}</MessageMax><TotalRequests>{7}</TotalRequests><RequestRate>{8}</RequestRate>" + 
                                 "<Workers>{9}</Workers><UserData>{10}</UserData><TriggerIP>{11}</TriggerIP></Test>", 
                                 dateTime, targetIP, className, scenario, connectionString, 
                                 messageMin, messageMax, totalRequests, requestRate, 
                                 workers, userData, triggerIP);
        }

        public static string GetFinishPacket(DateTime   dateTime, 
                                             string     className, 
                                             string     targetIP = "unused")
        {
            return string.Format("F<Test time=\"{0}\" targetIP=\"{1}\"><Class>{2}</Class></Test>", 
                                 dateTime, targetIP, className);
        }

        public static string GetUpdatePacket(DateTime   dateTime, 
                                             string     className,
                                             int        messageMin, 
                                             int        messageMax, 
                                             int        totalRequests, 
                                             int        requestRate, 
                                             string     targetIP = "unused")
        {
            return string.Format("U<Test time=\"{0}\" targetIP=\"{1}\"><Class>{2}</Class><MessageMin>{3}</MessageMin>" +
                                 "<MessageMax>{4}</MessageMax><TotalRequests>{5}</TotalRequests><RequestRate>{6}</RequestRate></Test>", 
                                 dateTime, targetIP, className, messageMin, messageMax, totalRequests, requestRate);
        }

        public static void ParseFeedbackPacket(string data, out string className, out DateTime time, out bool finished, Dictionary<string, double> metricsLookup)
        {
            var doc = new XmlDocument();
            doc.LoadXml(data);

            var testNode = doc.SelectSingleNode("Test");
            time         = DateTime.Parse(testNode.Attributes[0].Value);
            finished     = bool.Parse(testNode.Attributes[1].Value);
            
            var classNode    = doc.SelectSingleNode("Test/Class");
            className = classNode.InnerText;

            var metricNodeList = doc.SelectNodes("Test/Metric");

            if (metricNodeList != null)
            {
                for (int i = 0; i < metricNodeList.Count; i++)
                {
                    var metricNode = metricNodeList[i];
                    var name       = metricNode.Attributes[0].Value;
                    var value      = double.Parse(metricNode.InnerText);

                    metricsLookup[name] = value;
                }
            }
        }

        public override Dictionary<string, double> GetMetrics()
        {
            var metricsLookup                     = new Dictionary<string, double>();
            metricsLookup["GetIterations"]        = _getIterations;
            metricsLookup["SetIterations"]        = _setIterations;            
            metricsLookup["GetTicks"]             = _getTicks;
            metricsLookup["SetTicks"]             = _setTicks;
            metricsLookup["AverageGetTime"]       = AverageGetTime;
            metricsLookup["AverageSetTime"]       = AverageSetTime;         
            metricsLookup["GetsPerSecond"]        = GetsPerSecond;
            metricsLookup["SetsPerSecond"]        = SetsPerSecond;
            metricsLookup["AverageGetsPerSecond"] = AverageGetsPerSecond;
            metricsLookup["AverageSetsPerSecond"] = AverageSetsPerSecond;

            return metricsLookup;
        }

        public override void PrepForTest()
        {
            _getTicks                = 0;
            _setTicks                = 0;
            _getIterations           = 0;
            _setIterations           = 0;
            _oldGetIterations        = 0;
            _oldSetIterations        = 0;
            _oldGetMeteredTicks      = StopwatchHelpers.GetTicks();
            _oldSetMeteredTicks      = StopwatchHelpers.GetTicks();
            _startingGetMeteredTicks = _oldGetMeteredTicks;
            _startingSetMeteredTicks = _oldSetMeteredTicks;

            if (SimpleLoadTestSettings<T>.Scenario.ToLower().Contains("set") == true)
            {
                _canSet = true;
            }
            else
            {
                _canSet = false;
            }

            if (SimpleLoadTestSettings<T>.Scenario.ToLower().Contains("get") == true)
            {
                _canGet = true;
            }
            else
            {
                _canGet = false;
            }        
        }

        public virtual object Connect(object connection)
        {
            return null;
        }

        public abstract void Get(object connection);
        public abstract void Set(object connection);

        public override void OverrideSettings()
        {
            SimpleLoadTestSettings<T>.RequestMode = RequestModes.ByMessage;
        }

        public override object DoRequest(object connection)
        {
            _doingStep = "Initiating Channel";
            connection = Connect(connection);

            if (_canSet == true)
            {
                _doingStep = "Set";
                _set(connection);
            }

            if (_canGet == true)
            {
                _doingStep = "Get";
                _get(connection);
            }

            return connection;
        }

        public override void ExceptionThrown(string exceptionMessage)
        {
            base.ExceptionThrown(exceptionMessage + string.Format(" [DoingStep: {0} Key: {1}]", _doingStep, UserID));
        }

        private void _get(object connection)
        {
            long startTicks = StopwatchHelpers.GetTicks();

            Get(connection);

            long endTicks     = StopwatchHelpers.GetTicks();
            long ticksElapsed = endTicks - startTicks;

            Interlocked.Increment(ref _getIterations);            
            Interlocked.Add(ref _getTicks, ticksElapsed);            
        }

        private void _set(object connection)
        {            
            long startTicks = StopwatchHelpers.GetTicks();

            Set(connection);

            long endTicks     = StopwatchHelpers.GetTicks();
            long ticksElapsed = endTicks - startTicks;

            Interlocked.Increment(ref _setIterations);
            Interlocked.Add(ref _setTicks, ticksElapsed);
        }
    }
}
