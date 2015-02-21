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
using System.Data;
using System.Data.SqlClient;
using System.Linq;


namespace BasicDBLoadTest
{
    public class SQL_DAL : IDisposable
    {
        private string _connStr = string.Empty;
        private SqlConnection _conn;
    
        /// <summary>
        /// Configuration string for your database
        /// </summary>
        /// <param name="configurationString"></param>
        public SQL_DAL(string connectionString)
        {
            _connStr = connectionString;
            _conn = new SqlConnection(_connStr);
            _conn.Open();
        }
        
        ~SQL_DAL()
        {
            _dispose(false);
        }

        /// <summary>
        /// Creates a customer in SQL
        /// </summary>
        /// <param name="cust"></param>
        /// <returns>bool - Success?</returns>
        public bool Set(Customer cust)
        {
            int? execRetVal = null;
            bool ret = false;

            using (SqlCommand cmd = new SqlCommand("Customer_Insert", _conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@Id", cust.Id));
                cmd.Parameters.Add(new SqlParameter("@FirstName", cust.FirstName));
                cmd.Parameters.Add(new SqlParameter("@LastName", cust.LastName));
                cmd.Parameters.Add(new SqlParameter("@DateActive", cust.DateActive));
                cmd.Parameters.Add(new SqlParameter("@Payload", cust.Payload));
                execRetVal = cmd.ExecuteNonQuery();
            }
    
            // handle the return
            if (execRetVal > 0)
            {
                ret = true;
            }

            return ret;
        }

        /// <summary>
        /// Selects a customer by Id
        /// </summary>
        /// <param name="Id">Id to select</param>
        /// <returns>Customer</returns>
        public Customer Get(Guid Id)
        {
            IDataReader dr;

            using (SqlCommand cmd = new SqlCommand("Customer_Select", _conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@Id", Id));
                dr = cmd.ExecuteReader();
            }

            return SQL_Factory.MakeCustomers(dr).First();
        }

        /// <summary>
        /// Deletes all data in the Customer table
        /// </summary>
        /// <returns>bool -- Success?</returns>
        public bool Purge()
        {
            var ret = false;
            int? execRetVal = null;

            using (SqlCommand cmd = new SqlCommand("Customer_DeleteAllRecords", _conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;                   
                execRetVal      = cmd.ExecuteNonQuery();
            }

            // handle the return
            if (execRetVal > 0)
            {
                ret = true;
            }

            return ret;
        }

        public void Dispose()
        {
            _dispose(true);
        }

        private void _dispose(bool isDisposing)
        {
            if (_conn != null)
            {
                _conn.Close();
                _conn.Dispose();
                _conn = null;
            }

            if (isDisposing == true)
            {
                GC.SuppressFinalize(this);
            }
        }
    }

    public static class SQL_Factory
    {
        /// <summary>
        /// makes customer objects from a reader
        /// </summary>
        /// <param name="dr">datareader</param>
        /// <returns>List of Customer</returns>
        public static List<Customer> MakeCustomers(IDataReader dr)
        {
            var ret = new List<Customer>();

            while(dr.Read())
            {
                var retCustomer        = new Customer();
                retCustomer.Id         = Guid.Parse(dr["Id"].ToString());
                retCustomer.FirstName  = dr["FirstName"].ToString();
                retCustomer.LastName   = dr["LastName"].ToString();
                retCustomer.DateActive = DateTime.Parse(dr["DateActive"].ToString());
                retCustomer.Payload    = dr["Payload"].ToString();
                
                ret.Add(retCustomer);
            }

            dr.Close();
            dr.Dispose();
            
            return ret;
        }        
    }
}
