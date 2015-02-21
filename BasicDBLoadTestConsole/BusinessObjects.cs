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


namespace BasicDBLoadTest
{
    [Serializable]
    public class Customer
    {
        private Guid        _id;
        private string      _firstName  = string.Empty;
        private string      _lastName   = string.Empty;
        private DateTime    _dateActive = DateTime.Now;
        private string      _payload    = string.Empty;

        /// <summary>
        /// default constructor
        /// </summary>
        public Customer()
        { }

        /// <summary>
        /// creates a new customer object
        /// </summary>
        /// <param name="Id">Id used in SQL server</param>
        /// <param name="firstName">First Name</param>
        /// <param name="lastName">Last Name</param>
        /// <param name="dateActive">Date the customer was created</param>
        /// <param name="payload">Payload -- for changing object size</param>
        public Customer(Guid Id, string firstName, string lastName, DateTime dateActive, string payload)
        {
            this._id         = Id;
            this._firstName  = firstName;
            this._lastName   = lastName;
            this._dateActive = dateActive;
            this._payload    = payload;
        }

        /// <summary>
        /// primary Key in SQL
        /// </summary>
        public Guid Id
        {
            get { return this._id; }
            set { this._id = value; }
        }

        /// <summary>
        /// First Name
        /// </summary>
        public string FirstName
        {
            get { return this._firstName; }
            set { this._firstName = value; }
        }

        /// <summary>
        /// Last Name
        /// </summary>
        public string LastName
        {
            get { return this._lastName; }
            set { this._lastName = value; }
        }

        /// <summary>
        /// Date the customer became active
        /// </summary>
        public DateTime DateActive
        {
            get { return this._dateActive; }
            set { this._dateActive = value; }
        }
        
        /// <summary>
        /// Used in testing for varying object size
        /// </summary>
        public string Payload
        {
            get { return this._payload; }
            set { this._payload = value; }
        }
    }
}
