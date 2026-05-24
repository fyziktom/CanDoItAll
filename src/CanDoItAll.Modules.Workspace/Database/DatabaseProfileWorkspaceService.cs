using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workspace;

public sealed class DatabaseProfileWorkspaceService(
    IDatabaseProfileService profileService,
    IDatabaseProfileRuntimeAccessor profileAccessor,
    IDatabaseTransferService transferService,
    IDatabaseDriverRegistry driverRegistry,
    IAppDatabaseBootstrapper bootstrapper,
    ISwitchableAppDbContextFactory dbContextFactory,
    IDatabaseSwitchCoordinator switchCoordinator,
    ILogger<DatabaseProfileWorkspaceService> logger)
{
    private static readonly TimeSpan SchemaHealthTimeout = TimeSpan.FromSeconds(5);

    public Task<IReadOnlyList<DatabaseProfileSummary>> ListProfilesAsync(CancellationToken cancellationToken = default)
    {
        return profileService.ListAsync(cancellationToken);
    }

    public Task<DatabaseProfileEditorModel> GetProfileAsync(Guid? id = null, CancellationToken cancellationToken = default)
    {
        return profileService.GetEditorAsync(id, cancellationToken);
    }

    public Task<DatabaseSelectionStateModel> GetCurrentSelectionAsync(CancellationToken cancellationToken = default)
    {
        return profileService.GetCurrentSelectionAsync(cancellationToken);
    }

    public Task<IReadOnlyList<DatabaseTransferSourceSummary>> ListTransferSourcesAsync(
        Guid targetProfileId,
        CancellationToken cancellationToken = default)
    {
        return transferService.ListSourcesAsync(targetProfileId, cancellationToken);
    }

    public Task<IReadOnlyList<DatabaseTransferItemPreview>> PreviewTransferAsync(
        Guid sourceProfileId,
        Guid targetProfileId,
        CancellationToken cancellationToken = default)
    {
        return transferService.PreviewAsync(sourceProfileId, targetProfileId, cancellationToken);
    }

    public Task<DatabaseTransferResult> TransferSettingsAsync(
        DatabaseTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        return transferService.TransferAsync(request, cancellationToken);
    }

    public async Task<DatabaseProfileEditorModel> GetCurrentEditorAsync(CancellationToken cancellationToken = default)
    {
        var selection = await profileService.GetCurrentSelectionAsync(cancellationToken);
        if (!selection.IsRuntimeLocked)
        {
            return await profileService.GetEditorAsync(selection.ActiveProfileId, cancellationToken);
        }

        var profile = profileAccessor.ResolveCurrentProfile();
        return CreateEditor(profile.Profile);
    }

    public Result Validate(DatabaseProfileEditorModel model)
    {
        return profileService.Validate(model);
    }

    public Task<Result<Guid>> SaveProfileAsync(DatabaseProfileEditorModel model, CancellationToken cancellationToken = default)
    {
        return profileService.SaveAsync(model, cancellationToken);
    }

    public Task<Result> DeleteProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return profileService.DeleteAsync(id, cancellationToken);
    }

    public Task<Result<DatabaseSwitchResult>> ActivateProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return switchCoordinator.SwitchAsync(id, cancellationToken);
    }

    public async Task<DatabaseProfileSchemaHealth> GetSchemaHealthAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ResolvedDatabaseProfile profile;
        try
        {
            profile = profileAccessor.ResolveProfile(id);
        }
        catch (Exception ex)
        {
            return CreateSchemaHealth(
                id,
                DatabaseProfileSchemaStatus.Unavailable,
                $"The data source profile could not be resolved: {ex.Message}",
                [],
                canApplySchema: false);
        }

        var canApplySchema = !profile.Profile.Runtime.LockedByRuntimeOverride &&
            profile.Profile.ProviderKind == DatabaseProviderKind.PostgreSql;

        if (profile.Profile.ProviderKind == DatabaseProviderKind.InMemory)
        {
            return CreateSchemaHealth(
                id,
                DatabaseProfileSchemaStatus.Current,
                "In-memory data sources do not require relational migrations.",
                [],
                canApplySchema: false);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(SchemaHealthTimeout);

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextForProfileAsync(profile, timeout.Token);
            if (!await dbContext.Database.CanConnectAsync(timeout.Token))
            {
                return CreateSchemaHealth(
                    id,
                    DatabaseProfileSchemaStatus.Unavailable,
                    "The data source could not be opened for a schema check.",
                    [],
                    canApplySchema);
            }

            if (!dbContext.Database.IsRelational())
            {
                return CreateSchemaHealth(
                    id,
                    DatabaseProfileSchemaStatus.Current,
                    "This data source does not use relational migrations.",
                    [],
                    canApplySchema: false);
            }

            var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(timeout.Token))
                .ToList();
            if (pendingMigrations.Count == 0)
            {
                var schemaIssues = await FindSchemaIssuesAsync(dbContext, timeout.Token);
                if (schemaIssues.Count > 0)
                {
                    return CreateSchemaHealth(
                        id,
                        DatabaseProfileSchemaStatus.NeedsMigration,
                        $"Database schema is missing {schemaIssues.Count} expected table or column item(s). Apply the current schema before activation or transfer.",
                        pendingMigrations,
                        canApplySchema,
                        schemaIssues);
                }

                return CreateSchemaHealth(
                    id,
                    DatabaseProfileSchemaStatus.Current,
                    "Database schema is current.",
                    pendingMigrations,
                    canApplySchema);
            }

            return CreateSchemaHealth(
                id,
                DatabaseProfileSchemaStatus.NeedsMigration,
                $"{pendingMigrations.Count} database migration(s) need to be applied before this data source is used for transfer.",
                pendingMigrations,
                canApplySchema);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CreateSchemaHealth(
                id,
                DatabaseProfileSchemaStatus.Unavailable,
                "The schema check timed out before the data source responded.",
                [],
                canApplySchema);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Schema health check failed for database profile {ProfileId}.", id);
            return CreateSchemaHealth(
                id,
                DatabaseProfileSchemaStatus.Unavailable,
                $"Schema check failed: {ex.Message}",
                [],
                canApplySchema);
        }
    }

    public async Task<Result> ApplySchemaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var profile = profileAccessor.ResolveProfile(id);
            if (profile.Profile.Runtime.LockedByRuntimeOverride)
            {
                return Result.Failure(Error.Failure("Runtime override profiles cannot be migrated from the UI."));
            }

            if (profile.Profile.ProviderKind == DatabaseProviderKind.InMemory)
            {
                return Result.Success();
            }

            var driver = driverRegistry.Resolve(profile.Profile.ProviderKind);
            await driver.CreateEmptyAsync(profile, cancellationToken);
            await bootstrapper.EnsureProfileReadyAsync(profile, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Applying database schema failed for profile {ProfileId}.", id);
            return Result.Failure(Error.Failure($"Applying the database schema failed: {ex.Message}"));
        }
    }

    public async Task<Result> CreateEmptyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var profile = profileAccessor.ResolveProfile(id);
            if (profile.Profile.Runtime.LockedByRuntimeOverride)
            {
                return Result.Failure(Error.Failure("Runtime override profiles cannot be created or bootstrapped from the UI."));
            }

            var driver = driverRegistry.Resolve(profile.Profile.ProviderKind);
            await driver.CreateEmptyAsync(profile, cancellationToken);
            await bootstrapper.EnsureProfileReadyAsync(profile, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Creating an empty database failed for profile {ProfileId}.", id);
            return Result.Failure(Error.Failure($"Creating an empty database failed: {ex.Message}"));
        }
    }

    public async Task<Result> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var profile = profileAccessor.ResolveProfile(id);
            if (profile.Profile.ProviderKind != DatabaseProviderKind.PostgreSql)
            {
                return Result.Failure(Error.Validation("Connection testing is only available for PostgreSQL profiles."));
            }

            if (profile.Profile.Runtime.LockedByRuntimeOverride)
            {
                return Result.Failure(Error.Failure("Runtime override profiles cannot be retested from the UI."));
            }

            await driverRegistry.Resolve(DatabaseProviderKind.PostgreSql)
                .EnsureDatabaseAsync(profile, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Testing the PostgreSQL connection failed for profile {ProfileId}.", id);
            return Result.Failure(Error.Failure($"PostgreSQL connection test failed: {ex.Message}"));
        }
    }

    private static DatabaseProfileEditorModel CreateEditor(DatabaseProfileRecord profile)
    {
        return new DatabaseProfileEditorModel
        {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            ProviderKind = profile.ProviderKind,
            SourceKind = profile.SourceKind,
            InMemoryDatabaseName = profile.InMemory?.DatabaseName,
            WorkspaceRoot = profile.Storage.WorkspaceRoot,
            PostgresHost = profile.PostgreSql?.Host ?? "localhost",
            PostgresPort = profile.PostgreSql?.Port ?? 5432,
            PostgresDatabaseName = profile.PostgreSql?.DatabaseName ?? "candoitall",
            PostgresUsername = profile.PostgreSql?.Username ?? "postgres",
            PostgresPassword = string.Empty,
            PostgresAdminDatabaseName = profile.PostgreSql?.AdminDatabaseName,
            PostgresTrustServerCertificate = profile.PostgreSql?.TrustServerCertificate ?? false,
            IsRuntimeLocked = profile.Runtime.LockedByRuntimeOverride
        };
    }

    private static DatabaseProfileSchemaHealth CreateSchemaHealth(
        Guid profileId,
        DatabaseProfileSchemaStatus status,
        string summary,
        IReadOnlyList<string> pendingMigrations,
        bool canApplySchema,
        IReadOnlyList<string>? schemaIssues = null)
    {
        return new DatabaseProfileSchemaHealth(
            profileId,
            status,
            summary,
            pendingMigrations.Count,
            pendingMigrations,
            schemaIssues ?? [],
            canApplySchema);
    }

    private static async Task<IReadOnlyList<string>> FindSchemaIssuesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var expectedSchema = BuildExpectedSchema(dbContext);
        if (expectedSchema.Count == 0)
        {
            return [];
        }

        var actualSchema = dbContext.Database.IsNpgsql()
            ? await ReadPostgreSqlSchemaAsync(dbContext, expectedSchema.Keys, cancellationToken)
            : [];

        if (actualSchema.Count == 0)
        {
            return expectedSchema.Keys
                .Select(table => $"Missing table {FormatTableName(table)}")
                .Take(12)
                .ToList();
        }

        var issues = new List<string>();
        foreach (var expectedTable in expectedSchema.OrderBy(item => FormatTableName(item.Key), StringComparer.OrdinalIgnoreCase))
        {
            if (!actualSchema.TryGetValue(expectedTable.Key, out var actualColumns))
            {
                issues.Add($"Missing table {FormatTableName(expectedTable.Key)}");
                continue;
            }

            foreach (var expectedColumn in expectedTable.Value.OrderBy(column => column, StringComparer.OrdinalIgnoreCase))
            {
                if (!actualColumns.Contains(expectedColumn))
                {
                    issues.Add($"Missing column {FormatTableName(expectedTable.Key)}.{expectedColumn}");
                }
            }
        }

        return issues.Take(12).ToList();
    }

    private static Dictionary<SchemaTable, HashSet<string>> BuildExpectedSchema(AppDbContext dbContext)
    {
        var expectedSchema = new Dictionary<SchemaTable, HashSet<string>>();
        foreach (var entityType in dbContext.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(tableName))
            {
                continue;
            }

            var table = new SchemaTable(NormalizeSchema(entityType.GetSchema(), dbContext), tableName);
            if (!expectedSchema.TryGetValue(table, out var columns))
            {
                columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                expectedSchema[table] = columns;
            }

            var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName(storeObject);
                if (!string.IsNullOrWhiteSpace(columnName))
                {
                    columns.Add(columnName);
                }
            }
        }

        return expectedSchema;
    }

    private static async Task<Dictionary<SchemaTable, HashSet<string>>> ReadPostgreSqlSchemaAsync(
        AppDbContext dbContext,
        IEnumerable<SchemaTable> expectedTables,
        CancellationToken cancellationToken)
    {
        var expectedTableSet = expectedTables.ToHashSet();
        var schema = new Dictionary<SchemaTable, HashSet<string>>();
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
                SELECT table_schema, table_name, column_name
                FROM information_schema.columns
                WHERE table_schema NOT IN ('pg_catalog', 'information_schema');
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(0) || reader.IsDBNull(1) || reader.IsDBNull(2))
                {
                    continue;
                }

                var table = new SchemaTable(reader.GetString(0), reader.GetString(1));
                if (!expectedTableSet.Contains(table))
                {
                    continue;
                }

                if (!schema.TryGetValue(table, out var columns))
                {
                    columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    schema[table] = columns;
                }

                columns.Add(reader.GetString(2));
            }

            return schema;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string NormalizeSchema(string? schema, AppDbContext dbContext)
    {
        if (!string.IsNullOrWhiteSpace(schema))
        {
            return schema;
        }

        return dbContext.Database.IsNpgsql() ? "public" : string.Empty;
    }

    private static string FormatTableName(SchemaTable table)
    {
        return string.IsNullOrWhiteSpace(table.Schema)
            ? table.Name
            : $"{table.Schema}.{table.Name}";
    }

    private sealed record SchemaTable(string Schema, string Name);
}
