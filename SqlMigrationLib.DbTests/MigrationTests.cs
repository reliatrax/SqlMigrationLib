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
    [TestFixture]
    [Category("IntegrationTest")]
    public class MigrationTests
    {
        SqlMigrationLibConfig config;

        [SetUp]
        public void Setup()
        {
            config = new SqlMigrationLibConfig
            {
                ConnectionString = @"Data Source =.\sqlexpress; DataBase = SqlMigrationLibTestDB; Integrated Security = SSPI;",
            };

            using (SqlRunner r = DBUtils.GetSqlRunner())
            {
                DBUtils.DropAndAddTables(r);                 // clear out all old data

                // Seed it with some data
                DBUtils.AddMigrationRow(r, 100, DateTime.UtcNow);
            }
        }

        [Test]
        public void TestMigration1()
        {
            // Arrange
            MigrationDescription[] migrations = new MigrationDescription[]
            {
                new MigrationDescription { MigrationID = 101, MigrationSql = @"INSERT INTO dbo.Messages(MessageText) VALUES('Ran migration 101')" }
            };

            MoqMigrationUtils utils = new MoqMigrationUtils( migrations);

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

                dbMigrations.Single(x => x.MigrationID == 101).UpdateUTC.Should().Be(utils.TimeVersionLastSet);
            }
        }

        [Test]
        public void TestMigration_ShouldExcludeComments()
        {
            MigrationDescription[] migrations = new MigrationDescription[]
            {
                new MigrationDescription { MigrationID = 101, MigrationSql =
                    @"INSERT INTO dbo.Messages(MessageText) VALUES('Message 1');
                      --INSERT INTO dbo.Messages(MessageText) VALUES('Should not appear 1');
                      /*
                         INSERT INTO dbo.Messages(MessageText) VALUES('Should not appear 1');
                         INSERT INTO dbo.Messages(MessageText) VALUES('Should not appear 2');
                       */
                       INSERT INTO dbo.Messages(MessageText) VALUES('Message 2');
                     "
                }
            };

            MoqMigrationUtils utils = new MoqMigrationUtils(migrations);

            MigrationRunner<int> runner = new MigrationRunner<int>(config, utils);

            // Act
            runner.Run(101);

            // Assert
            using (IDbConnection db = new SqlConnection(config.ConnectionString))
            {
                // Check messages (this ensures that the migration itself is run)
                string[] messages = db.Query<Message>("Select * From Messages order by MessageID").Select(x => x.MessageText).ToArray();

                messages.Should().BeEquivalentTo(new string[]
                {
                    "Message 1",
                    "Message 2"
                });
            }
        }

    }
}
