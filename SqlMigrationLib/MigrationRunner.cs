using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace SqlMigrationLib
{
    public class MigrationRunner<TVer>
    {
        ISqlMigrationUtils<TVer> _migrationUtils;
        IDbConnection _db;

        public MigrationRunner( IDbConnection db, ISqlMigrationUtils<TVer> migrationUtils )
        {
            _db = db;
            _migrationUtils = migrationUtils;
        }

        public void BringToVersion(TVer requiredVersion)
        {
            // Ensure that the connection is opened (otherwise executing the command will fail)
            ConnectionState originalState = _db.State;
            if (originalState != ConnectionState.Open)
                _db.Open();

            // Get the current database version
            TVer currentDBVersion;

            try
            {
                _migrationUtils.LogInformation("Getting current database version");
                SqlQueryWithParams query = _migrationUtils.GetDBVersionQuery();
                currentDBVersion = _db.DbExecuteScalar<TVer>(query);
            }
            catch ( Exception e)
            {
                _migrationUtils.LogError(e, "Exception in GetDBVersionQuery: {0}", e.Message);
                return;
            }

            // See if we have any migration scripts to run
            TVer[] migrationVers;

            try
            {
                _migrationUtils.LogInformation("Calling ListRequiredMigrationScripts({0},{1})", currentDBVersion, requiredVersion);
                migrationVers = _migrationUtils.ListRequiredMigrationScripts(currentDBVersion, requiredVersion);

                if (migrationVers == null || migrationVers.Length == 0)
                {
                    _migrationUtils.LogInformation("No migrations required");
                    return;
                }

                _migrationUtils.LogInformation("Migrations required: {0}", string.Join(", ", migrationVers) );
            }
            catch ( Exception e)
            {
                _migrationUtils.LogError(e, "Exception in ListRequiredMigrationScripts({0},{1})", currentDBVersion, requiredVersion);
                return;
            }

            // Run each migration script in turn
            foreach (TVer migrationVer in migrationVers)
            {
                string transname = "T" + DateTime.Now.Ticks.ToString();     // a unique name
                bool shouldRollback = false;

                try
                {
                    _migrationUtils.LogInformation("Starting Migration {0}", migrationVer);

                    string sql = _migrationUtils.ReadMigrationScript(migrationVer);

                    // Begin transaction
                    _migrationUtils.LogInformation("Beginning Transaction {0}", transname);
                    Execute($"BEGIN TRANSACTION {transname}");      // DDL does not support SQL parameters
                    shouldRollback = true;

                    // Run the migration!
                    _migrationUtils.LogInformation("Running Migration {0}", migrationVer);
                    RunMigration(sql);

                    // Update the DB version if required (the migration script itself may do this)
                    _migrationUtils.LogInformation("Updating Database version to {0}", migrationVer);
                    UpdateDBVersion(migrationVer);

                    _migrationUtils.LogInformation("Commiting transaction {0}", transname);
                    Execute($"COMMIT TRANSACTION {transname}");      // DDL does not support SQL parameters

                    _migrationUtils.LogInformation("Finished Migration {0}", migrationVer);
                }
                catch (Exception e)
                {
                    _migrationUtils.LogError(e, "Exception encountered while running migration {0}: {1}", migrationVer, e.Message);

                    if (shouldRollback)
                        RollbackTransaction(transname);
                    return;     // Abort after the first failure
                }
            }
        }

        private void RollbackTransaction(string transname)
        {
            try
            {
                _migrationUtils.LogInformation("Rolling back transaction {0}", transname);

                Execute($"ROLLBACK TRANSACTION {transname}");            // DDL does not support SQL parameters

                _migrationUtils.LogInformation("Rolled back transaction {0} successfully!", transname);
            }
            catch (Exception ex)
            {
                _migrationUtils.LogError(ex, "Error rolling back transaction {0}: {1}", transname, ex.Message);
            }
        }


        private void RunMigration(string sql)
        {
            sql = CommentStripper.ProcessSql(sql);  // remove all comments, because they may contain the word "GO" in them which would confuse our splitter

            string[] batches = SqlBatchSplitter.SplitBatches(sql);      // split on GO

            foreach (string batch in batches)
            {
                // Regularize line endings in the batch (makes the sql log easier to read)
                string s = batch.Replace("\r\n", "\n");
                s = Regex.Replace(s, "\n+", "\n");

                Execute(s);
            }
        }

        private void UpdateDBVersion(TVer ver)
        {
            SqlQueryWithParams query = _migrationUtils.SetDBVersionQuery(ver);     // call the user-supplied delegate to get the query

            // The set version query is not required.  The migration script itself may set the version
            if (query == null)
                return;

            Execute(query);
        }

        // Execute queries, logging the SQL and rows affected
        private void Execute(string sql)
        {
            sql = sql.Trim();       // trimming makes the log messages clearer

            int rowsAffected = _db.DbExecuteNonQuery(sql);

            _migrationUtils.LogSqlBatch(sql, rowsAffected);
        }

        private void Execute(SqlQueryWithParams sql)
        {
            int rowsAffected = _db.DbExecuteNonQuery(sql);

            string logmsg = sql.Query + "\nPARAMETERS:\n" + string.Join("\n", sql.Parameters.Select(p => $"   {p.Name}: {p.Value}"));
            logmsg = logmsg.Trim();       // trimming makes the log messages clearer

            _migrationUtils.LogSqlBatch(logmsg, rowsAffected);
        }
    }
}
