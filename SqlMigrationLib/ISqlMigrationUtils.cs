using System;

namespace SqlMigrationLib
{
    public interface ISqlMigrationUtils<TVer>
    {
        TVer[] ListRequiredMigrationScripts( TVer currentDBVersion, TVer requiredVersion);

        string ReadMigrationScript( TVer migrationName );

        SqlQueryWithParams GetDBVersionQuery();

        SqlQueryWithParams SetDBVersionQuery(TVer ver);

        void LogSqlBatch(string sql, int rowsAffected);

        void LogInformation(string message, params object[] args);

        void LogError(Exception e, string message, params object[] args);
    }
}
