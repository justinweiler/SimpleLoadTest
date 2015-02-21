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

using System.Linq;
using System.Net;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Net.NetworkInformation;


namespace SimpleLoadTest
{
    public static class NetworkHelper
    {
        private static string _localIP = null;

        public static bool IsValidIp(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip) || ip == "0.0.0.0")
                return false;

            return true;
        }

        public static string GetLocalIP()
        {
            if (IsValidIp(_localIP))
                return _localIP;

            try
            {
                // preferred method over clr
                if (TryGetIpFromTcpRegistry(out _localIP))
                {
                    Debug.WriteLine("[NetworkHelper] Local IP resolved via probed TCP registry to {0}", _localIP, 0);
                    return _localIP;
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine("[NetworkHelper] Error while resolving local IP: {0}", ex);
            }

            // lastly, get IP using CLR
            if (TryGetClrIp(out _localIP))
            {
                Debug.WriteLine("[NetworkHelper] Local IP resolved via CLR to {0}", _localIP, 0);
                return _localIP;
            }

            Debug.WriteLine("[NetworkHelper] WARNING!!! Local IP not resolved");

            return null;
        }

        /// <summary>
        /// Returns the next greater, unused local network port in the event that the given port is used.
        /// </summary>
        /// <param name="startingPort">The port to check.</param>
        /// <returns>The port number if unused, or the next greater, unused local network port.</returns>
        /// <remarks>
        /// see http://stackoverflow.com/questions/570098/in-c-how-to-check-if-a-tcp-port-is-available
        /// </remarks>
        public static int GetNextAvailablePort(int startingPort)
        {
            Mutex mutex = new Mutex(false, "GetNextAvailablePort");

            try
            {
                mutex.WaitOne();
                Func<int, bool> portCheck = (port) =>
                    {
                        //
                        // Evaluate current system tcp connections. This is the same information provided
                        // by the netstat command line application, just in .Net strongly-typed object
                        // form.  We will look through the list, and see if the ports we would like to use
                        // are taken.
                        //
                        IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();

                        TcpConnectionInformation[] tcpConnectionInfoArray = ipGlobalProperties.GetActiveTcpConnections();
                        var firstportQuery = from tcpConnectionInfo in tcpConnectionInfoArray
                                             select tcpConnectionInfo.LocalEndPoint.Port;

                        IPEndPoint[] ipEndPoints = ipGlobalProperties.GetActiveTcpListeners();
                        var secondPortQuery = from ipEndPoint in ipEndPoints
                                              select ipEndPoint.Port;

                        var portQuery = firstportQuery.Union(secondPortQuery);
                        if (portQuery.Contains(port))
                        {
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    };

                //
                // Increment service port to an unused port number
                //
                while (!portCheck(startingPort))
                {
                    if (startingPort != 65535)
                    {
                        startingPort++;
                    }
                    else
                    {
                        startingPort = 1;
                    }
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
 
            return startingPort;
        }

        public static string GetFirstActiveAdapterId()
        {
            using (RegistryKey dnsAdaptersRoot = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\services\Tcpip\Parameters\DNSRegisteredAdapters"))
            {
                if (dnsAdaptersRoot != null)
                {
                    foreach (var adapterId in dnsAdaptersRoot.GetSubKeyNames())
                    {
                        using (var adapterKey = dnsAdaptersRoot.OpenSubKey(adapterId))
                        {
                            if (adapterKey != null)
                            {
                                if ((int)adapterKey.GetValue("StaleAdapter", 1) == 0)
                                {
                                    return adapterId;
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        public static bool TryGetIpFromTcpRegistry(out string ip)
        {
            string dnsAdapterId = GetFirstActiveAdapterId();
            
            if (dnsAdapterId != null)
            {
                var regPath = @"SYSTEM\CurrentControlSet\services\Tcpip\Parameters\Interfaces\" + dnsAdapterId;
            
                using (RegistryKey adapterKey = Registry.LocalMachine.OpenSubKey(regPath))
                {
                    if (adapterKey != null)
                    {
                        if ((int)adapterKey.GetValue("EnableDHCP", 0) == 1)
                        {
                            ip = (string)adapterKey.GetValue("DhcpIPAddress");
                            if (IsValidIp(ip))
                                return true;
                        }

                        var arr = adapterKey.GetValue("IPAddress") as string[];
                        
                        if (arr != null && arr.Length > 0)
                        {
                            ip = arr[0];
            
                            if (IsValidIp(ip))
                                return true;
                        }
                    }
                }
            }

            ip = null;

            return false;
        }

        public static bool TryGetClrIp(out string ip)
        {
            IPHostEntry myHost = Dns.GetHostEntry(string.Empty);

            var query = from address in myHost.AddressList
                        where address.AddressFamily == AddressFamily.InterNetwork
                        select address.ToString();

            ip = query.FirstOrDefault();
            
            if (IsValidIp(ip))
                return true;

            return false;
        }
    }
}
