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
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SimpleLoadTest;


namespace BasicDBLoadTestGUI
{
    /// <summary>
    /// Interaction logic for TestWindow.xaml
    /// </summary>
    public partial class TestWindow : Window
    {        
        private Dictionary<string, TesterSettings>              _testerLookup     = new Dictionary<string, TesterSettings>();
        private Dictionary<string, Dictionary<string, double>>  _metricsLookup    = new Dictionary<string, Dictionary<string, double>>();
        private string                                          _sqlConxnString   = string.Empty;
        private ObservableCollection<TesterSettings>            _testerCollection = new ObservableCollection<TesterSettings>();
        private DispatcherTimer                                 _timer            = new DispatcherTimer();

        private string                                          _multicastIP;
        private bool                                            _manualCommit;
        private ulong                                           _totalOps;
        private double                                          _throughput;
        private bool                                            _isEditing;
        
        public TestWindow()
        {
            InitializeComponent();

            _multicastIP                  = Properties.Settings.Default.MulticastIP;
            TesterSettings.SqlConxnString = Properties.Settings.Default.DefaultSQLConxn;
            _timer.Interval               = TimeSpan.FromSeconds(3);
            _timer.Tick                   += _timer_Tick;

            TesterSettings.ParentWindow = this;

            testerGrid.DataContext = _testerCollection;
            _testerCollection.Clear();

            UDPCommandProcessor.AddProcessor('M', _processMetrics);
            UDPCommandProcessor.AddProcessor('H', _processHeartbeat);
            UDPCommManager.StartListener(_multicastIP, UDPCommandProcessor.ProcessCommand);

            _timer.Start();
        }

        private void _processHeartbeat(string ip, string data)
        {
            lock (_testerLookup)
            {
                if (string.IsNullOrWhiteSpace(data) == true)
                {
                    data = TesterSettings.UNKNOWN;
                }

                if (_testerLookup.ContainsKey(ip) == false)
                {
                    var newTesterSettings = new TesterSettings()
                    {
                        Status      = data,
                        Timestamp   = DateTime.Now.ToString(),
                        TesterIP    = ip,
                        Method      = TesterSettings.SQL,
                        Scenario    = TesterSettings.SETGET,
                        Items       = 1000000,
                        MinSize     = 100,
                        MaxSize     = 100,
                        Threads     = 15,
                        Action      = TesterSettings.NONE,
                    };

                    _testerLookup.Add(ip, newTesterSettings);

                    Dispatcher.Invoke(new Action<TesterSettings>(_testerCollection.Add), newTesterSettings);
                }
                else
                {
                    if (_testerLookup[ip].Status == TesterSettings.INIT && data == TesterSettings.IDLE)
                    {
                        data = TesterSettings.INIT;
                    }

                    _testerLookup[ip].Status    = data;
                    _testerLookup[ip].Timestamp = DateTime.Now.ToString();
                }
            }
        }

        private void _processMetrics(string ip, string data)
        {
            lock (_testerLookup)
            {
                if (_testerLookup.ContainsKey(ip) == false)
                {
                    return;
                }

                if (_metricsLookup.ContainsKey(ip) == false)
                {
                    _metricsLookup[ip] = new Dictionary<string, double>();
                }

                DateTime time;
                string   className;
                bool     finished;

                BasicSetGetMessageRequest<UserRequest>.ParseFeedbackPacket(data, out className, out time, out finished, _metricsLookup[ip]);

                StringBuilder output = new StringBuilder();
                output.Append(string.Format("[{0}] {1} : {2}{3} => ", time, ip, finished ? "<Test Complete> " : string.Empty, className));

                double  totalGetTicks             = 0,
                        totalSetTicks             = 0,
                        totalGets                 = 0,
                        totalSets                 = 0,
                        totalGetsPerSecond        = 0,
                        totalSetsPerSecond        = 0,
                        totalAverageGetsPerSecond = 0,
                        totalAverageSetsPerSecond = 0,
                        totalOps                  = 0;

                var getLatency = double.NaN;
                var setLatency = double.NaN;

                foreach (var server in _metricsLookup.Keys)
                {
                    if (_testerLookup[server].Status == TesterSettings.RUN || server == ip)
                    {
                        var metricArray = _metricsLookup[server];

                        double getIterations    = metricArray["GetIterations"];
                        double getTicks         = metricArray["GetTicks"];
                        double getsPerSecond    = metricArray["GetsPerSecond"];
                        double avgGetsPerSecond = metricArray["AverageGetsPerSecond"];
                        double setIterations    = metricArray["SetIterations"];
                        double setTicks         = metricArray["SetTicks"];
                        double setsPerSecond    = metricArray["SetsPerSecond"];
                        double avgSetsPerSecond = metricArray["AverageSetsPerSecond"];

                        totalGets                   += getIterations;
                        totalGetTicks               += getTicks;
                        totalGetsPerSecond          += getsPerSecond;
                        totalAverageGetsPerSecond   += avgGetsPerSecond;
                        totalSets                   += setIterations;
                        totalSetTicks               += setTicks;
                        totalSetsPerSecond          += setsPerSecond;
                        totalAverageSetsPerSecond   += avgSetsPerSecond;
                        totalOps                    += setIterations;

                        if (server == ip)
                        {
                            _testerLookup[server].Timestamp    = DateTime.Now.ToString();
                            _testerLookup[server].TPS          = setsPerSecond.ToString("F0");
                            _testerLookup[server].Transactions = setIterations.ToString("F0");

                            var getMS = StopwatchHelpers.GetElapsedMS(0, (long)getTicks);
                            getLatency = double.NaN;

                            if (getIterations != 0)
                            {
                                getLatency = getMS / getIterations;
                            }

                            _testerLookup[server].GetLatency = getLatency.ToString("F2");

                            var setMS  = StopwatchHelpers.GetElapsedMS(0, (long)setTicks);
                            setLatency = double.NaN;

                            if (setIterations != 0)
                            {
                                setLatency = setMS / setIterations;
                            }

                            _testerLookup[server].SetLatency = setLatency.ToString("F2");
                        }
                    }
                    else
                    {
                        int tx;

                        if (int.TryParse(_testerLookup[server].Transactions, out tx) == true)
                        {
                            totalOps += tx;
                        }
                    }
                }

                var totalGetMS = StopwatchHelpers.GetElapsedMS(0, (long)totalGetTicks);
                getLatency     = double.NaN;

                if (totalGets != 0)
                {
                    getLatency = totalGetMS / totalGets;
                }

                var totalSetMS = StopwatchHelpers.GetElapsedMS(0, (long)totalSetTicks);
                setLatency     = double.NaN;

                if (totalSets != 0)
                {
                    setLatency = totalSetMS / totalSets;
                }

                output.AppendLine(string.Format("{0} Gets @ {1:0.00} ms => {2:0.00} tps ({3:0.00} atps), {4} Sets @ {5:0.00} ms => {6:0.00} tps ({7:0.00} atps)",
                    totalGets, getLatency, totalGetsPerSecond, totalAverageGetsPerSecond,
                    totalSets, setLatency, totalSetsPerSecond, totalAverageSetsPerSecond));

                _throughput = totalSetsPerSecond;
                _totalOps   = (ulong)totalOps;

                if (finished == true)
                {
                    _testerLookup[ip].Finished   = DateTime.Now.ToString();
                    _testerLookup[ip].Status     = TesterSettings.DONE;
                    _testerLookup[ip].Action     = _testerLookup[ip].Action == TesterSettings.NONE ? TesterSettings.NONE : TesterSettings.START;
                }
                
                Dispatcher.Invoke(new Action<string>(x => lblTPS.Content = x), _throughput.ToString("F0"));
                Dispatcher.Invoke(new Action<string>(x => lblTotal.Content = x), _totalOps.ToString());
                Dispatcher.Invoke(new Action<string>(txtOutput.AppendText), output.ToString());
            }
        }

        private void _timer_Tick(object sender, EventArgs e)
        {
            if (_isEditing == false)
            {
                testerGrid.Items.Refresh();
            }
        }

        private void _startTester(TesterSettings tester)
        {
            string packet = BasicSetGetMessageRequest<UserRequest>.GetStartTestPacket(DateTime.Now, TesterSettings.GetRequestType(tester.Method), 
                tester.Scenario, tester.Connection, tester.MinSize, tester.MaxSize, tester.Items, tester.Rate, tester.Threads, tester.TesterIP);             

            UDPCommManager.SquawkMulticastPacket(_multicastIP, packet);

            tester.Status       = TesterSettings.INIT;
            tester.Started      = DateTime.Now.ToString();
            tester.Finished     = string.Empty;
            tester.TPS          = string.Empty;
            tester.Transactions = string.Empty;
            tester.SetLatency   = string.Empty;
            tester.GetLatency   = string.Empty;
            tester.Action       = tester.Action == TesterSettings.NONE ? TesterSettings.NONE : TesterSettings.FINISH;
            
            testerGrid.Items.Refresh();
        }

        private void _finishTester(TesterSettings tester)
        {
            string packet = BasicSetGetMessageRequest<UserRequest>.GetFinishPacket(DateTime.Now, TesterSettings.GetRequestType(tester.Method), 
                tester.TesterIP);

            UDPCommManager.SquawkMulticastPacket(_multicastIP, packet);        

            tester.Finished     = DateTime.Now.ToString();
            tester.Status       = TesterSettings.ABORT;
            tester.Action       = tester.Action == TesterSettings.NONE ? TesterSettings.NONE : TesterSettings.START;
            
            testerGrid.Items.Refresh();        
        }

        private void _updateTester(TesterSettings tester)
        {
            string packet = BasicSetGetMessageRequest<UserRequest>.GetUpdatePacket(DateTime.Now, TesterSettings.GetRequestType(tester.Method),
                tester.MinSize, tester.MaxSize, tester.Items, tester.Rate, tester.TesterIP);

            UDPCommManager.SquawkMulticastPacket(_multicastIP, packet);        
        }

        private void Window_Closing(object sender, EventArgs e)
        {
            _timer.Stop();
            UDPCommManager.StopAll();
        }

        private void testerGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            _isEditing = true;
        }

        private void testerGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            _isEditing = false;
        }

        private void testerGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (_manualCommit == false && e.Cancel == false) 
            {
                _manualCommit = true;
                
                DataGrid grid = (DataGrid)sender;

                if (grid.CommitEdit(DataGridEditingUnit.Row, true) == true)
                {            
                    if (Keyboard.IsKeyDown(Key.LeftShift) == true)
                    {
                        TesterSettings tester = (TesterSettings)e.Row.Item;

                        foreach (var otherTester in _testerCollection)
                        {
                            if (tester == otherTester)
                            {
                                continue;
                            }

                            switch (e.Column.Header.ToString())
                            {
                                case "Method":
                                    otherTester.Method = tester.Method;
                                    break;

                                case "Scenario":
                                    otherTester.Scenario = tester.Scenario;
                                    break;

                                case "Connection":
                                    otherTester.Connection = tester.Connection;
                                    break;

                                case "Items":
                                    otherTester.Items = tester.Items;
                                    break;

                                case "Min Size":
                                    otherTester.MinSize = tester.MinSize;
                                    break;

                                case "Max Size":
                                    otherTester.MaxSize = tester.MaxSize;
                                    break;

                                case "Threads":
                                    otherTester.Threads = tester.Threads;
                                    break;

                                case "Rate":
                                    otherTester.Rate = tester.Rate;
                                    break;

                                case "Action":
                                    otherTester.Action = tester.Action;
                                    break;
                            }
                        }
                    }

                    grid.Items.Refresh();
                }

                _manualCommit = false;
            }
        }

        private void _execute_Click(object sender, RoutedEventArgs e)
        {
            lock (_testerLookup)
            {
                foreach (var tester in _testerCollection)
                {
                    if (tester.Status != TesterSettings.LOST)
                    {
                        switch (tester.Action)
                        {
                            case TesterSettings.START:  _startTester(tester);   break;
                            case TesterSettings.FINISH: _finishTester(tester);  break;
                            case TesterSettings.UPDATE: _updateTester(tester);  break;
                        }
                    }
                }
            }
        }

        private void _reset_Click(object sender, RoutedEventArgs e)
        {
            _totalOps      = 0;
            _throughput    = 0;            
            _metricsLookup = new Dictionary<string, Dictionary<string, double>>();

            lblTotal.Content = "0";
            lblTPS.Content   = "0";
            txtOutput.Clear();

            lock (_testerLookup)
            {
                _testerLookup.Clear();
                _testerCollection.Clear();
            }
        }
    }
}
