using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data.SqlClient;

namespace SqlMigrationLib
{
    public class SqlParm
    {
        public string Name { get; private set; }
        public object Value { get; private set; }

        public SqlParm(string name, object value)
        {
            Name = name;
            Value = value;
        }
    }

    public class SqlQueryWithParams
    {
        public string Query { get; private set; }
        public SqlParm[] Parameters { get; private set; }

        public SqlQueryWithParams(string query, params SqlParm[] parameters)
        {
            Query = query;
            Parameters = parameters;
        }
    }


    public class SqlRunner : IDisposable
    {
        SqlConnection sqlConx = null;

        public SqlRunner( string connectionString )
        {
            sqlConx = new SqlConnection(connectionString);
            sqlConx.Open();
        }

        static SqlCommand BuildSqlCommand(SqlConnection sqlConx, SqlQueryWithParams q)
        {
            SqlCommand sqlcmd = new SqlCommand(q.Query, sqlConx);

            if (q.Parameters != null)
            {
                foreach (var p in q.Parameters)
                    sqlcmd.Parameters.AddWithValue(p.Name, p.Value);
            }

            return sqlcmd;
        }

        public int ExecuteNonQuery(string query, params SqlParm[] parms)
        {
            var qp = new SqlQueryWithParams(query, parms);

            return ExecuteNonQuery(qp);
        }

        public int ExecuteNonQuery(SqlQueryWithParams qp)
        {
            SqlCommand cmd = BuildSqlCommand(sqlConx, qp);

            return cmd.ExecuteNonQuery();
        }

        public T ExecuteScalar<T>(string query, params SqlParm[] parms)
        {
            var qp = new SqlQueryWithParams(query, parms);

            return ExecuteScalar<T>(qp);
        }

        public T ExecuteScalar<T>(SqlQueryWithParams qp)
        {
            SqlCommand cmd = BuildSqlCommand(sqlConx, qp);

            return (T)cmd.ExecuteScalar();
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing && sqlConx != null)
                {
                    sqlConx.Close();
                    sqlConx.Dispose();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
