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
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SimpleLoadTest;
using BasicDBLoadTestGUI.Properties;


namespace BasicDBLoadTestGUI
{
    public class TesterSettings
    {
        public static TestWindow ParentWindow;

        // methods
        public const string SQL           = "SQL";
        public const string HTTP          = "HTTP";

        // request classes
        public const string SQLREQ        = "SQLRequest";
        public const string HTTPREQ       = "HttpRequest";

        // actions
        public const string NONE          = "None";
        public const string START         = "Start";
        public const string UPDATE        = "Update";
        public const string FINISH        = "Finish";
        
        // statii
        public const string INIT          = "init";
        public const string IDLE          = "idle";
        public const string LOST          = "lost";
        public const string RUN           = "run";
        public const string ABORT         = "abort";
        public const string DONE          = "done";
        public const string UNKNOWN       = "unknown";

        // default context
        public const string CONTEXT       = "customer";
        
        // scenarios
        public const string SETGET        = "SETGET";
        public const string SET           = "SET";
        public const string GET           = "GET";

        public static string SqlConxnString;

        private static List<string> _testMethods                 = new List<string>(){ SQL, HTTP };
        private static List<string> _testScenarios               = new List<string>(){ SETGET, SET, GET };
        private static List<string> _notRunningActions           = new List<string>(){ NONE, START };
        private static List<string> _runningActions              = new List<string>(){ NONE, FINISH, UPDATE };

        private static Dictionary<string,string> _method2Request = new Dictionary<string, string>()
                                                                    {
                                                                        { SQL, SQLREQ },
                                                                        { HTTP, HTTPREQ },
                                                                    };

        private string _method = null;
        private string _connection;
        private string _status;
        private string _scenario;
        private int    _threads;

        public static string GetRequestType(string method)
        {
            return _method2Request[method];
        }

        public string TesterIP 
        { 
            get; 
            set; 
        }

        public string Timestamp 
        { 
            get; 
            set; 
        }

        public int Items 
        { 
            get; 
            set; 
        }

        public int MaxSize 
        { 
            get; 
            set; 
        }

        public int MinSize 
        { 
            get; 
            set; 
        }

        public int Rate 
        { 
            get; 
            set; 
        }

        public string Action 
        { 
            get; 
            set; 
        }

        public string Started
        { 
            get; 
            set; 
        }

        public string Finished
        { 
            get; 
            set; 
        }

        public string Transactions
        { 
            get; 
            set; 
        }

        public string TPS
        { 
            get; 
            set; 
        }

        public string GetLatency
        { 
            get; 
            set; 
        }

        public string SetLatency
        { 
            get; 
            set; 
        }

        public int Threads 
        {
            get
            {
                return _threads;
            }
            set
            {
                if (IsStarted == false)
                {
                    _threads = value;
                }
            }
        }

        public string Connection 
        {
            get
            {
                return _connection;
            }
            set
            {
                if (IsStarted == false)
                {
                    _connection = value;
                }
            }
        }

        public string Status 
        {
            get
            {
                return (DateTime.Parse(Timestamp) + TimeSpan.FromSeconds(30) < DateTime.Now) ? LOST : _status;
            }
            set
            {
                _status = value;
            }
        }

        public Brush Color
        {
            get
            {
                return (DateTime.Parse(Timestamp) + TimeSpan.FromSeconds(30) < DateTime.Now) ? Brushes.Red : Brushes.Black;
            }
        }

        public string Scenario
        {
            get
            {
                return _scenario;
            }
            set
            {
                if (IsStarted == false)
                {
                    _scenario = value;
                }
            }
        }

        public string Method 
        {
            get
            {
                return _method;
            }
            set
            {
                if (IsStarted == false || _method == null)
                {
                    if (value != _method)
                    {
                        if (value == SQL)
                        {
                            Connection = SqlConxnString;
                        }
                        else if (value == HTTP)
                        {
                            Connection = "http://localhost/get";
                        }
                        else
                        {
                            Connection = string.Empty;
                        }
                    }

                    _method = value;
                }
            }
        }

        public List<string> MethodList
        {
            get
            {
                return _testMethods;
            }
        }

        public List<string> ScenarioList
        {
            get
            {
                return _testScenarios;
            }
        }

        public List<string> ActionList
        {
            get 
            { 
                return (Status == IDLE) ? _notRunningActions : _runningActions; 
            }
        }

        public bool IsStarted
        {
            get
            {
                return Status != IDLE && Status != INIT;
            }
        }
    }
}
