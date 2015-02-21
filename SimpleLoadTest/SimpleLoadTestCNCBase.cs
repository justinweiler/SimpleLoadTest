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
using System.Xml;


namespace SimpleLoadTest
{
    public abstract class SimpleLoadTestCNCBase
    {
        public static string                    MulticastIP;
        public static bool                      IsSlaveMode;       
        private static List<ISimpleLoadTest>    _testList = new List<ISimpleLoadTest>();        

        static SimpleLoadTestCNCBase()
        {
            MulticastIP         = Properties.Settings.Default.MulticastIP;
        }

        public static void TellIdle()
        {
            if (SimpleLoadTestCNCBase.IsSlaveMode == true)
            {
                UDPCommManager.SetMulticastPacket(SimpleLoadTestCNCBase.MulticastIP, "Hidle", "heartbeat");
            }
        }

        public static void TellWait()
        {
            if (SimpleLoadTestCNCBase.IsSlaveMode == true)
            {
                UDPCommManager.SetMulticastPacket(SimpleLoadTestCNCBase.MulticastIP, "Hwait", "heartbeat");
            }
        }

        public static void TellRun()
        {
            if (SimpleLoadTestCNCBase.IsSlaveMode == true)
            {
                UDPCommManager.SetMulticastPacket(SimpleLoadTestCNCBase.MulticastIP, "Hrun", "heartbeat");
            }
        }

        public static void TellStop()
        {
            if (SimpleLoadTestCNCBase.IsSlaveMode == true)
            {
                UDPCommManager.SetMulticastPacket(SimpleLoadTestCNCBase.MulticastIP, "Hstop", "heartbeat");
            }
        }

        public static void TellPrep()
        {
            if (SimpleLoadTestCNCBase.IsSlaveMode == true)
            {
                UDPCommManager.SetMulticastPacket(SimpleLoadTestCNCBase.MulticastIP, "Hprep", "heartbeat");
            }
        }

        public static void InitializeSlaveMode()
        {
            UDPCommandProcessor.AddProcessor('S', _startTest);
            UDPCommandProcessor.AddProcessor('F', _finishTest);
            UDPCommandProcessor.AddProcessor('U', _updateTest);
            UDPCommManager.StartListener(MulticastIP, UDPCommandProcessor.ProcessCommand);
            
            UDPCommManager.SetSquawkInterval(MulticastIP, 10000);
            UDPCommManager.StartSquawker(MulticastIP, "Hidle", "heartbeat");
            
            IsSlaveMode = true;
        }

        public static void TerminateSlaveMode()
        {
            UDPCommManager.StopAll();
        }

        private static void _startTest(string ip, string data)
        {
            string className;
            var simpleLoadTest = _processXmlGetInstanceAndAdjustSettings(data, out className);

            if (simpleLoadTest != null)
            {
                _cleanTestList(simpleLoadTest.GetType());

                lock (_testList)
                {
                    _testList.Add(simpleLoadTest);
                }

                simpleLoadTest.OutputSettingsToConsole();
                simpleLoadTest.DoTest("Slave Mode - " + className, false);                
            }
        }

        private static void _updateTest(string ip, string data)
        {
            string className;
            _processXmlGetInstanceAndAdjustSettings(data, out className);
        }

        private static void _finishTest(string ip, string data)
        {

            var simpleLoadTestType = _processXmlGetType(data);
            _cleanTestList(simpleLoadTestType);
        }

        private static void _cleanTestList(Type testType)
        {
            lock (_testList)
            {
                for (int i = _testList.Count - 1; i >= 0; i--)
                {
                    var test = _testList[i];

                    if (test.GetType() == testType)
                    {
                        test.Stop();
                        _testList.RemoveAt(i);
                        return;
                    }
                }
            }
        }

        private static ISimpleLoadTest _processXmlGetInstanceAndAdjustSettings(string data, out string className)
        {
            className = null;

            var doc = new XmlDocument();
            doc.LoadXml(data);

            var ipTarget = doc.SelectSingleNode("Test").Attributes["targetIP"];

            if (ipTarget != null && string.IsNullOrWhiteSpace(ipTarget.InnerText) == false && 
                ipTarget.InnerText.ToLower() != "unused" && ipTarget.InnerText != NetworkHelper.GetLocalIP())
            {
                return null;
            }

            var classNode = doc.SelectSingleNode("Test/Class");

            className = classNode.InnerText;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GlobalAssemblyCache == false)
                {
                    foreach (var type in assembly.GetExportedTypes())
                    {
                        if (type.Name == className)
                        {
                            var baseType = typeof(SimpleLoadTest<>);
                            var genericType = baseType.MakeGenericType(type);
                            var simpleLoadTest = (ISimpleLoadTest)Activator.CreateInstance(genericType);

                            simpleLoadTest.Settings.ParseXml(doc);

                            return simpleLoadTest;
                        }
                    }
                }
            }

            return null;
        }

        private static Type _processXmlGetType(string data)
        {
            var doc = new XmlDocument();
            doc.LoadXml(data);

            var ipTarget = doc.SelectSingleNode("Test").Attributes["targetIP"];

            if (ipTarget != null && string.IsNullOrWhiteSpace(ipTarget.InnerText) == false && 
                ipTarget.InnerText.ToLower() != "unused" && ipTarget.InnerText != NetworkHelper.GetLocalIP())
            {
                return null;
            }

            var classNode = doc.SelectSingleNode("Test/Class");

            string className = classNode.InnerText;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GlobalAssemblyCache == false)
                {
                    foreach (var type in assembly.GetExportedTypes())
                    {
                        if (type.Name == className)
                        {
                            var baseType = typeof(SimpleLoadTest<>);
                            var genericType = baseType.MakeGenericType(type);
                            return genericType;
                        }
                    }
                }
            }

            return null;
        }
    }
}
