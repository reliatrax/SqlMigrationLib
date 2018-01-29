using System;

namespace SqlMigrationLib
{
    public interface ISqlMigrationUtils<TVer>
    {
        TVer[] ListRequiredMigrationScripts( TVer currentDBVersion, TVer requiredVersion);

        string ReadMigrationScript( TVer migrationName );

        SqlQueryWithParams GetDBVersionQuery();

        SqlQueryWithParams SetDBVersionQuery(TVer ver);

        TVer GetInProgressVer(TVer migrationVer);

        void LogSqlBatch(int batchNum, string sql);

        void LogInformation(string message, params object[] args);

        void LogError(Exception e, string message, params object[] args);
    }
}
