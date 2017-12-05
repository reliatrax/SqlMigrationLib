using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlMigrationLib.DbTests
{
    public class MigrationDescription
    {
        public int MigrationID { get; set; }
        public string MigrationSql { get; set; }
    }

    public class ExecutedBatch
    {
        public string ExecutedSql { get; private set; }
        public int RowsAffected { get; private set; }

        public ExecutedBatch( string executesSql, int rowsAffected )
        {
            ExecutedSql = executesSql;
            RowsAffected = rowsAffected;
        }
    }

    public class MoqMigrationUtils : ISqlMigrationUtils<int>
    {
        public DateTime TimeVersionLastSet { get; private set; }

        MigrationDescription[] Migrations { get; set; }

        public MoqMigrationUtils(IEnumerable<MigrationDescription> migrations)
        {
            Migrations = migrations.ToArray();
        }

        public int[] ListRequiredMigrationScripts(int currentDBVersion, int requiredVersion)
        {
            return Migrations.Where(x => x.MigrationID > currentDBVersion && x.MigrationID <= requiredVersion)
                             .Select(x => x.MigrationID)
                             .ToArray();
        }

        public string ReadMigrationScript(int migrationName)
        {
            return Migrations.Single(x => x.MigrationID == migrationName).MigrationSql;
        }

        public SqlQueryWithParams GetDBVersionQuery()
        {
            return new SqlQueryWithParams(@"SELECT TOP 1 MigrationID FROM dbo.Migrations ORDER BY MigrationID DESC");
        }

        public SqlQueryWithParams SetDBVersionQuery(int ver)
        {
            // Current time, truncated to the nearest second.  Sql has less precision on DateTime type, if we keep it to the nearest second, it round trips perfectly
            TimeVersionLastSet = DateTime.UtcNow.AddTicks(-1 * (DateTime.Now.Ticks % TimeSpan.TicksPerSecond));

            return new SqlQueryWithParams(@"INSERT INTO dbo.Migrations(MigrationID,UpdateUTC) VALUES(@p1,@p2)", new SqlParm("p1", ver), new SqlParm("p2", TimeVersionLastSet));
        }

        // Logging

        List<string> informationLog = new List<string>();

        public IReadOnlyList<string> InformationLog => informationLog.AsReadOnly();

        public void LogInformation(string message, params object[] args)
        {
            string fmtd = string.Format(message, args);

            informationLog.Add(fmtd);
        }

        // Log Executed SQL Batches

        List<ExecutedBatch> sqlLog = new List<ExecutedBatch>();
        public IReadOnlyList<ExecutedBatch> ExecutedBatches => sqlLog.AsReadOnly();

        public void LogSqlBatch(string sql, int rowsAffected)
        {
            sqlLog.Add(new ExecutedBatch(sql, rowsAffected));
        }

        // Log Errors

        public string LastErrorMessage { get; private set; }

        public void LogError(Exception e, string message, params object[] args)
        {
            LastErrorMessage = e.Message;       // "Log" the error message
        }
    }
}
