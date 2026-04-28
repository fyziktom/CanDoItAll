using System.Data.Common;
using System.Reflection;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Composition;

public static class CanDoItAllDatabaseMigrationBootstrap
{
    private const string HistoryTableName = "__EFMigrationsHistory";
    private const string MigrationLockTableName = "__EFMigrationsLock";
    private static readonly TimeSpan StaleMigrationLockThreshold = TimeSpan.FromMinutes(2);

    public static async Task PrepareLegacySqliteAsync(
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
            "Reconciling legacy SQLite schema and baselining migration history from {MigrationId}.",
            baselineMigrationId);

        await WorkspaceSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await ProjectsSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await PromptFactorySchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await ProjectStructureAgentSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var schemaSnapshot = await ReadSchemaSnapshotAsync(dbContext, cancellationToken);
        var baselineMigrationIds = ResolveBaselineMigrationIds(dbContext.Database.GetMigrations(), schemaSnapshot);

        await EnsureHistoryTableAsync(dbContext, cancellationToken);

        foreach (var migrationId in baselineMigrationIds)
        {
            await SeedHistoryRowAsync(dbContext, migrationId, cancellationToken);
        }
    }

    public static async Task ReleaseStaleSqliteMigrationLockAsync(
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

        var tableNames = await ReadTableNamesAsync(dbContext, cancellationToken);
        if (!tableNames.Contains(MigrationLockTableName))
        {
            return;
        }

        var staleLock = await ReadMigrationLockAsync(dbContext, cancellationToken);
        if (staleLock is null)
        {
            return;
        }

        var utcNow = DateTimeOffset.UtcNow;
        if (utcNow - staleLock.TimestampUtc < StaleMigrationLockThreshold)
        {
            return;
        }

        logger.LogWarning(
            "Removing stale SQLite EF migration lock {LockId} from {LockedAtUtc:u} before migration execution.",
            staleLock.Id,
            staleLock.TimestampUtc);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM "__EFMigrationsLock"
            WHERE "Id" = {0};
            """,
            [staleLock.Id],
            cancellationToken);
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

    private static async Task<SqliteSchemaSnapshot> ReadSchemaSnapshotAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var tableNames = await ReadTableNamesAsync(dbContext, cancellationToken);
        var columnsByTable = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tableName in tableNames.Where(name => !name.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase)))
        {
            columnsByTable[tableName] = await ReadColumnNamesAsync(dbContext, tableName, cancellationToken);
        }

        return new SqliteSchemaSnapshot(tableNames, columnsByTable);
    }

    private static async Task<HashSet<string>> ReadColumnNamesAsync(
        AppDbContext dbContext,
        string tableName,
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
            command.CommandText = $"""PRAGMA table_info("{tableName.Replace("\"", "\"\"", StringComparison.Ordinal)}");""";

            var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(1))
                {
                    columnNames.Add(reader.GetString(1));
                }
            }

            return columnNames;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<SqliteMigrationLockRow?> ReadMigrationLockAsync(
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
                SELECT "Id", "Timestamp"
                FROM "__EFMigrationsLock"
                ORDER BY "Id"
                LIMIT 1;
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0) || reader.IsDBNull(1))
            {
                return null;
            }

            var id = reader.GetInt64(0);
            var timestampText = reader.GetString(1);
            if (!DateTimeOffset.TryParse(timestampText, out var timestampUtc))
            {
                return null;
            }

            return new SqliteMigrationLockRow(id, timestampUtc);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static IReadOnlyList<string> ResolveBaselineMigrationIds(
        IEnumerable<string> migrationIds,
        SqliteSchemaSnapshot schemaSnapshot)
    {
        var orderedMigrationIds = migrationIds.ToList();
        if (HasCurrentManagedSqliteSchema(schemaSnapshot))
        {
            return orderedMigrationIds;
        }

        var baselineMigrationIds = new List<string>();

        foreach (var migrationId in orderedMigrationIds)
        {
            if (!IsMigrationRepresentedBySchema(migrationId, schemaSnapshot))
            {
                break;
            }

            baselineMigrationIds.Add(migrationId);
        }

        return baselineMigrationIds;
    }

    private static bool IsMigrationRepresentedBySchema(string migrationId, SqliteSchemaSnapshot schemaSnapshot)
    {
        return migrationId switch
        {
            _ when migrationId.Contains("InitialCreate", StringComparison.Ordinal) => true,
            _ when migrationId.Contains("AddStorageFoundation", StringComparison.Ordinal) =>
                HasTable(schemaSnapshot, "Storage_Catalog")
                && HasTable(schemaSnapshot, "Storage_RoutingRules"),
            _ when migrationId.Contains("AddProjectObjectDurationSeconds", StringComparison.Ordinal) =>
                HasColumn(schemaSnapshot, "Workbench_ProjectObjects", "DurationSeconds"),
            _ when migrationId.Contains("AddCrmHrFoundation", StringComparison.Ordinal) =>
                HasTable(schemaSnapshot, "CrmHr_Parties")
                && HasTable(schemaSnapshot, "CrmHr_LookupOptions"),
            _ when migrationId.Contains("AddCrmHrAccountsAndInteractions", StringComparison.Ordinal) =>
                HasTable(schemaSnapshot, "CrmHr_AccountProfiles")
                && HasTable(schemaSnapshot, "CrmHr_AccountStakeholders"),
            _ when migrationId.Contains("AddCrmHrCrossModuleResponsibleParties", StringComparison.Ordinal) =>
                HasColumn(schemaSnapshot, "Validation_Runs", "ResponsiblePartyId")
                && HasColumn(schemaSnapshot, "TestLab_TestPlans", "ResponsiblePartyId")
                && HasColumn(schemaSnapshot, "Resources_ProjectResources", "OwnerPartyId")
                && HasColumn(schemaSnapshot, "Resources_ProjectResources", "MaintainerPartyId"),
            _ when migrationId.Contains("AddWorkbenchProjectionLayouts", StringComparison.Ordinal) =>
                HasTable(schemaSnapshot, "Workbench_ProjectProjectionLayouts"),
            _ when migrationId.Contains("AddWorkbenchProjectionVisibility", StringComparison.Ordinal) =>
                HasColumn(schemaSnapshot, "Workbench_ProjectProjectionLayouts", "IsHidden"),
            _ when migrationId.Contains("AddProjectNodeBindings", StringComparison.Ordinal) =>
                HasTable(schemaSnapshot, "Workbench_ProjectNodeBindings")
                && HasTable(schemaSnapshot, "Workbench_ProjectNodeReferences"),
            _ when migrationId.Contains("AddProjectNodeLifecycleEvents", StringComparison.Ordinal) =>
                HasTable(schemaSnapshot, "Workbench_ProjectNodeLifecycleEvents"),
            _ when migrationId.Contains("AddConnectorPluginPlatformAndCrossModuleMutations", StringComparison.Ordinal) =>
                HasColumn(schemaSnapshot, "Workspace_ProviderProfiles", "ConnectorPluginKey")
                && HasColumn(schemaSnapshot, "Workspace_ProviderProfiles", "ConfigSchemaVersion")
                && HasColumn(schemaSnapshot, "Resources_ProjectResources", "ConnectorPluginKey")
                && HasColumn(schemaSnapshot, "Resources_ProjectResources", "ConfigSchemaVersion")
                && HasTable(schemaSnapshot, "Workbench_ProjectCrossModuleMutations"),
            _ when migrationId.Contains("AddProjectObjectMarkersJson", StringComparison.Ordinal) =>
                HasColumn(schemaSnapshot, "Workbench_ProjectObjects", "MarkersJson"),
            _ when migrationId.Contains("AddCrossModuleMutationDurabilityFields", StringComparison.Ordinal) =>
                HasColumn(schemaSnapshot, "Workbench_ProjectCrossModuleMutations", "ApprovalState")
                && HasColumn(schemaSnapshot, "Workbench_ProjectCrossModuleMutations", "AttemptCount")
                && HasColumn(schemaSnapshot, "Workbench_ProjectCrossModuleMutations", "LastAttemptAtUtc")
                && HasColumn(schemaSnapshot, "Workbench_ProjectCrossModuleMutations", "CompletedAtUtc"),
            _ when migrationId.Contains("AddConnectorCommandOutboxBoundary", StringComparison.Ordinal) =>
                HasTable(schemaSnapshot, "Workspace_ConnectorCommands")
                && HasTable(schemaSnapshot, "Workspace_ConnectorCommandAudits")
                && !HasColumn(schemaSnapshot, "Workbench_ProjectObjects", "Route")
                && !HasColumn(schemaSnapshot, "Workbench_ProjectObjects", "ExternalArtifactKind")
                && !HasColumn(schemaSnapshot, "Workbench_ProjectObjects", "ExternalArtifactId")
                && !HasColumn(schemaSnapshot, "Workbench_ProjectObjects", "MediaRelativePath")
                && !HasColumn(schemaSnapshot, "Workbench_ProjectObjects", "MediaContentType")
                && !HasColumn(schemaSnapshot, "Workbench_ProjectObjects", "MediaOriginalFileName")
                && !HasColumn(schemaSnapshot, "Workbench_ProjectObjects", "StorageObjectReferenceJson"),
            _ => false
        };
    }

    private static bool HasCurrentManagedSqliteSchema(SqliteSchemaSnapshot schemaSnapshot)
    {
        return HasTable(schemaSnapshot, "Automation_DeadLetters")
               && HasTable(schemaSnapshot, "Processes_Definitions")
               && HasTable(schemaSnapshot, "Processes_LaunchPlans")
               && HasTable(schemaSnapshot, "Collaboration_Threads")
               && HasTable(schemaSnapshot, "CrmHr_AiResourceBindings")
               && HasColumn(schemaSnapshot, "Workbench_ProjectProjectionLayouts", "IsHidden");
    }

    private static bool HasTable(SqliteSchemaSnapshot schemaSnapshot, string tableName)
    {
        return schemaSnapshot.TableNames.Contains(tableName);
    }

    private static bool HasColumn(SqliteSchemaSnapshot schemaSnapshot, string tableName, string columnName)
    {
        return schemaSnapshot.ColumnsByTable.TryGetValue(tableName, out var columnNames)
            && columnNames.Contains(columnName);
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

    private sealed record SqliteSchemaSnapshot(
        HashSet<string> TableNames,
        Dictionary<string, HashSet<string>> ColumnsByTable);

    private sealed record SqliteMigrationLockRow(
        long Id,
        DateTimeOffset TimestampUtc);
}
