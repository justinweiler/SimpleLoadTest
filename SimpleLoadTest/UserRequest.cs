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


namespace SimpleLoadTest
{
    public abstract class UserRequest
    {
        public enum State
        {
            Pending,
            InProgress,
            Done
        }

        public long             WorkStartTicks;
        public long             WorkEndTicks;
        public long             WaitStartTicks;
        public double           WaitUntilMS;
        public State            RequestState;

        private static Random   _rnd = new Random();

        public abstract object DoRequest(object threadInfo);

        public virtual void OverrideSettings()
        {
        }

        public virtual void PrepForTest()
        {
        }

        public virtual void FinishTest()
        {
        }

        public virtual Dictionary<string, double> GetMetrics()
        {
            return  null;
        }

        public void Reset<T>(long nowTicks) where T : UserRequest
        {
            WorkStartTicks = 0;
            WorkEndTicks   = 0;
            WaitStartTicks = nowTicks;
            WaitUntilMS    = getRandom(SimpleLoadTestSettings<T>.RateMin, SimpleLoadTestSettings<T>.RateMax) * 1000.0;
            RequestState   = UserRequest.State.Pending;
        }

        protected int getRandom(int min, int max)
        {
            lock (_rnd)
            {
                return _rnd.Next(min, max);
            }
        }
        
        protected string getRandomString(int min, int max)
        {
            return Encoding.ASCII.GetString(getRandomBytes(min, max));
        }

        protected byte[] getRandomBytes(int min, int max)
        {
            int size = getRandom(min, max);
            byte[] buffer = new byte[size];

            lock (_rnd)
            {
                _rnd.NextBytes(buffer);
            }

            for (int i = 0; i < size; i++)
            {
                buffer[i] = (byte)((buffer[i] % 26) + 65);
            }

            return buffer;
        }

        public virtual void ExceptionThrown(string exceptionMessage)
        {
            Console.WriteLine("An exception has been thrown while processing work: {0}", exceptionMessage);
        }
    }
}
