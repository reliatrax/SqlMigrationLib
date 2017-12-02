using System;

namespace SqlMigrationLib
{
    public interface ISqlMigrationUtils<TVer>
    {
        TVer[] ListAvailableMigrationScripts( TVer currentDBVersion, TVer requiredVersion);

        string ReadMigrationScript( TVer migrationName );

        int CompareVersions(TVer a, TVer b);

        SqlQueryWithParams GetDBVersionQuery();

        SqlQueryWithParams SetDBVersionQuery(TVer ver);

        void LogInformation(string message, params object[] args);

        void LogError(Exception e);
    }
}
