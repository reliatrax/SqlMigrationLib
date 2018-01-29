using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using Dapper;

namespace SqlMigrationLib.DbTests
{
    static class DBUtils
    {
        public static string GetTestDataBaseName()
        {
            return ConfigurationManager.AppSettings["dbname"];
        }

        public static string GetConnectionString(string databaseName = null)
        {
            string connectionStringBase = ConfigurationManager.AppSettings["constringbase"];

            if (string.IsNullOrEmpty(databaseName))
                databaseName = GetTestDataBaseName();

            string constring = connectionStringBase + $" DataBase={databaseName};";

            return constring;
        }

        public static void SetupDatabase()
        {
            string testDBName = GetTestDataBaseName();

            // Create the database -- we need to connect using "master" here since our test DB may not exist
            using (IDbConnection db = new SqlConnection(GetConnectionString("master")))
            {
                db.Open();

                // Check if the database already exists
                string existsquery = string.Format(@"SELECT count(*) FROM master.dbo.sysdatabases WHERE ('[' + name + ']' = '{0}' OR name = '{0}')", testDBName);
                int dbCount = db.ExecuteScalar<int>(existsquery);

                if (dbCount == 0)
                    InitializeDataBase(db, testDBName);  // create the dB & add our table

                db.Close();
            }
        }

        public static void AddMigrationRow(IDbConnection db, int migrationID, DateTime updateUtc)
        {
            string sql = @"INSERT INTO dbo.MigrationHistories(MigrationID, UpdateUTC) VALUES(@migrationID, @updateUTC)";

            db.Execute(sql, new { migrationID = migrationID, updateUTC = updateUtc });
        }


        public static void AddMessage(IDbConnection db, string message)
        {
            string sql = @"INSERT INTO dbo.Messages(MessageText) VALUES(@p1)";

            db.Execute(sql, new { p1 = message });
        }


        public static void InitializeDataBase(IDbConnection db, string dbname)
        {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            // Create our directory
            string path = Path.Combine(programData, "CCS", "SqlMigrationLibDbTests");
            if (Directory.Exists(path) == false)
                Directory.CreateDirectory(path);

            // create the database
            string dataPath = Path.Combine(path, "sqlMigrationLib.mdf");
            string logPath = Path.Combine(path, "sqlMigrationLib.ldf");

            // Note that sql parameters are not allowed for DDL operations like CREATE DATABASE, so we have to use string.Format
            string query = string.Format(@"CREATE DATABASE {0} CONTAINMENT=PARTIAL
                                    ON (NAME='Data',
                                        FILENAME='{1}',
                                        SIZE=4, FILEGROWTH=10%)
                                    LOG ON (NAME='Log',
                                        FILENAME='{2}',
                                        SIZE=4, FILEGROWTH=10%)", dbname, dataPath, logPath);

            db.Execute(query);
        }

        public static void DropAndAddTables(IDbConnection db)
        {
            string query = @"DROP TABLE IF EXISTS dbo.MigrationHistories;
                             DROP TABLE IF EXISTS dbo.Messages;";

            db.Execute(query);

            // Add the migration history table
            query = @"CREATE TABLE dbo.MigrationHistories (
                                    MigrationID INT NOT NULL,		-- Primary key
                                    UpdateUTC DateTime NOT NULL,	-- UTC Time this migration was run
                                    CONSTRAINT PK_MigrationID PRIMARY KEY CLUSTERED  (MigrationID ASC)
                                  );";

            db.Execute(query);

            // Add a messages table
            query = @"CREATE TABLE dbo.Messages (
                                    MessageID INT IDENTITY(1,1) NOT NULL,   -- Primary key
                                    MessageText VARCHAR(100) NOT NULL,	-- Some message text
                                    CONSTRAINT PK_MessageID PRIMARY KEY CLUSTERED  (MessageID ASC)
                                  );";

            db.Execute(query);
        }
    }
}
