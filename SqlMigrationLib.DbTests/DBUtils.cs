using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlMigrationLib.DbTests
{
    static class DBUtils
    {
        static string connectionStringBase = @"Data Source=.\sqlexpress; Integrated Security=SSPI;";

        static string testDBName = "SqlMigrationLibTestDB";

        public static SqlRunner GetSqlRunner(string databaseName = null )
        {
            if (string.IsNullOrEmpty(databaseName))
                databaseName = testDBName;

            string constring = connectionStringBase + $" DataBase={databaseName};";

            return new SqlRunner(constring);
        }

        public static void SetupDatabase()
        {
            // Create the database -- we need to connect using "master" here since our test DB may not exist
            using (SqlRunner r = GetSqlRunner("master"))
            {
                // Check if the database already exists
                string existsquery = string.Format(@"SELECT count(*) FROM master.dbo.sysdatabases WHERE ('[' + name + ']' = '{0}' OR name = '{0}')", testDBName);
                int dbCount = r.ExecuteScalar<int>(existsquery);

                if (dbCount == 0)
                    InitializeDataBase(r, testDBName);  // create the dB & add our table
            }
        }

        public static void AddMigrationRow(SqlRunner r, int migrationID, DateTime updateUtc)
        {
            string sql = @"INSERT INTO dbo.Migrations(MigrationID, UpdateUTC) VALUES(@migrationID, @updateUTC)";

            r.ExecuteNonQuery(sql, new SqlParm("@migrationID", migrationID), new SqlParm("@updateUTC", updateUtc));
        }

        public static void InitializeDataBase(SqlRunner r, string dbname)
        {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            // Create our directory
            string path = Path.Combine(programData, "SqlMigrationLibDbTests");
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

            r.ExecuteNonQuery(query);
        }

        public static void DropAndAddTables(SqlRunner r)
        {
            string query = @"DROP TABLE IF EXISTS dbo.Migrations;
                             DROP TABLE IF EXISTS dbo.Messages;";

            r.ExecuteNonQuery(query);

            // Add the migration history table
            query = @"CREATE TABLE dbo.Migrations (
                                    MigrationID INT NOT NULL,		-- Primary key
                                    UpdateUTC DateTime NOT NULL,	-- UTC Time this migration was run
                                    CONSTRAINT PK_MigrationID PRIMARY KEY CLUSTERED  (MigrationID ASC)
                                  );";

            r.ExecuteNonQuery(query);

            // Add a messages table
            query = @"CREATE TABLE dbo.Messages (
                                    MessageID INT IDENTITY(1,1) NOT NULL,   -- Primary key
                                    MessageText VARCHAR(100) NOT NULL,	-- Some message text
                                    CONSTRAINT PK_MessageID PRIMARY KEY CLUSTERED  (MessageID ASC)
                                  );";

            r.ExecuteNonQuery(query);
        }
    }
}
