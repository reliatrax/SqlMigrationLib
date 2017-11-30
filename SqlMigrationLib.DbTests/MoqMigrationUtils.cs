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

    public class MoqMigrationUtils : ISqlMigrationUtils<int>
    {
        public DateTime TimeVersionLastSet { get; private set; }

        MigrationDescription[] Migrations { get; set; }

        public MoqMigrationUtils(IEnumerable<MigrationDescription> migrations)
        {
            Migrations = migrations.ToArray();
        }

        public int CompareVersions(int a, int b)
        {
            if (a < b)
                return -1;
            else if (a > b)
                return 1;
            else
                return 0;
        }

        public int[] ListAvailableMigrationScripts(int currentDBVersion, int requiredVersion)
        {
            return Migrations.Where(x => CompareVersions(x.MigrationID, currentDBVersion) > 0 && CompareVersions(x.MigrationID, requiredVersion) <= 0)
                             .Select(x => x.MigrationID)
                             .ToArray();
        }

        public string ReadMigrationScript(int migrationName)
        {
            return Migrations.Single(x => x.MigrationID == migrationName).MigrationSql;
        }

        public SqlQueryWithParams GetDBVersionQuery()
        {
//                ConnectionString = @"Data Source =.\sqlexpress; DataBase = SqlMigrationLibTestDB; Integrated Security = SSPI;",

            return new SqlQueryWithParams(@"SELECT TOP 1 MigrationID FROM dbo.Migrations ORDER BY MigrationID DESC");
        }

        public SqlQueryWithParams SetDBVersionQuery(int ver)
        {
            // Current time, truncated to the nearest second.  Sql has less precision on DateTime type, if we keep it to the nearest second, it round trips perfectly
            TimeVersionLastSet = DateTime.UtcNow.AddTicks(-1 * (DateTime.Now.Ticks % TimeSpan.TicksPerSecond));

            return new SqlQueryWithParams(@"INSERT INTO dbo.Migrations(MigrationID,UpdateUTC) VALUES(@p1,@p2)", new SqlParm("p1", ver), new SqlParm("p2", TimeVersionLastSet));
        }
    }

}
