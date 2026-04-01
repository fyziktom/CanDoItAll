using System.Data.Common;
using System.Reflection;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Web.Infrastructure;

internal static class LegacySqliteMigrationBootstrap
{
    private const string HistoryTableName = "__EFMigrationsHistory";

    public static async Task PrepareAsync(
        AppDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);

        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        if (!await HasLegacySchemaWithoutHistoryAsync(dbContext, cancellationToken))
        {
            return;
        }

        var baselineMigrationId = dbContext.Database.GetMigrations().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(baselineMigrationId))
        {
            throw new InvalidOperationException("SQLite legacy upgrade cannot run because no SQLite baseline migration was found.");
        }

        logger.LogInformation(
            "Reconciling legacy SQLite schema and baselining migration history with {MigrationId}.",
            baselineMigrationId);

        await WorkspaceSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await ProjectsSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await PromptFactorySchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await ProjectStructureAgentSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        await EnsureHistoryTableAsync(dbContext, cancellationToken);
        await SeedHistoryRowAsync(dbContext, baselineMigrationId, cancellationToken);
    }

    private static async Task<bool> HasLegacySchemaWithoutHistoryAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var tableNames = await ReadTableNamesAsync(dbContext, cancellationToken);
        if (tableNames.Contains(HistoryTableName))
        {
            return false;
        }

        return tableNames.Any(name => !name.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<HashSet<string>> ReadTableNamesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT "name"
                FROM "sqlite_master"
                WHERE "type" = 'table';
                """;

            var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    tableNames.Add(reader.GetString(0));
                }
            }

            return tableNames;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task EnsureHistoryTableAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """,
            cancellationToken);
    }

    private static async Task SeedHistoryRowAsync(
        AppDbContext dbContext,
        string migrationId,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ($migrationId, $productVersion);
                """;

            AddParameter(command, "$migrationId", migrationId);
            AddParameter(command, "$productVersion", ResolveEfProductVersion());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string ResolveEfProductVersion()
    {
        var informationalVersion = typeof(DbContext).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return string.IsNullOrWhiteSpace(informationalVersion)
            ? typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "10.0.0"
            : informationalVersion.Split('+', 2)[0];
    }
}
