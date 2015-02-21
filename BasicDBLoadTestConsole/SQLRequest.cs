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
using SimpleLoadTest;


namespace BasicDBLoadTest
{
    public class SQLRequest : BasicSetGetMessageRequest<SQLRequest>
    {
        public override object Connect(object connection)
        {
            if (connection == null)
            {
                connection = new SQL_DAL(SimpleLoadTestSettings<SQLRequest>.Connection);
            }

            return connection;
        }

        public override void Get(object connection)
        {
            var sqlDAL = (SQL_DAL)connection;

            var customer = sqlDAL.Get(UserID);

            if (customer == null)
            {
                Console.WriteLine("Unable to retrieve key: {0}", UserID);
            }
        }

        public override void Set(object connection)
        {            
            var sqlDAL = (SQL_DAL)connection;

            var customer = new Customer()
            {
                Id         = UserID,
                FirstName  = "Customer",
                LastName   = UserID.ToString(),
                DateActive = DateTime.Now,
                Payload    = getRandomString(SimpleLoadTestSettings<SQLRequest>.MessageMin, SimpleLoadTestSettings<SQLRequest>.MessageMax)
            };

            if (sqlDAL.Set(customer) != true)
            {
                Console.WriteLine("Unable to set key: {0}", UserID);
            }
        }
    }
}
