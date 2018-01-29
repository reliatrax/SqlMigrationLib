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

            try
            {
                BringToVersionWorker(requiredVersion);
            }
            catch( Exception e )
            {
                _migrationUtils.LogError(e, "Exception while executing BringToVersionWorker");

                // Attempt to close the DB
                if (originalState != ConnectionState.Open)
                {
                    TryCloseDbConnection();
                }
            }
        }

        private void TryCloseDbConnection()
        {
            try
            {
                _db.Close();
            }
            catch (Exception eclose)
            {
                _migrationUtils.LogError(eclose, "Exception while closing the DB connection after finished");
            }
        }

        private void BringToVersionWorker(TVer requiredVersion)
        {
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
                    _migrationUtils.LogInformation("No migrations required from {0} to {1}", currentDBVersion, requiredVersion);
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
                _migrationUtils.LogInformation("Starting Migration {0}", migrationVer);

                IDbTransaction transaction;
                string sql;

                try
                {
                    // Read the Migration script
                    sql = _migrationUtils.ReadMigrationScript(migrationVer);

                    // Begin transaction
                    _migrationUtils.LogInformation("Beginning Transaction");
                    transaction = _db.BeginTransaction();
                }
                catch (Exception e)
                {
                    _migrationUtils.LogError(e, "Exception encountered while running migration {0}: {1}", migrationVer, e.Message);
                    return;
                }

                try {
                    // Update the DB version indicatating that we're in progress
                    TVer inProgressVer = _migrationUtils.GetInProgressVer(migrationVer);
                    UpdateDBVersion(transaction, inProgressVer);

                    // Run the migration!
                    RunMigration(transaction, migrationVer, sql);

                    // Update the DB version to the final version
                    UpdateDBVersion(transaction, migrationVer);

                    _migrationUtils.LogInformation("Commiting transaction");
                    transaction.Commit();
                }
                catch (Exception e)
                {
                    _migrationUtils.LogError(e, "Exception encountered while running migration {0} on {1}: {2}", migrationVer, _db.Database, e.Message);

                    RollbackTransaction(transaction);

                    return;     // Abort after the first failure
                }

                _migrationUtils.LogInformation("Finished Migration {0}", migrationVer);
            }
        }

        private void RollbackTransaction(IDbTransaction transaction)
        {
            try
            {
                _migrationUtils.LogInformation("Rolling back transaction");

                transaction.Rollback();

                _migrationUtils.LogInformation("Rolled back transaction successfully!");
            }
            catch (Exception ex)
            {
                _migrationUtils.LogError(ex, "Error rolling back transaction: {0}", ex.Message);
            }
        }


        private void RunMigration(IDbTransaction transaction, TVer migrationVer, string sql)
        {
            sql = CommentStripper.ProcessSql(sql);  // remove all comments, because they may contain the word "GO" in them which would confuse our splitter

            string[] batches = SqlBatchSplitter.SplitBatches(sql);      // split on GO

            _migrationUtils.LogInformation("Running Migration {0} ({1} batches) on {2}.", migrationVer, batches.Length, _db.Database);

            int batchNum = 1;

            foreach (string batch in batches)
            {
                // Regularize line endings in the batch (makes the sql log easier to read)
                string s = batch.Replace("\r\n", "\n");
                s = Regex.Replace(s, "\n+", "\n");

                Execute(transaction, batchNum, s);

                ++batchNum;
            }
        }

        private void UpdateDBVersion(IDbTransaction transaction, TVer ver)
        {
            SqlQueryWithParams query = _migrationUtils.SetDBVersionQuery(ver);     // call the user-supplied delegate to get the query

            // The set version query is not required.  The migration script itself may set the version
            if (query == null)
                return;

            string sqlmsg = query.Query + " PARAMETERS: (" + string.Join(", ", query.Parameters.Select(p => $"{p.Name}: {p.Value}")) + ")";
            sqlmsg = sqlmsg.Trim();       // trimming makes the log messages clearer

            _migrationUtils.LogInformation("Setting {0} Version to {1} with SQL '{2}'", _db.Database, ver, sqlmsg);

            int rowsAffected = _db.DbExecuteNonQuery(query, transaction);
        }

        // Execute queries, logging the SQL and rows affected
        private void Execute(IDbTransaction transaction, int batchNum, string sql)
        {
            sql = sql.Trim();       // trimming makes the log messages clearer

            var qp = new SqlQueryWithParams(sql);

            _migrationUtils.LogSqlBatch(batchNum, sql);

            int rowsAffected = _db.DbExecuteNonQuery(qp, transaction );

            _migrationUtils.LogInformation("Batch {0} exectued successfully on {1}. {2} rows affected.", batchNum, _db.Database, rowsAffected);
        }
    }
}
