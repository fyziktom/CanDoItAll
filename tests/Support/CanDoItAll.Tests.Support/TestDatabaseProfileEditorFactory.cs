using CanDoItAll.Infrastructure.ControlPlane;
using Npgsql;

namespace CanDoItAll.Tests.Support;

public static class TestDatabaseProfileEditorFactory
{
    public static DatabaseProfileEditorModel CreatePostgreSqlEditor(
        TestDatabaseProfile profile,
        string displayName)
    {
        if (profile.Provider != TestDatabaseProviderKind.PostgreSql)
        {
            throw new InvalidOperationException($"Profile '{profile.ProfileKey}' is not PostgreSQL-backed.");
        }

        var builder = new NpgsqlConnectionStringBuilder(profile.ConnectionString);
        return new DatabaseProfileEditorModel
        {
            DisplayName = displayName,
            ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection,
            PostgresHost = builder.Host ?? "127.0.0.1",
            PostgresPort = builder.Port,
            PostgresDatabaseName = builder.Database ?? "candoitall",
            PostgresUsername = builder.Username ?? "postgres",
            PostgresPassword = builder.Password ?? string.Empty,
            PostgresAdminDatabaseName = builder.Database,
            WorkspaceRoot = profile.WorkspaceRootPath
        };
    }
}
