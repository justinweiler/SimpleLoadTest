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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;


namespace SimpleLoadTest
{
    public delegate void packetProto(string ip, string data);

    #region UDPCommManager

    /// <summary>
    /// Class to manage UDPCommInstances
    /// </summary>
    public class UDPCommManager
    {
        #region Member Variables

        private static Dictionary<string, UDPCommInstance> _udpCommInstances = new Dictionary<string, UDPCommInstance>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Public Implementation

        /// <summary>
        /// Sets squawk interval for UDPCommInstance at specified multicast ip
        /// </summary>
        /// <param name="multicastIP">Multicast IP at which to squawk</param>
        /// <param name="interval">new squawk interval</param>
        public static void SetSquawkInterval(string multicastIP, int interval)
        {
            lock (_udpCommInstances)
            {
                if (_udpCommInstances.ContainsKey(multicastIP) == true)
                    _udpCommInstances[multicastIP].SquawkInterval = interval;
                else
                    throw new Exception("UDPCommInstance not found on requested multicast ip");
            }
        }

        /// <summary>
        /// retrieves squawk interval for UDPCommInstance at specified multicast ip
        /// </summary>
        /// <param name="multicastIP">Multicast IP at which to squawk</param>
        /// <returns>current squawk interval</returns>
        public static int GetSquawkInterval(string multicastIP)
        {
            lock (_udpCommInstances)
            {
                if (_udpCommInstances.ContainsKey(multicastIP) == true)
                    return _udpCommInstances[multicastIP].SquawkInterval;
                else
                    throw new Exception("UDPCommInstance not found on requested multicast ip");
            }
        }

        /// <summary>
        /// creates new UDPCommInstance if it does not exist yet and start listening
        /// </summary>
        /// <param name="multicastIP">Multicast IP at which to listen</param>
        /// <param name="packetCallback">callback function to call when packet is received</param>
        public static void StartListener(string multicastIP, packetProto packetCallback)
        {
            lock (_udpCommInstances)
            {
                if (_udpCommInstances.ContainsKey(multicastIP) == false)
                    _udpCommInstances[multicastIP] = new UDPCommInstance();

                _udpCommInstances[multicastIP].StartListener(multicastIP, packetCallback);
            }
        }

        /// <summary>
        /// creates new UDPCommInstance if it does not exist yet and start sending squawk messages periodically
        /// </summary>
        /// <param name="multicastIP">Multicast IP at which to squawk</param>
        /// <param name="multicastPacket">data for first UDP packet</param>
        /// <param name="packetCompartment">data compartment to store packet</param>
        public static void StartSquawker(string multicastIP, string multicastPacket, string packetCompartment)
        {
            lock (_udpCommInstances)
            {
                if (_udpCommInstances.ContainsKey(multicastIP) == false)
                    _udpCommInstances[multicastIP] = new UDPCommInstance();

                _udpCommInstances[multicastIP].StartSquawker(multicastIP, multicastPacket, packetCompartment);
            }
        }

        /// <summary>
        /// stores data for next UDP packet to be multicast
        /// </summary>
        /// <param name="multicastIP">Multicast IP at which to squawk</param>
        /// <param name="multicastPacket">data for UDP packet</param>
        /// <param name="packetCompartment">data compartment to store packet</param>
        public static void SetMulticastPacket(string multicastIP, string multicastPacket, string packetCompartment)
        {
            lock (_udpCommInstances)
            {
                if (_udpCommInstances.ContainsKey(multicastIP) == false)
                    _udpCommInstances[multicastIP] = new UDPCommInstance();

                _udpCommInstances[multicastIP].SetMulticastPacket(multicastPacket, packetCompartment);
            }
        }

        /// <summary>
        /// Inject UDP packet once to specified multicast ip
        /// </summary>
        /// <param name="multicastIP">Multicast IP at which to squawk</param>
        /// <param name="multicastPacket">data for UDP packet</param>
        public static void SquawkMulticastPacket(string multicastIP, string multicastPacket)
        {
            lock (_udpCommInstances)
            {
                if (_udpCommInstances.ContainsKey(multicastIP) == true)
                {
                    _udpCommInstances[multicastIP].SquawkMulticastPacket(multicastPacket);
                }
                else
                {
                    var udpCommInstance = new UDPCommInstance();
                    udpCommInstance.SquawkMulticastPacket(multicastPacket, multicastIP);
                }
            }
        }

        /// <summary>
        /// stops UDPCommInstance at specified multicast ip
        /// </summary>
        /// <param name="multicastIP">Multicast IP at which to squawk and or listen</param>
        public static void Stop(string multicastIP)
        {
            lock (_udpCommInstances)
            {
                if (_udpCommInstances.ContainsKey(multicastIP) == true)
                {
                    _udpCommInstances[multicastIP].Stop();
                    _udpCommInstances.Remove(multicastIP);
                }
                else
                {
                    throw new Exception("UDPCommInstance not found on requested multicast ip");
                }
            }
        }

        /// <summary>
        /// Stops all UDPCommInstances managed by this UDPCommManager
        /// </summary>
        public static void StopAll()
        {
            lock (_udpCommInstances)
            {
                string[] keys = new string[_udpCommInstances.Count];

                _udpCommInstances.Keys.CopyTo(keys, 0);

                foreach (string key in keys)
                {
                    _udpCommInstances[key].Stop();
                    _udpCommInstances.Remove(key);
                }
            }
        }

        #endregion
    }

    #endregion

    #region UDPCommInstance

    /// <summary>
    /// Wrapper class for a UDP Multicast communicator
    /// </summary>
    public class UDPCommInstance
    {
        #region Member Variables

        private Socket                          _listenSocket = null;
        private Thread                          _listenThread = null;
        private Thread                          _squawkThread = null;
        private bool                            _done = false;
        private Dictionary<string, string>      _multicastPacketLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private packetProto                     _processUDPPacket = null;
        private int                             _squawkInterval = 10000;
        private string                          _multicastIP;
        private object                          _lock = new object();
        private ManualResetEvent                _mreDone = new ManualResetEvent(false);

        #endregion

        #region Public Implementation

        /// <summary>
        /// time interval how often squawk messages are sent
        /// </summary>
        public int SquawkInterval 
        {
            get { return _squawkInterval; } 
            set { _squawkInterval = value; } 
        }

        /// <summary>
        /// Starts listening at specified multicast ip
        /// </summary>
        /// <param name="multicastIP">Multicast IP at which to listen</param>
        /// <param name="packetCallback">callback function to call when UDP packet is received</param>
        public void StartListener(string multicastIP, packetProto packetCallback)
        {
            lock (_lock)
            {
                if (_listenThread == null)
                {
                    _multicastIP = multicastIP;
                    _listenThread = new Thread(_listener);
                    _listenThread.Name = "UDPCommInstance.Listener";
                    _processUDPPacket = packetCallback;
                    _listenThread.Start();
                }
            }
        }

        /// <summary>
        /// Periodically send UDP squawk message to multicast ip address
        /// </summary>
        /// <param name="multicastIP">IP to multicast to</param>
        /// <param name="multicastPacket">data for UDP packet</param>
        /// <param name="packetCompartment">data compartment to store packet</param>
        public void StartSquawker(string multicastIP, string multicastPacket, string packetCompartment)
        {
            lock (_lock)
            {
                _multicastPacketLookup[packetCompartment] = multicastPacket;

                if (_squawkThread == null)
                {
                    _multicastIP = multicastIP;
                    _squawkThread = new Thread(_squawker);
                    _squawkThread.Name = "UDPCommInstance.Squawker";
                    _squawkThread.Start();
                }
            }
        }

        /// <summary>
        /// sets data for UDP packet for next multicast
        /// </summary>
        /// <param name="multicastPacket">data</param>
        public void SetMulticastPacket(string multicastPacket, string packetCompartment)
        {
            lock (_lock)
            {
                _multicastPacketLookup[packetCompartment] = multicastPacket;
            }
            _squawk();
        }

        /// <summary>
        /// Multicasts injected packet
        /// </summary>
        /// <param name="multicastPacket">data for UDP packet</param>
        /// <param name="multicastIP">IP to multicast to, null to disregard</param>
        public bool SquawkMulticastPacket(string multicastPacket, string multicastIP = null)
        {
            try
            {
                _squawk(multicastPacket, multicastIP);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("[UDPCommManager] SquawkMulticastPacket exception: {0}", ex.ToString()));
                return false;
            }
        }

        /// <summary>
        /// stops UDPCommInstance
        /// </summary>
        public void Stop()
        {
            try
            {
                lock (_lock)
                {
                    _done = true;
                    _mreDone.Set();

                    // ROK added aborts since this was keeping the process from exiting
                    if (_listenThread != null)
                        if (!_listenThread.Join(30000))
                            _listenThread.Abort();

                    if (_squawkThread != null)
                        if (!_squawkThread.Join(60000))
                            _squawkThread.Abort();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("[UDPCommManager] Stop exception: {0}", ex.ToString()));
                return;
            }
            finally
            {
                _listenThread = null;
                _squawkThread = null;
            }

            Debug.WriteLine("[UDPCommManager] _squawker and _listener stopped");
        }

        #endregion

        #region Private Implementation

        private void _squawk(string multicastPacket = null, string multicastIP = null)
        {
            try
            {
                if (_done == false)
                {
                    lock (_lock)
                    {
                        if (_done == false)
                        {
                            byte[] sendbuf = new byte[1024 * 10 - 512]; // 10k less a little room for the header

                            if (string.IsNullOrWhiteSpace(multicastIP) == true)
                                multicastIP = _multicastIP;

                            // set the destination group
                            string[] parts = multicastIP.Split(new char[]{':'}, StringSplitOptions.RemoveEmptyEntries);
                            IPAddress mipAddress = IPAddress.Parse(parts[0]);

                            int localPort = 0;

                            if (parts.Length > 1)
                                localPort = int.Parse(parts[1]);

                            // create the destination group
                            IPEndPoint destinationGroup = new IPEndPoint(mipAddress, localPort);

                            // create the socket
                            string localIP = NetworkHelper.GetLocalIP();

                            IPAddress ipAddress = IPAddress.Parse(localIP);
                            var toSendPackets   = new List<string>();

                            if (string.IsNullOrWhiteSpace(multicastPacket) == false)
                                toSendPackets.Add(multicastPacket);
                            else
                                toSendPackets.AddRange(_multicastPacketLookup.Values);

                            foreach (string packet in toSendPackets)
                            {
                                Socket squawkSocket = null;

                                try
                                {
                                    // create the socket
                                    squawkSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.IP);

                                    //Bind the socket to the selected IP address
                                    squawkSocket.Bind(new IPEndPoint(ipAddress, 0));
                                    squawkSocket.SendBufferSize = 1024 * 10;
                                    squawkSocket.MulticastLoopback = false;

                                    //Set the socket options
                                    squawkSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(mipAddress, ipAddress));
                                    squawkSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, ipAddress.GetAddressBytes());

                                    if (string.IsNullOrWhiteSpace(packet) == false)
                                    {
                                        byte[] tempbuf = Encoding.ASCII.GetBytes(NetworkHelper.GetLocalIP() + "$$$" + packet);

                                        if (tempbuf.Length <= sendbuf.Length)
                                        {
                                            // send packet
                                            Array.Copy(tempbuf, sendbuf, tempbuf.Length);
                                            squawkSocket.SendTo(sendbuf, tempbuf.Length, SocketFlags.None, destinationGroup);
                                        }
                                        else
                                        {
                                            Debug.WriteLine("[UDPCommManager] _squawk packet too large!  We will not send");
                                        }
                                    }
                                }
                                finally
                                {
                                    try
                                    {
                                        // close socket
                                        if (squawkSocket != null)
                                            squawkSocket.Close();

                                        squawkSocket = null;
                                    }
                                    catch
                                    {
                                    }
                                }
                            
                                if (toSendPackets.Count > 0)
                                    Thread.Sleep(1000);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("[UDPCommManager] _squawk on {0} exception: {1}", _multicastIP, ex.ToString()));
                throw ex;
            }
        }

        private void _squawker()
        {
        tryagain:            
            
            try
            {
                Debug.WriteLine(string.Format("[UDPCommManager] _squawker on {0} started", _multicastIP));

                while (_done == false)
                {
                    _squawk();

                    if (_mreDone.WaitOne(_squawkInterval))
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("[UDPCommManager] _squawker on {0} exception: {1}", _multicastIP, ex.ToString()));

                if (_done == false)
                    _mreDone.WaitOne(5000); // wait 5 seconds before trying again

                if (_done == false)
                    goto tryagain;
            }
            finally
            {
                Debug.WriteLine("[UDPCommManager] _squawker ending");
            }
        }

        private void _listener()
        {
        tryagain:

            try
            {
                Debug.WriteLine(string.Format("[UDPCommManager] _listener on {0} started", _multicastIP));

                // create the socket
                _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);

                // get the local ip
                string localIP = NetworkHelper.GetLocalIP();

                IPAddress ipAddress = IPAddress.Parse(localIP);

                // split out the multicast ip from possible ip:port
                string[] parts       = _multicastIP.Split(new char[]{':'}, StringSplitOptions.RemoveEmptyEntries);
                string mcastIP       = parts[0];
                IPAddress mipAddress = IPAddress.Parse(mcastIP);

                int localPort = 0;

                if (parts.Length > 1)
                    localPort = int.Parse(parts[1]);

                // set receive buffer
                _listenSocket.ReceiveBufferSize = 1024 * 1024;
                
                //Bind the socket to the selected IP address
                _listenSocket.Bind(new IPEndPoint(ipAddress, localPort));

                //Set the socket options
                _listenSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
                _listenSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(mipAddress, ipAddress));

                // create the incoming data buffer
                byte[] receivedData = new byte[_listenSocket.ReceiveBufferSize];

                // Creates an IPEndPoint to capture the identity of the sending host.
                IPEndPoint sender       = new IPEndPoint(IPAddress.Any, 0);
                EndPoint senderEndPoint = (EndPoint)sender;

                WaitHandle[] handles = new WaitHandle[2] { _mreDone, null };

                // sniff all incoming messages
                while (_done == false)
                {
                    //int nReceived = _listenSocket.ReceiveFrom(receivedData, ref senderEndPoint);
                    var async = _listenSocket.BeginReceiveFrom(receivedData, 0, receivedData.Length, SocketFlags.None, ref senderEndPoint, null, null);

                    handles[1] = async.AsyncWaitHandle;
                    if (WaitHandle.WaitAny(handles) != 1)
                        break;

                    int nReceived = _listenSocket.EndReceiveFrom(async, ref senderEndPoint);

                    IPHeader ipHeader = new IPHeader(receivedData, nReceived);                
                    string sourceIP   = ipHeader.SourceAddress.ToString();
                    string destIP     = ipHeader.DestinationAddress.ToString();

                    // make sure datagram is relevant
                    if (ipHeader.ProtocolType == ProtocolType.Udp && destIP == mcastIP)
                    {
                        byte[] bytes = new byte[ipHeader.MessageLength - 8];
                        Array.Copy(ipHeader.Data, 8, bytes, 0, ipHeader.MessageLength - 8);

                        if (string.IsNullOrWhiteSpace(sourceIP) == true && _done == false)
                        {
                            Debug.WriteLine("[UDPCommManager] Received squawk from bad sourceIP!");
                        }
                        else if (_processUDPPacket == null && _done == false)
                        {
                            Debug.WriteLine(string.Format("[UDPCommManager] Received squawk from {0} - No processor defined.", sourceIP));
                        }
                        else if (_done == false)
                        {
                            //Debug.WriteLine(string.Format("[UDPCommManager] Received squawk from {0}", sourceIP));
                            string packet = ASCIIEncoding.ASCII.GetString(bytes);

                            ThreadPool.SetMinThreads(1000, 1000);

                            if (packet.IndexOf("$$$") != -1 && packet.IndexOf("$$$") < 16) // 172.156.189.112$$$packetdata
                            {
                                string[] packetParts = packet.Split(new string[] { "$$$" }, StringSplitOptions.RemoveEmptyEntries);

                                if (packetParts.Length == 2 && IPAddress.TryParse(packetParts[0], out ipAddress) == true)
                                {
                                    ThreadPool.QueueUserWorkItem(_processUdpPacketProc, packetParts);
                                }
                                else
                                {
                                    Debug.WriteLine(string.Format("[UDPCommManager] Received squawk from {0} - Malformed Packet!", sourceIP));
                                }
                            }
                            else
                            {
                                ThreadPool.QueueUserWorkItem(_processUdpPacketProc, new string[] { sourceIP, packet });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("[UDPCommManager] _listener on {0} exception: {1}", _multicastIP, ex.ToString()));

                if (_done == false)
                    _mreDone.WaitOne(5000); // wait 5 seconds before trying again

                if (_done == false)
                    goto tryagain;
            }
            finally
            {
                try
                {
                    if (_listenSocket != null)
                        _listenSocket.Close();

                    _listenSocket = null;
                }
                catch
                {
                }

                Debug.WriteLine("[UDPCommManager] _listener ending");
            }
        }

        private void _processUdpPacketProc(object obj)
        {
            try
            {
                string[] parameters = (string[])obj;
                _processUDPPacket(parameters[0], parameters[1]);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("[UDPCommManager] _processUdpPacketProc exception: {0}", ex.ToString()));
            }
        }

        #endregion
    }

    #endregion
}
