using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data.SqlClient;
using System.Data;

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


    // Helper to build / execute DbCommands
    public static class SqlRunner
    {
        static IDbCommand BuildDbCommand(IDbConnection db, SqlQueryWithParams q, int commandTimeout=30)
        {
            IDbCommand cmd = db.CreateCommand();
            cmd.CommandText = q.Query;
            cmd.CommandTimeout = commandTimeout;

            if (q.Parameters != null)
            {
                foreach (var p in q.Parameters)
                {
                    IDbDataParameter dbparm = cmd.CreateParameter();
                    dbparm.ParameterName = p.Name;
                    dbparm.Value = p.Value;
                    cmd.Parameters.Add(dbparm);
                }
            }

            return cmd;
        }

        public static int DbExecuteNonQuery(this IDbConnection db, SqlQueryWithParams qp, IDbTransaction transaction, int commandTimeout=30)
        {
            IDbCommand cmd = BuildDbCommand(db, qp, commandTimeout);

            if (transaction != null)
                cmd.Transaction = transaction;

            return cmd.ExecuteNonQuery();
        }

        public static T DbExecuteScalar<T>(this IDbConnection db, SqlQueryWithParams qp)
        {
            IDbCommand cmd = BuildDbCommand(db, qp);

            return (T)cmd.ExecuteScalar();
        }
    }
}
