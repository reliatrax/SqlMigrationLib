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
    public class MigrationDBTests
    {
        SqlMigrationLibConfig config;

        [SetUp]
        public void Setup()
        {
            config = new SqlMigrationLibConfig
            {
                ConnectionString = DBUtils.GetConnectionString()        // gets a connection string to our test database
            };

            using (IDbConnection db = new SqlConnection(config.ConnectionString))
            {
                db.Open();

                DBUtils.DropAndAddTables(db);                 // clear out all old data

                // Seed it with some data
                DBUtils.AddMigrationRow(db, 100, DateTime.UtcNow);
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

            using (IDbConnection db = new SqlConnection(config.ConnectionString))
            {
                MigrationRunner<int> runner = new MigrationRunner<int>(db, utils);

                // Act
                runner.BringToVersion(101);

                // Assert
                // Check messages (this ensures that the migration itself is run)
                string[] messages = db.Query<Message>("Select * From Messages").Select(x => x.MessageText).ToArray();

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

            using (IDbConnection db = new SqlConnection(config.ConnectionString))
            {
                MigrationRunner<int> runner = new MigrationRunner<int>(db, utils);

                // Act
                runner.BringToVersion(101);

                // Assert
                // Check messages (this ensures that the migration itself is run)
                string[] messages = db.Query<Message>("Select * From Messages order by MessageID").Select(x => x.MessageText).ToArray();

                messages.Should().BeEquivalentTo(new string[]
                {
                    "Message 1",
                    "Message 2"
                });
            }
        }

        [Test]
        public void TestMigration_ExceptionsShouldBeLogged()
        {
            MigrationDescription[] migrations = new MigrationDescription[]
            {
                new MigrationDescription { MigrationID = 101, MigrationSql =
                    @"SELECT * from dbo.UNKNOWNTABLE;"
                }
            };

            MoqMigrationUtils utils = new MoqMigrationUtils(migrations);

            using (IDbConnection db = new SqlConnection(config.ConnectionString))
            {
                MigrationRunner<int> runner = new MigrationRunner<int>(db, utils);

                // Act
                runner.BringToVersion(101);

                // Assert
                utils.LastErrorMessage.Should().BeEquivalentTo("Invalid object name 'dbo.UNKNOWNTABLE'.");
            }
        }

        [Test]
        public void TestMigration_ExceptionsShouldCauseRollback()
        {
            using (IDbConnection db = new SqlConnection(config.ConnectionString))
            {
                // Arrange
                DBUtils.AddMessage(db, "Message added before migration");

                MigrationDescription[] migrations = new MigrationDescription[]
                {
                    new MigrationDescription { MigrationID = 101, MigrationSql =
                        @"INSERT INTO dbo.Messages(MessageText) VALUES('This should be rolled back');
                          SELECT * from dbo.UNKNOWNTABLE;"         // SQL error
                    }
                };

                MoqMigrationUtils utils = new MoqMigrationUtils(migrations);

                MigrationRunner<int> runner = new MigrationRunner<int>(db, utils);

                // Act
                runner.BringToVersion(101);

                // Assert
                // Check messages (this ensures that the migration itself is run)
                string[] messages = db.Query<Message>("Select * From Messages order by MessageID").Select(x => x.MessageText).ToArray();

                messages.Should().BeEquivalentTo(new string[]
                {
                    "Message added before migration"
                    // Note the message 'This should be rolled back' added during the migration should not appear
                });
            }
        }

        [Test]
        public void TestMigration_ShouldLogAllExecutedSql()
        {
            using (IDbConnection db = new SqlConnection(config.ConnectionString))
            {
                // Arrange
                MigrationDescription[] migrations = new MigrationDescription[]
                {
                    new MigrationDescription { MigrationID = 101, MigrationSql =
                        "INSERT INTO dbo.Messages(MessageText) VALUES('Batch 1');\n" +            // 1 row affected
                        "GO\n" +
                        "INSERT INTO dbo.Messages(MessageText) VALUES('Batch 2');\n" +            // 1 row affected
                        "GO\n" +
                        "Update dbo.Messages set MessageText = 'updated message'\n"           // 2 rows affected
                    }
                };

                MoqMigrationUtils utils = new MoqMigrationUtils(migrations);

                MigrationRunner<int> runner = new MigrationRunner<int>(db, utils);

                // Act
                runner.BringToVersion(101);

                // Assert
                utils.ExecutedBatches.Should().HaveCount(4);        // Begin Transaction + 3 batches + Update Version + End Transaction

                utils.ExecutedBatches[0].ExecutedSql.Should().Be("INSERT INTO dbo.Messages(MessageText) VALUES('Batch 1');");
                utils.ExecutedBatches[1].ExecutedSql.Should().Be("INSERT INTO dbo.Messages(MessageText) VALUES('Batch 2');");
                utils.ExecutedBatches[2].ExecutedSql.Should().Be("Update dbo.Messages set MessageText = 'updated message'");
                utils.ExecutedBatches[3].ExecutedSql.Should().StartWithEquivalent("INSERT INTO dbo.Migrations(MigrationID,UpdateUTC) VALUES(@p1,@p2)\nPARAMETERS:\n   p1: 101");

                utils.ExecutedBatches.Select(x => x.RowsAffected).ShouldBeEquivalentTo(new int[] { 1, 1, 2, 1 }, options => options.WithStrictOrdering());
            }
        }
    }
}
