using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace SqlMigrationLib
{
    public class MigrationRunner<TVer>
    {
        ISqlMigrationUtils<TVer> _migrationUtils;
        SqlMigrationLibConfig _config;

        public MigrationRunner( SqlMigrationLibConfig config, ISqlMigrationUtils<TVer> migrationUtils )
        {
            _config = config;
            _migrationUtils = migrationUtils;
        }

        public void Run(TVer requiredVersion)
        {
            using (SqlRunner runner = new SqlRunner(_config.ConnectionString))
            {
                // Get the current database version
                SqlQueryWithParams query = _migrationUtils.GetDBVersionQuery();
                TVer currentDBVersion = runner.ExecuteScalar<TVer>(query);

                // If the current version is >= the required version, then we have nothing to do
                if (_migrationUtils.CompareVersions(currentDBVersion, requiredVersion) >= 0)
                    return;

                // See if we have any migration scripts to run
                TVer[] migrationVers = _migrationUtils.ListAvailableMigrationScripts(currentDBVersion, requiredVersion);

                // Run each migration script in turn
                foreach (TVer migrationVer in migrationVers)
                {
                    string transname = "T" + DateTime.Now.Ticks.ToString();     // a unique name
                    bool shouldRollback = false;

                    try
                    {
                        string sql = _migrationUtils.ReadMigrationScript(migrationVer);

                        // Begin transaction
                        runner.ExecuteNonQuery(string.Format("BEGIN TRANSACTION {0}", transname));      // DDL does not support SQL parameters
                        shouldRollback = true;

                        // Run the migration!
                        RunMigration(runner, sql);

                        // Update the DB version if required (the migration script itself may do this)
                        UpdateDBVersion(runner, migrationVer);

                        runner.ExecuteNonQuery(string.Format("COMMIT TRANSACTION {0}", transname));      // DDL does not support SQL parameters
                    }
                    catch (Exception e)
                    {
                        // TODO: log!

                        if (shouldRollback)
                            RollbackTransaction(runner, transname);
                        return;     // Abort after the first failure
                    }
                }
            }
        }

        private static void RollbackTransaction(SqlRunner runner, string transname)
        {
            try
            {
                runner.ExecuteNonQuery(new SqlQueryWithParams("ROLLBACK TRANSACTION {name}", new SqlParm("name", transname)));
            }
            catch (Exception ex)
            {
                // #TODO - log critical error
            }
        }

        private void RunMigration(SqlRunner runner, string sql)
        {
            sql = CommentStripper.ProcessSql(sql);  // remove all comments, because they may contain the word "GO" in them which would confuse our splitter

            string[] batches = SqlBatchSplitter.SplitBatches(sql);

            foreach (string batch in batches)
            {
                runner.ExecuteNonQuery(batch);
            }
        }

        private void UpdateDBVersion(SqlRunner runner, TVer ver)
        {
            SqlQueryWithParams query = _migrationUtils.SetDBVersionQuery(ver);     // call the user-supplied delegate to get the query

            // The set version query is not required.  The migration script itself may set the version
            if (query == null)
                return;

            runner.ExecuteNonQuery(query);
        }
    }
}
