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

    public class ExecutingBatch
    {
        public int BatchNumber { get; private set; }
        public string ExecutedSql { get; private set; }

        public ExecutingBatch(int batchNumber, string executesSql )
        {
            ExecutedSql = executesSql;
            BatchNumber = batchNumber;
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
            return new SqlQueryWithParams(@"SELECT TOP 1 MigrationID FROM dbo.MigrationHistories ORDER BY UpdateUTC DESC");
        }

        public SqlQueryWithParams SetDBVersionQuery(int ver)
        {
            // Current time, truncated to the nearest second.  Sql has less precision on DateTime type, if we keep it to the nearest second, it round trips perfectly
            TimeVersionLastSet = DateTime.UtcNow.AddTicks(-1 * (DateTime.Now.Ticks % TimeSpan.TicksPerSecond));

            return new SqlQueryWithParams(@"INSERT INTO dbo.MigrationHistories(MigrationID,UpdateUTC) VALUES(@p1,@p2)", new SqlParm("p1", ver), new SqlParm("p2", TimeVersionLastSet));
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

        List<ExecutingBatch> sqlLog = new List<ExecutingBatch>();
        public IReadOnlyList<ExecutingBatch> ExecutingBatches => sqlLog.AsReadOnly();

        public void LogSqlBatch(int batchNum, string sql)
        {
            sqlLog.Add(new ExecutingBatch(batchNum, sql));
        }

        // Log Errors

        public string LastErrorMessage { get; private set; }

        public void LogError(Exception e, string message, params object[] args)
        {
            LastErrorMessage = e.Message;       // "Log" the error message
        }

        // Returns the special version to use while running the migration
        public int GetInProgressVer(int migrationVer)
        {
            return -migrationVer;
        }
    }
}
