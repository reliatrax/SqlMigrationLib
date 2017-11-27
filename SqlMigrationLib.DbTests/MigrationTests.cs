using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SqlMigrationLib;
using System.Data.SqlClient;
using System.Data;
using Dapper;
using FluentAssertions;

namespace SqlMigrationLib.DbTests
{
    public class Migration
    {
        public int MigrationID { get; set; }
        public DateTime UpdateUTC { get; set; }

        public Migration() { }
        
        public Migration(int migrationID, DateTime updateUTC)
        {
            MigrationID = migrationID;
            UpdateUTC = updateUTC;
        }
    }

    public class Message
    {
        public int MessageID { get; set; }
        public string MessageText { get; set; }
    }

    [TestFixture]
    [Category("IntegrationTest")]
    public class MigrationTests
    {

        public class MigrationDescription
        {
            public int MigrationID { get; set; }
            public string MigrationSql { get; set; }
        }

        public class MigrationUtils : ISqlMigrationUtils<int>
        {
            MigrationDescription[] Migrations { get; set; }

            public MigrationUtils( IEnumerable<MigrationDescription> migrations )
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
        }

        [Test]
        public void TestMethod()
        {
            MigrationDescription[] migrations = new MigrationDescription[]
            {
                new MigrationDescription { MigrationID = 101, MigrationSql = @"INSERT INTO dbo.Messages(MessageText) VALUES('Ran migration 101')" }
            };

            MigrationUtils utils = new MigrationUtils( migrations);

            DateTime updateDT = DateTime.UtcNow;

            SqlUpdateConfig config = new SqlUpdateConfig
            {
                ConnectionString = @"Data Source =.\sqlexpress; DataBase = SqlMigrationLibDB; Integrated Security = SSPI;",
                GetDBVersionQuery = () => new SqlQueryWithParams(@"SELECT TOP 1 MigrationID FROM dbo.Migrations ORDER BY MigrationID DESC"),
                SetDBVersionQuery = () => new SqlQueryWithParams(@"INSERT INTO dbo.Migrations(MigrationID,UpdateUTC) VALUES(@p1,@p2)", new SqlParm("p1", 101), new SqlParm("p2", updateDT) )
            };

            MigrationRunner<int> runner = new MigrationRunner<int>(config, utils);

            // Act
            runner.Run(101);

            // Assert
            using (IDbConnection db = new SqlConnection(config.ConnectionString))
            {
                // Check messages (this ensures that the migration itself is run)
                string[] messages = db.Query<Message>("Select * From Messages").Select( x => x.MessageText ).ToArray();

                messages.Should().HaveCount(1);
                messages[0].Should().Be("Ran migration 101");


                // Check that the Migrations table was updated (this ensures that othe SetDBVersionQuery is run)
                Migration[] dbMigrations = db.Query<Migration>("Select * From Migrations").ToArray();

                dbMigrations.Select(x => x.MigrationID).Should().BeEquivalentTo(new int[] { 100, 101 });

                dbMigrations.Single(x => x.MigrationID == 101).UpdateUTC.Should().Be(updateDT);
            }
        }
    }
}
