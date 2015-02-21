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
using System.Diagnostics;
using System.Net;


namespace SimpleLoadTest
{
    /// <summary>
    /// Class with static functions to process UDP packets
    /// </summary>
    public class UDPCommandProcessor
    {
        /// <summary>
        /// Dictionary that stores callback functions per first character of UDP packets
        /// </summary>
        private static Dictionary<char, packetProto> _processors = new Dictionary<char, packetProto>();
        private static bool _disabled = false;

        public static void DisableCommandProcessors()
        {
            _disabled = true;
        }

        public static void EnableCommandProcessors()
        {
            _disabled = false;
        }

        /// <summary>
        /// call callback function to process UDP packet
        /// </summary>
        /// <param name="ip">IP address the UDP packet is received from</param>
        /// <param name="bytes">Data field of UDP packet</param>
        public static void ProcessCommand(string ip, string data)
        {
            if (string.IsNullOrEmpty(data) == false)
            {
                char command = data[0];

                if (data.Length > 1)
                    data = data.Substring(1);
                else
                    data = string.Empty;

                if (_disabled == false)
                {
                    if (_processors.ContainsKey(command) == true)
					{
						//Debug.WriteLine(string.Format("[UDPCommandProcessor] Received command ({0}) from {1}", command, ip));
						_processors[command](ip, data);
					}
					else
					{
						//Debug.WriteLine(string.Format("[UDPCommandProcessor] Unknown command ({0}) from {1}", command, ip));
					}
                }
            }
        }

        /// <summary>
        /// stores a callback function for UDP packets starting with specific character
        /// </summary>
        /// <param name="c">character the according UDP packets starts with</param>
        /// <param name="callback">callback function</param>
        public static void AddProcessor(char c, packetProto callback)
        {
            if (_disabled == false)
                _processors[c] = callback;
        }
    }
}
