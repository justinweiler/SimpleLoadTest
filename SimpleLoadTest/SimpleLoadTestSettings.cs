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
using System.Xml;


namespace SimpleLoadTest
{
    public interface ISimpleLoadTestSettings
    {
        void ParseXml(XmlDocument doc);
    }

    public enum RequestModes
    {
        ByMessage = 1,
        ByUser    = 2
    }

    public class SimpleLoadTestSettings<T> : SimpleLoadTestCNCBase, ISimpleLoadTestSettings where T : UserRequest
    {
        public static string            Scenario;
        public static int               RampUp;              
        public static int               Duration;            
        public static int               Users;               
        public static int               Workers;             
        public static int               TotalRequests;
        public static RequestModes      RequestMode;      
        public static int               MaxAllowedLatency;
        public static string            Connection;  
        public static int               RateMin;
        public static int               RateMax;            
        public static int               MessageMin;
        public static int               MessageMax;
        public static int               RequestRate;
        public static string            UserData;

        static SimpleLoadTestSettings()
        {
            Scenario          = Properties.Settings.Default.Scenario;
            RampUp            = Properties.Settings.Default.RampUp;
            Duration          = Properties.Settings.Default.Duration;
            Users             = Properties.Settings.Default.Users;
            Workers           = Properties.Settings.Default.Workers;
            TotalRequests     = Properties.Settings.Default.TotalRequests;
            RequestMode       = (RequestModes)Properties.Settings.Default.RequestMode;
            MaxAllowedLatency = Properties.Settings.Default.MaxAllowedLatency;
            RateMin           = Properties.Settings.Default.RateMin;
            RateMax           = Properties.Settings.Default.RateMax;
            MessageMin        = Properties.Settings.Default.MessageMin;
            MessageMax        = Properties.Settings.Default.MessageMax;
            Connection        = Properties.Settings.Default.Connection;
            RequestRate       = Properties.Settings.Default.RequestRate;
            UserData          = Properties.Settings.Default.UserData;
        }

        public static void OutputSettingsToConsole()
        {
            Console.WriteLine("MulticastIP (IPv4):     {0}", MulticastIP);
            Console.WriteLine("Connection (str):       {0}", Connection);
            Console.WriteLine("Request Mode:           {0}", Enum.GetName(typeof(RequestModes), RequestMode));
            Console.WriteLine("Scenario:               {0}", Scenario);

            if (RequestMode == RequestModes.ByUser)
            {
                Console.WriteLine("RampUp (s):             {0}", RampUp);
                Console.WriteLine("Duration (s):           {0}", Duration);
                Console.WriteLine("Users (n):              {0}", Users);
                Console.WriteLine("Rate Min (s):           {0}", RateMin);
                Console.WriteLine("Rate Max (s):           {0}", RateMax);
            }
            else
            {
                Console.WriteLine("Total Requests:         {0}", TotalRequests);
            }

            Console.WriteLine("Message Min (b):        {0}", MessageMin);
            Console.WriteLine("Message Max (b):        {0}", MessageMax);
            Console.WriteLine("Workers (n):            {0}", Workers);
            Console.WriteLine("Max Latency (ms):       {0}", MaxAllowedLatency);
            Console.WriteLine("Request Rate (tps):     {0}", RequestRate);
            Console.WriteLine("User Data (str):        {0}", UserData);
        }

        public void ParseXml(XmlDocument doc)
        {
            var scenarioNode = doc.SelectSingleNode("Test/Scenario");

            if (scenarioNode != null)
            {
                Scenario = scenarioNode.InnerText;
            }

            var rampUpNode = doc.SelectSingleNode("Test/RampUp");

            if (rampUpNode != null)
            {
                RampUp = int.Parse(rampUpNode.InnerText);
            }

            var durationNode = doc.SelectSingleNode("Test/Duration");

            if (durationNode != null)
            {
                Duration = int.Parse(durationNode.InnerText);
            }
            
            var rateMinNode = doc.SelectSingleNode("Test/RateMin");

            if (rateMinNode != null)
            {
                RateMin = int.Parse(rateMinNode.InnerText);
            }

            var rateMaxNode = doc.SelectSingleNode("Test/RateMax");

            if (rateMaxNode != null)
            {
                RateMax = int.Parse(rateMaxNode.InnerText);
            }

            var conxnNode = doc.SelectSingleNode("Test/Connection");

            if (conxnNode != null)
            {
                Connection = conxnNode.InnerText;
            }

            var messageMinNode = doc.SelectSingleNode("Test/MessageMin");

            if (messageMinNode != null)
            {
                MessageMin = int.Parse(messageMinNode.InnerText);
            }

            var messageMaxNode = doc.SelectSingleNode("Test/MessageMax");

            if (messageMaxNode != null)
            {
                MessageMax = int.Parse(messageMaxNode.InnerText);
            }

            var workersNode = doc.SelectSingleNode("Test/Workers");

            if (workersNode != null)
            {
                Workers = int.Parse(workersNode.InnerText);
            }

            var maxAllowedLatencyNode = doc.SelectSingleNode("Test/MaxAllowedLatency");
            
            if (maxAllowedLatencyNode != null)
            {
                MaxAllowedLatency = int.Parse(maxAllowedLatencyNode.InnerText);
            }

            var usersNode = doc.SelectSingleNode("Test/Users");

            if (usersNode != null)
            {
                Users = int.Parse(usersNode.InnerText);
            }

            var requestModeNode = doc.SelectSingleNode("Test/RequestMode");
           
            if (requestModeNode != null)
            {
                RequestMode = (RequestModes)int.Parse(requestModeNode.InnerText);
            }

            var totalRequestsNode = doc.SelectSingleNode("Test/TotalRequests");

            if (totalRequestsNode != null)
            {
                TotalRequests = int.Parse(totalRequestsNode.InnerText);
            }

            var requestRateNode = doc.SelectSingleNode("Test/RequestRate");

            if (requestRateNode != null)
            {
                RequestRate = int.Parse(requestRateNode.InnerText);
            }

            var userDataNode = doc.SelectSingleNode("Test/UserData");

            if (userDataNode != null)
            {
                UserData = userDataNode.InnerText;
            }
        }
    }
}
