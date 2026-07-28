using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Composition.Memory;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.Composition;

public static class RuntimeHostServiceCollectionExtensions
{
    private const string OpenAiApiKeyConfigurationKey = "OPENAI_API_KEY";

    public static IServiceCollection AddCanDoItAllRuntimeModules(
        this IServiceCollection services,
        IConfiguration configuration,
        string? contentRootPath = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        PromoteConfiguredOpenAiCredential(configuration);

        services.AddSecurityModule(configuration);
        services.AddWorkspaceModule();
        services.AddProjectsModule();
        services.AddCanDoItAllMemory(configuration);
        services.AddWorkbenchModule();
        services.AddResourcesModule();
        services.AddPromptsModule();
        services.AddPluginsModule(configuration, contentRootPath);
        services.AddCanDoItAllGmailPlugin();
        services.AddCanDoItAllOffice365Plugin();
        services.AddProcessesModule(configuration);
        services.AddTestLabModule();
        services.AddAgentFrameworkModule(configuration);
        services.AddSchedulerPlannerModule(configuration);
        services.AddCollaborationModule();
        services.AddCrmHrModule();
        services.AddSchedulerPlannerWorkflowInputOptionProviders();
        services.AddCanDoItAllFileToolsIntegration();
        return services;
    }

    private static void PromoteConfiguredOpenAiCredential(
        IConfiguration configuration)
    {
        var configuredOpenAiApiKey = configuration[OpenAiApiKeyConfigurationKey];
        if (string.IsNullOrWhiteSpace(configuredOpenAiApiKey))
        {
            return;
        }

        AgentProviderEnvironmentCredential.PromoteProcessValue(
            OpenAiApiKeyConfigurationKey,
            configuredOpenAiApiKey);
    }

    public static IServiceCollection AddCanDoItAllRuntimeDatabaseSwitching(this IServiceCollection services)
    {
        services.AddSingleton<IAppDatabaseBootstrapper, AppDatabaseBootstrapper>();
        services.AddSingleton<IDatabaseSwitchCoordinator, DatabaseSwitchCoordinator>();
        return services;
    }
}

public sealed class AppDatabaseBootstrapper(
    IDatabaseProfileRuntimeAccessor profileAccessor,
    IProfileAppDbContextFactory dbContextFactory,
    ISecretVault secretVault,
    IEnumerable<IProviderRuntimeProfileSnapshotInitializer>
        providerRuntimeProfileSnapshotInitializers,
    ILogger<AppDatabaseBootstrapper> logger) : IAppDatabaseBootstrapper
{
    private static readonly Guid ManagedDeliveryUnitPartyId = Guid.Parse("10BE49B1-EF4D-4A58-B9EA-B3F7D40F31A1");
    private static readonly Guid ManagedProductOwnerPartyId = Guid.Parse("A6BBAD2B-9D18-40EA-95B5-6D73C20C3078");
    private static readonly Guid ManagedDeliveryManagerPartyId = Guid.Parse("4B4718D5-4F86-4A6A-9BE7-3ACCA7E0F2AB");
    private static readonly Guid ManagedDeliveryUnitRoleId = Guid.Parse("1A8A7BB6-10B5-4D18-A91F-00F25E045DBF");
    private static readonly Guid ManagedProductOwnerRoleId = Guid.Parse("DBF3B8E6-77D2-49D5-924A-74CA8FFFBFD3");
    private static readonly Guid ManagedDeliveryManagerRoleId = Guid.Parse("2D9DF6AC-8B49-43EA-960E-8B912A758296");
    private static readonly Guid ManagedProductOwnerProfileId = Guid.Parse("61C29FAE-C560-4C2D-993E-BE842FD635FB");
    private static readonly Guid ManagedDeliveryManagerProfileId = Guid.Parse("E0EBEC09-C37B-4F42-9FA4-1B2DDAC20572");
    private static readonly Guid RuntimeBootstrapOpenAiProviderId = Guid.Parse("C1C103DB-707E-3F52-8809-8D804FC171D1");
    private static readonly Guid RuntimeBootstrapOpenAiChatCompletionsProviderId = Guid.Parse("036B360A-E3F4-8350-97CA-F88DE60BA2BB");
    private static readonly Guid RuntimeBootstrapOpenAiImageProviderId = Guid.Parse("8958FA61-4BD6-1451-8123-4E4E4FEA2E26");
    private static readonly Guid RuntimeBootstrapComfyUiProviderId = Guid.Parse("509EAF62-4A4E-1C50-856F-8836328A519E");
    private static readonly Guid RuntimeBootstrapLocalOllamaProviderId = Guid.Parse("BD2BFFBB-23D5-D152-82F6-E1D37908B169");
    private const string RuntimeBootstrapOpenAiProviderName = ManagedSeedProviderFallbacks.OpenAiDefaultProviderName;
    private const string RuntimeBootstrapOpenAiChatCompletionsProviderName = ManagedSeedProviderFallbacks.OpenAiChatCompletionsProviderName;
    private const string RuntimeBootstrapOpenAiImageProviderName = "OpenAI image generation";
    private const string RuntimeBootstrapOpenAiBaseUrl = "https://api.openai.com/v1";
    private const string RuntimeBootstrapOpenAiApiKeyEnvironmentVariable = "OPENAI_API_KEY";
    private const string RuntimeBootstrapOpenAiModel = ManagedSeedProviderFallbacks.OpenAiDefaultModel;
    private const string RuntimeBootstrapOpenAiImageModel = "gpt-image-1-mini";
    private const string RuntimeBootstrapLocalOllamaProviderName = "Local Ollama";
    private const string RuntimeBootstrapLocalOllamaBaseUrl = "http://127.0.0.1:11434";
    private const string RuntimeBootstrapLocalOllamaModel = "llama3.1";
    private const string RuntimeBootstrapProviderSchemaVersion = "1.0";
    private const int RuntimeBootstrapOpenAiTimeoutSeconds = 600;
    private const int RuntimeBootstrapLocalOllamaTimeoutSeconds = 45;
    private static readonly Guid DefaultOpenAiApiKeySecretId = Guid.Parse("86F781F1-1E76-4B45-9F1A-42B8CF13D8C7");
    private const string DefaultOpenAiApiKeySecretName = "OpenAI API key";
    private const string InitialPostgreSqlBaselineMigrationId = "20260528182412_InitialPostgreSqlBaseline";
    private static readonly string[] BaselineSentinelTables =
    [
        "Projects_Projects",
        "Workspace_ProviderProfiles"
    ];
    private static readonly string[] RetiredLegacyPromptTables =
    [
        "Factory_PromptBlocks",
        "Factory_PromptBlueprints",
        "Factory_PromptBuildSessions",
        "Factory_PromptFlowTemplates",
        "Factory_PromptRunNodes",
        "Factory_PromptRuns"
    ];

    public async Task EnsureCurrentProfileReadyAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureProfileReadyAsync(
            profileAccessor.ResolveCurrentProfile(),
            cancellationToken);
        foreach (var initializer in
                 providerRuntimeProfileSnapshotInitializers)
        {
            await initializer.InitializeAsync(cancellationToken);
        }
    }

    public async Task EnsureProfileReadyAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Ensuring runtime database profile {ProfileId} ({DisplayName}) is ready. Provider={ProviderKind}, Source={SourceKind}.",
            profile.Profile.Id,
            profile.Profile.DisplayName,
            profile.Profile.ProviderKind,
            profile.Profile.SourceKind);

        await using var dbContext = await dbContextFactory.CreateDbContextForProfileAsync(profile, cancellationToken);
        if (!dbContext.Database.IsRelational())
        {
            logger.LogInformation(
                "Ensuring non-relational database profile {ProfileId} is created.",
                profile.Profile.Id);
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            logger.LogInformation(
                "Ensuring agent provider bootstrap for non-relational profile {ProfileId}.",
                profile.Profile.Id);
            await EnsureAgentProviderBootstrapAsync(
                profile,
                dbContext,
                cancellationToken);
            logger.LogInformation(
                "Non-relational database profile {ProfileId} is ready.",
                profile.Profile.Id);
            return;
        }

        logger.LogInformation(
            "Applying EF migrations for profile {ProfileId}.",
            profile.Profile.Id);
        await AdoptExistingPostgreSqlSchemaIfNeededAsync(profile, dbContext, cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation(
            "Ensuring CRM/HR schema for profile {ProfileId}.",
            profile.Profile.Id);
        await CrmHrSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        logger.LogInformation(
            "Ensuring plugin runtime schema for profile {ProfileId}.",
            profile.Profile.Id);
        await PluginSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        logger.LogInformation(
            "Ensuring scheduler planner schema for profile {ProfileId}.",
            profile.Profile.Id);
        await SchedulerPlannerSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        logger.LogInformation(
            "Ensuring agent provider bootstrap for profile {ProfileId}.",
            profile.Profile.Id);
        await EnsureAgentProviderBootstrapAsync(profile, dbContext, cancellationToken);
        logger.LogInformation(
            "Runtime database profile {ProfileId} is ready.",
            profile.Profile.Id);
    }

    private async Task AdoptExistingPostgreSqlSchemaIfNeededAsync(
        ResolvedDatabaseProfile profile,
        AppDbContext dbContext,
        CancellationToken cancellationToken) {
        if (profile.Profile.ProviderKind != DatabaseProviderKind.PostgreSql) {
            return;
        }

        var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var knownMigrationIds = dbContext.Database.GetMigrations().ToArray();

        if (appliedMigrations.Contains(InitialPostgreSqlBaselineMigrationId)) {
            return;
        }

        var existingSentinelTables = new List<string>();
        foreach (var tableName in BaselineSentinelTables) {
            if (await PostgreSqlTableExistsAsync(dbContext, tableName, cancellationToken)) {
                existingSentinelTables.Add(tableName);
            }
        }

        if (existingSentinelTables.Count == 0) {
            return;
        }

        if (existingSentinelTables.Count != BaselineSentinelTables.Length) {
            throw new InvalidOperationException(
                $"PostgreSQL profile '{profile.Profile.DisplayName}' has a partial CanDoItAll schema without the current EF baseline history. Existing sentinel tables: {string.Join(", ", existingSentinelTables)}. Refusing to adopt the merged baseline automatically.");
        }

        var missingRequirements = await FindMissingPostgreSqlMergedBaselineRequirementsAsync(dbContext, cancellationToken);
        if (missingRequirements.Count > 0) {
            throw new InvalidOperationException(
                $"PostgreSQL profile '{profile.Profile.DisplayName}' has CanDoItAll tables but does not match the current PostgreSQL migration chain. Missing schema requirements: {string.Join(", ", missingRequirements)}. Refusing to record migrations {string.Join(", ", knownMigrationIds)} automatically.");
        }

        logger.LogWarning(
            "PostgreSQL profile {ProfileId} has an existing current CanDoItAll schema but no recorded EF migration history. Recording current migration chain {MigrationIds} before applying pending migrations.",
            profile.Profile.Id,
            string.Join(", ", knownMigrationIds));

        await EnsurePostgreSqlMigrationHistoryTableAsync(dbContext, cancellationToken);
        foreach (var migrationId in knownMigrationIds) {
            if (!appliedMigrations.Contains(migrationId)) {
                await MarkPostgreSqlMigrationAppliedAsync(dbContext, migrationId, cancellationToken);
            }
        }
    }

    private static async Task<bool> PostgreSqlTableExistsAsync(
        AppDbContext dbContext,
        string tableName,
        CancellationToken cancellationToken) {
        var result = await ExecutePostgreSqlScalarAsync(
            dbContext,
            """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = current_schema()
                  AND table_name = @tableName
            );
            """,
            cancellationToken,
            ("@tableName", tableName));

        return result is true;
    }

    private static async Task<List<string>> FindMissingPostgreSqlMergedBaselineRequirementsAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken) {
        var missingRequirements = new List<string>();

        var expectedSchema = BuildExpectedPostgreSqlSchema(dbContext);
        var actualSchema = await ReadPostgreSqlSchemaAsync(dbContext, cancellationToken);
        foreach (var (tableName, expectedColumns) in expectedSchema) {
            if (!actualSchema.TryGetValue(tableName, out var actualColumns)) {
                missingRequirements.Add($"table {tableName}");
                continue;
            }

            foreach (var columnName in expectedColumns.Where(column => !actualColumns.Contains(column))) {
                missingRequirements.Add($"column {tableName}.{columnName}");
            }
        }

        foreach (var retiredTable in RetiredLegacyPromptTables.Where(actualSchema.ContainsKey))
        {
            missingRequirements.Add($"retired table {retiredTable}");
        }

        var expectedIndexNames = dbContext.Model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetIndexes())
            .Select(index => index.GetDatabaseName())
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(indexName => indexName, StringComparer.Ordinal);
        var actualIndexNames = await ReadPostgreSqlIndexNamesAsync(dbContext, cancellationToken);
        foreach (var indexName in expectedIndexNames.Where(indexName => !actualIndexNames.Contains(indexName))) {
            missingRequirements.Add($"index {indexName}");
        }

        if (await HasIncompletePromptGallerySearchBackfillAsync(dbContext, cancellationToken))
        {
            missingRequirements.Add("Prompt Gallery normalized search backfill");
        }

        return missingRequirements;
    }

    private static async Task<bool> HasIncompletePromptGallerySearchBackfillAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var result = await ExecutePostgreSqlScalarAsync(
            dbContext,
            """
            SELECT EXISTS (
                SELECT 1
                FROM "Prompts_PromptTags"
                WHERE BTRIM("NameKey") = ''
            ) OR EXISTS (
                SELECT 1
                FROM "Prompts_PromptArtifacts"
                WHERE BTRIM("SearchText") = ''
            );
            """,
            cancellationToken);
        return result is true;
    }

    private static Dictionary<string, HashSet<string>> BuildExpectedPostgreSqlSchema(AppDbContext dbContext)
    {
        var expectedSchema = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var entityType in dbContext.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(tableName))
            {
                continue;
            }

            if (!expectedSchema.TryGetValue(tableName, out var columns))
            {
                columns = new HashSet<string>(StringComparer.Ordinal);
                expectedSchema[tableName] = columns;
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

    private static async Task<Dictionary<string, HashSet<string>>> ReadPostgreSqlSchemaAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var schema = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        await ExecutePostgreSqlReaderAsync(
            dbContext,
            """
            SELECT table_name, column_name
            FROM information_schema.columns
            WHERE table_schema = current_schema();
            """,
            async reader =>
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var tableName = reader.GetString(0);
                    if (!schema.TryGetValue(tableName, out var columns))
                    {
                        columns = new HashSet<string>(StringComparer.Ordinal);
                        schema[tableName] = columns;
                    }

                    columns.Add(reader.GetString(1));
                }
            },
            cancellationToken);
        return schema;
    }

    private static async Task<HashSet<string>> ReadPostgreSqlIndexNamesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var indexNames = new HashSet<string>(StringComparer.Ordinal);
        await ExecutePostgreSqlReaderAsync(
            dbContext,
            """
            SELECT indexname
            FROM pg_indexes
            WHERE schemaname = current_schema();
            """,
            async reader =>
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    indexNames.Add(reader.GetString(0));
                }
            },
            cancellationToken);
        return indexNames;
    }

    private static Task EnsurePostgreSqlMigrationHistoryTableAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
        => ExecutePostgreSqlNonQueryAsync(
            dbContext,
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            );
            """,
            cancellationToken);

    private static Task MarkPostgreSqlMigrationAppliedAsync(
        AppDbContext dbContext,
        string migrationId,
        CancellationToken cancellationToken)
        => ExecutePostgreSqlNonQueryAsync(
            dbContext,
            """
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES (@migrationId, @productVersion)
            ON CONFLICT ("MigrationId") DO NOTHING;
            """,
            cancellationToken,
            ("@migrationId", migrationId),
            ("@productVersion", ResolveEfCoreProductVersion()));

    private static string ResolveEfCoreProductVersion() {
        var informationalVersion = typeof(DbContext).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion)) {
            return informationalVersion.Split('+', 2)[0];
        }

        return typeof(DbContext).Assembly.GetName().Version?.ToString(3) ?? "10.0.0";
    }

    private static async Task<object?> ExecutePostgreSqlScalarAsync(
        AppDbContext dbContext,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters) {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) {
            await connection.OpenAsync(cancellationToken);
        }

        try {
            await using var command = CreateCommand(connection, commandText, parameters);
            return await command.ExecuteScalarAsync(cancellationToken);
        }
        finally {
            if (shouldClose) {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task ExecutePostgreSqlReaderAsync(
        AppDbContext dbContext,
        string commandText,
        Func<DbDataReader, Task> readAsync,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = CreateCommand(connection, commandText);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await readAsync(reader);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task ExecutePostgreSqlNonQueryAsync(
        AppDbContext dbContext,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters) {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) {
            await connection.OpenAsync(cancellationToken);
        }

        try {
            await using var command = CreateCommand(connection, commandText, parameters);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally {
            if (shouldClose) {
                await connection.CloseAsync();
            }
        }
    }

    private static DbCommand CreateCommand(
        DbConnection connection,
        string commandText,
        params (string Name, object? Value)[] parameters) {
        var command = connection.CreateCommand();
        command.CommandText = commandText;
        foreach (var (name, value) in parameters) {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private async Task EnsureAgentProviderBootstrapAsync(
        ResolvedDatabaseProfile profile,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var changed = false;
        var openAiSecretId = await EnsureDefaultOpenAiSecretAsync(dbContext, cancellationToken);
        var openAiProvider = await dbContext.Set<CanDoItAll.Modules.Workspace.ProviderProfile>()
            .SingleOrDefaultAsync(item => item.Id == RuntimeBootstrapOpenAiProviderId, cancellationToken)
            ?? await dbContext.Set<CanDoItAll.Modules.Workspace.ProviderProfile>()
                .Where(item => item.Name == RuntimeBootstrapOpenAiProviderName)
                .OrderBy(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);
        if (openAiProvider is null)
        {
            openAiProvider = new CanDoItAll.Modules.Workspace.ProviderProfile
            {
                Id = RuntimeBootstrapOpenAiProviderId,
                Name = RuntimeBootstrapOpenAiProviderName,
                ProviderKind = CanDoItAll.Modules.Workspace.ProviderKind.OpenAi,
                ConnectorPluginKey = OpenAiProviderAdapter.PluginKey,
                ConfigSchemaVersion = RuntimeBootstrapProviderSchemaVersion,
                BaseUrl = RuntimeBootstrapOpenAiBaseUrl,
                ApiKeySecretId = openAiSecretId,
                DefaultModel = RuntimeBootstrapOpenAiModel,
                TimeoutSeconds = RuntimeBootstrapOpenAiTimeoutSeconds,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsToolCalling = true,
                SupportsStructuredOutput = true,
                SupportsVision = false,
                LastHealthStatus = "OpenAI active",
                LastHealthCheckAtUtc = null,
                ExtraSettingsJson = JsonSerializer.Serialize(new
                {
                    history = "service-managed",
                    reasoningEffort = ManagedSeedProviderFallbacks.DefaultReasoningEffort,
                    modelParameters = new
                    {
                        reasoningEffort = ManagedSeedProviderFallbacks.DefaultReasoningEffort
                    },
                    apiKeyEnvironmentVariable = RuntimeBootstrapOpenAiApiKeyEnvironmentVariable,
                    connectorPluginKey = OpenAiProviderAdapter.PluginKey,
                    configSchemaVersion = RuntimeBootstrapProviderSchemaVersion,
                    secretRecordId = openAiSecretId?.ToString("D"),
                    providerTransport = nameof(ProviderTransportKind.Responses),
                    timeoutSeconds = RuntimeBootstrapOpenAiTimeoutSeconds
                })
            };
            dbContext.Set<CanDoItAll.Modules.Workspace.ProviderProfile>().Add(openAiProvider);
            changed = true;
        }
        else
        {
            changed |= UpdateRuntimeBootstrapOpenAiProvider(openAiProvider);
        }

        if (openAiSecretId.HasValue && openAiProvider.ApiKeySecretId != openAiSecretId.Value)
        {
            openAiProvider.ApiKeySecretId = openAiSecretId.Value;
            changed = true;
        }

        changed |= UpdateRuntimeBootstrapOpenAiProviderConfigurationJson(openAiProvider);

        var openAiChatCompletionsProvider = await dbContext.Set<CanDoItAll.Modules.Workspace.ProviderProfile>()
            .SingleOrDefaultAsync(item => item.Id == RuntimeBootstrapOpenAiChatCompletionsProviderId, cancellationToken)
            ?? await dbContext.Set<CanDoItAll.Modules.Workspace.ProviderProfile>()
                .Where(item => item.Name == RuntimeBootstrapOpenAiChatCompletionsProviderName)
                .OrderBy(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);
        if (openAiChatCompletionsProvider is null)
        {
            openAiChatCompletionsProvider = new CanDoItAll.Modules.Workspace.ProviderProfile
            {
                Id = RuntimeBootstrapOpenAiChatCompletionsProviderId,
                Name = RuntimeBootstrapOpenAiChatCompletionsProviderName,
                ProviderKind = CanDoItAll.Modules.Workspace.ProviderKind.OpenAi,
                ConnectorPluginKey = OpenAiProviderAdapter.PluginKey,
                ConfigSchemaVersion = RuntimeBootstrapProviderSchemaVersion,
                BaseUrl = RuntimeBootstrapOpenAiBaseUrl,
                ApiKeySecretId = openAiSecretId,
                DefaultModel = RuntimeBootstrapOpenAiModel,
                TimeoutSeconds = RuntimeBootstrapOpenAiTimeoutSeconds,
                IsEnabled = true,
                SupportsStreaming = true,
                SupportsToolCalling = true,
                SupportsStructuredOutput = true,
                SupportsVision = false,
                LastHealthStatus = "OpenAI active",
                LastHealthCheckAtUtc = null,
                ExtraSettingsJson = BuildRuntimeBootstrapOpenAiChatCompletionsProviderConfigurationJson(openAiSecretId)
            };
            dbContext.Set<CanDoItAll.Modules.Workspace.ProviderProfile>().Add(openAiChatCompletionsProvider);
            changed = true;
        }
        else
        {
            changed |= UpdateRuntimeBootstrapOpenAiChatCompletionsProvider(openAiChatCompletionsProvider);
        }

        if (openAiSecretId.HasValue && openAiChatCompletionsProvider.ApiKeySecretId != openAiSecretId.Value)
        {
            openAiChatCompletionsProvider.ApiKeySecretId = openAiSecretId.Value;
            changed = true;
        }

        changed |= UpdateRuntimeBootstrapOpenAiChatCompletionsProviderConfigurationJson(openAiChatCompletionsProvider);
        changed |= await EnsureManagedCatalogProviderSeedsAsync(
            dbContext,
            openAiSecretId,
            cancellationToken);

        var settings = await dbContext.Set<WorkspaceSettings>()
            .FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            dbContext.Set<WorkspaceSettings>().Add(new WorkspaceSettings
            {
                DefaultProviderProfileId = RuntimeBootstrapOpenAiProviderId,
                WorkspaceName = "CanDoItAll",
                DefaultPromptOutputFormat = "Markdown",
                Notes = "Runtime bootstrap default provider.",
                UpdatedAtUtc = timestamp
            });
            changed = true;
        }
        else if (ShouldReplaceDefaultProvider(settings.DefaultProviderProfileId, openAiProvider.Id, dbContext))
        {
            settings.DefaultProviderProfileId = openAiProvider.Id;
            settings.UpdatedAtUtc = timestamp;
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Seeded OpenAI provider bootstrap for profile {ProfileId}.",
                profile.Profile.Id);
        }
    }

    private async Task<bool> EnsureManagedCatalogProviderSeedsAsync(
        AppDbContext dbContext,
        Guid? openAiSecretId,
        CancellationToken cancellationToken)
    {
        var seeds = CreateManagedCatalogProviderSeeds(openAiSecretId);
        var seedIds = seeds.Select(seed => seed.Id).ToArray();
        var seedNames = seeds.Select(seed => seed.Name).ToArray();
        var existingProviders = await dbContext
            .Set<CanDoItAll.Modules.Workspace.ProviderProfile>()
            .Where(provider =>
                seedIds.Contains(provider.Id) ||
                seedNames.Contains(provider.Name))
            .ToListAsync(cancellationToken);
        var changed = false;

        foreach (var seed in seeds)
        {
            var byId = existingProviders
                .FirstOrDefault(provider => provider.Id == seed.Id);
            var byName = existingProviders
                .FirstOrDefault(provider =>
                    string.Equals(
                        provider.Name,
                        seed.Name,
                        StringComparison.Ordinal));
            if (byId is not null &&
                !string.Equals(byId.Name, seed.Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Managed provider seed '{seed.Name}' cannot use Id '{seed.Id:D}' because that Id belongs to '{byId.Name}'.");
            }

            if (byName is not null && byName.Id != seed.Id)
            {
                throw new InvalidOperationException(
                    $"Managed provider seed '{seed.Name}' must use Id '{seed.Id:D}', but the canonical provider uses '{byName.Id:D}'.");
            }

            if (byId is not null)
            {
                continue;
            }

            dbContext
                .Set<CanDoItAll.Modules.Workspace.ProviderProfile>()
                .Add(seed.CreateEntity());
            changed = true;
        }

        return changed;
    }

    private static IReadOnlyList<ManagedCatalogProviderSeed>
        CreateManagedCatalogProviderSeeds(Guid? openAiSecretId)
    {
        return
        [
            new ManagedCatalogProviderSeed(
                RuntimeBootstrapOpenAiImageProviderId,
                RuntimeBootstrapOpenAiImageProviderName,
                CanDoItAll.Modules.Workspace.ProviderKind.OpenAi,
                OpenAiProviderAdapter.PluginKey,
                RuntimeBootstrapOpenAiBaseUrl,
                openAiSecretId,
                RuntimeBootstrapOpenAiImageModel,
                RuntimeBootstrapOpenAiTimeoutSeconds,
                SupportsStreaming: false,
                SupportsToolCalling: false,
                SupportsStructuredOutput: false,
                BuildManagedProviderConfigurationJson(
                    "{}",
                    OpenAiProviderAdapter.PluginKey,
                    openAiSecretId,
                    RuntimeBootstrapOpenAiTimeoutSeconds,
                    ProviderTransportKind.Responses,
                    ProviderProfilePurpose.ImageGeneration,
                    ["cloud", "image", "image-generation", "openai"],
                    isPrivateProvider: false,
                    ProviderPricingDefaults.CreateDefaultPrices(
                        CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi,
                        RuntimeBootstrapOpenAiImageModel))),
            new ManagedCatalogProviderSeed(
                RuntimeBootstrapComfyUiProviderId,
                ComfyUiFluxProviderDefaults.ProviderName,
                ProviderKind: null,
                ComfyUiProviderAdapter.PluginKey,
                ComfyUiFluxProviderDefaults.DefaultBaseUrl,
                ApiKeySecretId: null,
                ComfyUiFluxProviderDefaults.DefaultModel,
                ComfyUiFluxProviderDefaults.TimeoutSeconds,
                SupportsStreaming: false,
                SupportsToolCalling: false,
                SupportsStructuredOutput: false,
                BuildManagedProviderConfigurationJson(
                    ComfyUiFluxProviderDefaults.CreateConfigurationJson(),
                    ComfyUiProviderAdapter.PluginKey,
                    secretRecordId: null,
                    ComfyUiFluxProviderDefaults.TimeoutSeconds,
                    ProviderTransportKind.ChatCompletions,
                    ProviderProfilePurpose.ImageGeneration,
                    ["comfyui", "flux", "image", "image-generation", "local"],
                    isPrivateProvider: true,
                    ProviderPricingDefaults.CreateDefaultPrices(
                        CanDoItAll.AgentFramework.Models.ProviderKind.ComfyUi,
                        ComfyUiFluxProviderDefaults.DefaultModel))),
            new ManagedCatalogProviderSeed(
                RuntimeBootstrapLocalOllamaProviderId,
                RuntimeBootstrapLocalOllamaProviderName,
                CanDoItAll.Modules.Workspace.ProviderKind.OllamaLocal,
                OllamaProviderAdapter.PluginKey,
                RuntimeBootstrapLocalOllamaBaseUrl,
                ApiKeySecretId: null,
                RuntimeBootstrapLocalOllamaModel,
                RuntimeBootstrapLocalOllamaTimeoutSeconds,
                SupportsStreaming: true,
                SupportsToolCalling: true,
                SupportsStructuredOutput: false,
                BuildManagedProviderConfigurationJson(
                    JsonSerializer.Serialize(new
                    {
                        history = "framework-managed",
                        local = true,
                        modelParameters = new
                        {
                            numPredict =
                                AgentProviderModelParameterPolicy
                                    .DefaultOllamaMaxOutputTokens,
                            think =
                                AgentProviderModelParameterPolicy
                                    .DefaultOllamaThinkEnabled
                        }
                    }),
                    OllamaProviderAdapter.PluginKey,
                    secretRecordId: null,
                    RuntimeBootstrapLocalOllamaTimeoutSeconds,
                    ProviderTransportKind.ChatCompletions,
                    ProviderProfilePurpose.Chat,
                    ["chat", "local", "ollama"],
                    isPrivateProvider: true,
                    ProviderPricingDefaults.CreateDefaultPrices(
                        CanDoItAll.AgentFramework.Models.ProviderKind.Ollama,
                        RuntimeBootstrapLocalOllamaModel)))
        ];
    }

    private static string BuildManagedProviderConfigurationJson(
        string configurationJson,
        string connectorPluginKey,
        Guid? secretRecordId,
        int timeoutSeconds,
        ProviderTransportKind transport,
        ProviderProfilePurpose purpose,
        IEnumerable<string> tags,
        bool isPrivateProvider,
        IReadOnlyList<ProviderModelTokenPrice> modelPrices)
    {
        var configuration = JsonNode.Parse(configurationJson)?.AsObject()
            ?? new JsonObject();
        configuration["connectorPluginKey"] = connectorPluginKey;
        configuration["configSchemaVersion"] =
            RuntimeBootstrapProviderSchemaVersion;
        configuration["timeoutSeconds"] = timeoutSeconds;
        configuration["providerTransport"] = transport.ToString();
        configuration["providerPurpose"] = purpose.ToString();
        if (secretRecordId.HasValue)
        {
            configuration["secretRecordId"] =
                secretRecordId.Value.ToString("D");
            configuration["apiKeyEnvironmentVariable"] =
                RuntimeBootstrapOpenAiApiKeyEnvironmentVariable;
        }

        var tagArray = new JsonArray();
        foreach (var tag in tags
                     .Where(tag => !string.IsNullOrWhiteSpace(tag))
                     .Select(tag => tag.Trim().TrimStart('#').ToLowerInvariant())
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase))
        {
            tagArray.Add(tag);
        }

        configuration["tags"] = tagArray;
        return ProviderPricingMetadata.Write(
            configuration.ToJsonString(),
            isPrivateProvider,
            modelPrices);
    }

    private sealed record ManagedCatalogProviderSeed(
        Guid Id,
        string Name,
        CanDoItAll.Modules.Workspace.ProviderKind? ProviderKind,
        string ConnectorPluginKey,
        string BaseUrl,
        Guid? ApiKeySecretId,
        string DefaultModel,
        int TimeoutSeconds,
        bool SupportsStreaming,
        bool SupportsToolCalling,
        bool SupportsStructuredOutput,
        string ExtraSettingsJson)
    {
        public CanDoItAll.Modules.Workspace.ProviderProfile CreateEntity()
        {
            return new CanDoItAll.Modules.Workspace.ProviderProfile
            {
                Id = Id,
                Name = Name,
                ProviderKind = ProviderKind,
                ConnectorPluginKey = ConnectorPluginKey,
                ConfigSchemaVersion =
                    RuntimeBootstrapProviderSchemaVersion,
                BaseUrl = BaseUrl,
                ApiKeySecretId = ApiKeySecretId,
                DefaultModel = DefaultModel,
                TimeoutSeconds = TimeoutSeconds,
                IsEnabled = true,
                SupportsStreaming = SupportsStreaming,
                SupportsToolCalling = SupportsToolCalling,
                SupportsStructuredOutput = SupportsStructuredOutput,
                SupportsVision = false,
                LastHealthStatus = "Not checked",
                ExtraSettingsJson = ExtraSettingsJson
            };
        }
    }

    private async Task<Guid?> EnsureDefaultOpenAiSecretAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var configuredKey = Environment.GetEnvironmentVariable(RuntimeBootstrapOpenAiApiKeyEnvironmentVariable);
        var existingSecret = await dbContext.Set<SecretRecord>()
            .Where(item => item.Id == DefaultOpenAiApiKeySecretId || item.Name == DefaultOpenAiApiKeySecretName)
            .OrderBy(item => item.Id == DefaultOpenAiApiKeySecretId ? 0 : 1)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return existingSecret?.Id;
        }

        var normalizedKey = configuredKey.Trim();
        var timestamp = DateTimeOffset.UtcNow;
        if (existingSecret is null)
        {
            existingSecret = new SecretRecord
            {
                Id = DefaultOpenAiApiKeySecretId,
                CreatedAtUtc = timestamp
            };
            dbContext.Set<SecretRecord>().Add(existingSecret);
        }

        var oldVaultKey = SecretVaultRecordReference.TryParse(existingSecret.EncryptedPayload, out var parsedOldVaultKey)
            ? parsedOldVaultKey
            : null;
        var existingValue = string.IsNullOrWhiteSpace(oldVaultKey)
            ? null
            : await secretVault.GetAsync(oldVaultKey, cancellationToken);
        var metadataJson = JsonSerializer.Serialize(new
        {
            source = "environment",
            environmentVariable = RuntimeBootstrapOpenAiApiKeyEnvironmentVariable,
            managedBy = "runtime-bootstrap"
        });
        var metadataChanged =
            !string.Equals(existingSecret.Name, DefaultOpenAiApiKeySecretName, StringComparison.Ordinal) ||
            existingSecret.Kind != SecretKind.ApiKey ||
            !string.Equals(existingSecret.Scope, "workspace", StringComparison.Ordinal) ||
            !string.Equals(existingSecret.MetadataJson, metadataJson, StringComparison.Ordinal);

        if (string.Equals(existingValue, normalizedKey, StringComparison.Ordinal) && !metadataChanged)
        {
            return existingSecret.Id;
        }

        var newVaultKey = SecretVaultRecordReference.BuildKey(existingSecret.Id, Guid.NewGuid());
        await secretVault.SetAsync(newVaultKey, normalizedKey, cancellationToken);

        existingSecret.Name = DefaultOpenAiApiKeySecretName;
        existingSecret.Kind = SecretKind.ApiKey;
        existingSecret.Scope = "workspace";
        existingSecret.MetadataJson = metadataJson;
        existingSecret.RotationNote = $"Synchronized from {RuntimeBootstrapOpenAiApiKeyEnvironmentVariable}.";
        existingSecret.EncryptedPayload = SecretVaultRecordReference.Create(newVaultKey);
        existingSecret.UpdatedAtUtc = timestamp;

        await dbContext.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(oldVaultKey) &&
            !string.Equals(oldVaultKey, newVaultKey, StringComparison.Ordinal))
        {
            await secretVault.DeleteAsync(oldVaultKey, cancellationToken);
        }

        return existingSecret.Id;
    }

    private static bool ShouldReplaceDefaultProvider(
        Guid? currentDefaultProviderId,
        Guid openAiProviderId,
        AppDbContext dbContext)
    {
        if (currentDefaultProviderId == openAiProviderId)
        {
            return false;
        }

        if (!currentDefaultProviderId.HasValue)
        {
            return true;
        }

        return !dbContext.Set<CanDoItAll.Modules.Workspace.ProviderProfile>()
            .Any(item => item.Id == currentDefaultProviderId.Value);
    }

    private static bool UpdateRuntimeBootstrapOpenAiProvider(CanDoItAll.Modules.Workspace.ProviderProfile provider)
    {
        var changed = false;
        if (!string.Equals(provider.Name, RuntimeBootstrapOpenAiProviderName, StringComparison.Ordinal))
        {
            provider.Name = RuntimeBootstrapOpenAiProviderName;
            changed = true;
        }

        if (provider.ProviderKind != CanDoItAll.Modules.Workspace.ProviderKind.OpenAi)
        {
            provider.ProviderKind = CanDoItAll.Modules.Workspace.ProviderKind.OpenAi;
            changed = true;
        }

        if (!string.Equals(provider.ConnectorPluginKey, OpenAiProviderAdapter.PluginKey, StringComparison.Ordinal))
        {
            provider.ConnectorPluginKey = OpenAiProviderAdapter.PluginKey;
            changed = true;
        }

        if (!string.Equals(provider.ConfigSchemaVersion, RuntimeBootstrapProviderSchemaVersion, StringComparison.Ordinal))
        {
            provider.ConfigSchemaVersion = RuntimeBootstrapProviderSchemaVersion;
            changed = true;
        }

        if (!string.Equals(provider.BaseUrl, RuntimeBootstrapOpenAiBaseUrl, StringComparison.Ordinal))
        {
            provider.BaseUrl = RuntimeBootstrapOpenAiBaseUrl;
            changed = true;
        }

        if (!string.Equals(provider.DefaultModel, RuntimeBootstrapOpenAiModel, StringComparison.Ordinal))
        {
            provider.DefaultModel = RuntimeBootstrapOpenAiModel;
            changed = true;
        }

        if (provider.TimeoutSeconds != RuntimeBootstrapOpenAiTimeoutSeconds)
        {
            provider.TimeoutSeconds = RuntimeBootstrapOpenAiTimeoutSeconds;
            changed = true;
        }

        if (!provider.IsEnabled)
        {
            provider.IsEnabled = true;
            changed = true;
        }

        if (!provider.SupportsStreaming)
        {
            provider.SupportsStreaming = true;
            changed = true;
        }

        if (!provider.SupportsToolCalling)
        {
            provider.SupportsToolCalling = true;
            changed = true;
        }

        if (!provider.SupportsStructuredOutput)
        {
            provider.SupportsStructuredOutput = true;
            changed = true;
        }

        if (provider.SupportsVision)
        {
            provider.SupportsVision = false;
            changed = true;
        }

        if (!string.Equals(provider.LastHealthStatus, "OpenAI active", StringComparison.Ordinal))
        {
            provider.LastHealthStatus = "OpenAI active";
            changed = true;
        }

        return changed;
    }

    private static bool UpdateRuntimeBootstrapOpenAiProviderConfigurationJson(CanDoItAll.Modules.Workspace.ProviderProfile provider)
    {
        var expectedExtraSettingsJson = JsonSerializer.Serialize(new
        {
            history = "service-managed",
            reasoningEffort = ManagedSeedProviderFallbacks.DefaultReasoningEffort,
            modelParameters = new
            {
                reasoningEffort = ManagedSeedProviderFallbacks.DefaultReasoningEffort
            },
            apiKeyEnvironmentVariable = RuntimeBootstrapOpenAiApiKeyEnvironmentVariable,
            connectorPluginKey = OpenAiProviderAdapter.PluginKey,
            configSchemaVersion = RuntimeBootstrapProviderSchemaVersion,
            secretRecordId = provider.ApiKeySecretId?.ToString("D"),
            providerTransport = nameof(ProviderTransportKind.Responses),
            timeoutSeconds = RuntimeBootstrapOpenAiTimeoutSeconds
        });
        if (string.Equals(provider.ExtraSettingsJson, expectedExtraSettingsJson, StringComparison.Ordinal))
        {
            return false;
        }

        provider.ExtraSettingsJson = expectedExtraSettingsJson;
        return true;
    }

    private static bool UpdateRuntimeBootstrapOpenAiChatCompletionsProvider(CanDoItAll.Modules.Workspace.ProviderProfile provider)
    {
        var changed = false;
        if (!string.Equals(provider.Name, RuntimeBootstrapOpenAiChatCompletionsProviderName, StringComparison.Ordinal))
        {
            provider.Name = RuntimeBootstrapOpenAiChatCompletionsProviderName;
            changed = true;
        }

        if (provider.ProviderKind != CanDoItAll.Modules.Workspace.ProviderKind.OpenAi)
        {
            provider.ProviderKind = CanDoItAll.Modules.Workspace.ProviderKind.OpenAi;
            changed = true;
        }

        if (!string.Equals(provider.ConnectorPluginKey, OpenAiProviderAdapter.PluginKey, StringComparison.Ordinal))
        {
            provider.ConnectorPluginKey = OpenAiProviderAdapter.PluginKey;
            changed = true;
        }

        if (!string.Equals(provider.ConfigSchemaVersion, RuntimeBootstrapProviderSchemaVersion, StringComparison.Ordinal))
        {
            provider.ConfigSchemaVersion = RuntimeBootstrapProviderSchemaVersion;
            changed = true;
        }

        if (!string.Equals(provider.BaseUrl, RuntimeBootstrapOpenAiBaseUrl, StringComparison.Ordinal))
        {
            provider.BaseUrl = RuntimeBootstrapOpenAiBaseUrl;
            changed = true;
        }

        if (!string.Equals(provider.DefaultModel, RuntimeBootstrapOpenAiModel, StringComparison.Ordinal))
        {
            provider.DefaultModel = RuntimeBootstrapOpenAiModel;
            changed = true;
        }

        if (provider.TimeoutSeconds != RuntimeBootstrapOpenAiTimeoutSeconds)
        {
            provider.TimeoutSeconds = RuntimeBootstrapOpenAiTimeoutSeconds;
            changed = true;
        }

        if (!provider.IsEnabled)
        {
            provider.IsEnabled = true;
            changed = true;
        }

        if (!provider.SupportsStreaming)
        {
            provider.SupportsStreaming = true;
            changed = true;
        }

        if (!provider.SupportsToolCalling)
        {
            provider.SupportsToolCalling = true;
            changed = true;
        }

        if (!provider.SupportsStructuredOutput)
        {
            provider.SupportsStructuredOutput = true;
            changed = true;
        }

        if (provider.SupportsVision)
        {
            provider.SupportsVision = false;
            changed = true;
        }

        if (!string.Equals(provider.LastHealthStatus, "OpenAI active", StringComparison.Ordinal))
        {
            provider.LastHealthStatus = "OpenAI active";
            changed = true;
        }

        return changed;
    }

    private static bool UpdateRuntimeBootstrapOpenAiChatCompletionsProviderConfigurationJson(CanDoItAll.Modules.Workspace.ProviderProfile provider)
    {
        var expectedExtraSettingsJson = BuildRuntimeBootstrapOpenAiChatCompletionsProviderConfigurationJson(provider.ApiKeySecretId);
        if (string.Equals(provider.ExtraSettingsJson, expectedExtraSettingsJson, StringComparison.Ordinal))
        {
            return false;
        }

        provider.ExtraSettingsJson = expectedExtraSettingsJson;
        return true;
    }

    private static string BuildRuntimeBootstrapOpenAiChatCompletionsProviderConfigurationJson(Guid? secretRecordId)
        => JsonSerializer.Serialize(new
        {
            history = "framework-managed",
            apiKeyEnvironmentVariable = RuntimeBootstrapOpenAiApiKeyEnvironmentVariable,
            connectorPluginKey = OpenAiProviderAdapter.PluginKey,
            configSchemaVersion = RuntimeBootstrapProviderSchemaVersion,
            secretRecordId = secretRecordId?.ToString("D"),
            providerTransport = nameof(ProviderTransportKind.ChatCompletions),
            timeoutSeconds = RuntimeBootstrapOpenAiTimeoutSeconds
        });

}

public sealed class DatabaseSwitchCoordinator(
    IDatabaseProfileRuntimeAccessor profileAccessor,
    IDatabaseProfileService profileService,
    IDatabaseDriverRegistry driverRegistry,
    IAppDatabaseBootstrapper bootstrapper,
    ILogger<DatabaseSwitchCoordinator> logger) : IDatabaseSwitchCoordinator
{
    public async Task<Result<DatabaseSwitchResult>> SwitchAsync(Guid targetProfileId, CancellationToken cancellationToken = default)
    {
        var currentProfile = profileAccessor.ResolveCurrentProfile();
        if (currentProfile.Profile.Runtime.LockedByRuntimeOverride)
        {
            return Result<DatabaseSwitchResult>.Failure(
                Error.Failure("Runtime override is active. Database switching is disabled."));
        }

        if (currentProfile.Profile.Id == targetProfileId)
        {
            return Result<DatabaseSwitchResult>.Success(new DatabaseSwitchResult(
                currentProfile.Profile.Id,
                currentProfile.Profile.Id,
                0,
                Environment.ProcessId)
            {
                RuntimeProfileId = currentProfile.Profile.Id,
                PendingRestartProfileId = null,
                RequiresRestart = false,
                RuntimeChangedInProcess = false,
                Message = "The selected database profile is already the canonical runtime profile for this process."
            });
        }

        ResolvedDatabaseProfile targetProfile;
        try
        {
            targetProfile = profileAccessor.ResolveProfile(targetProfileId);
        }
        catch (Exception ex)
        {
            return Result<DatabaseSwitchResult>.Failure(Error.Failure(ex.Message));
        }

        try
        {
            await driverRegistry.Resolve(targetProfile.Profile.ProviderKind)
                .EnsureDatabaseAsync(targetProfile, cancellationToken);
            await bootstrapper.EnsureProfileReadyAsync(targetProfile, cancellationToken);

            var activationResult = await profileService.ActivateAsync(targetProfileId, cancellationToken);
            if (activationResult.IsFailure)
            {
                return Result<DatabaseSwitchResult>.Failure(activationResult.Errors);
            }

            logger.LogInformation(
                "Persisted database profile activation from runtime profile {RuntimeProfileId} to pending restart profile {PendingRestartProfileId}. RestartRequired={RestartRequired}.",
                currentProfile.Profile.Id,
                targetProfile.Profile.Id,
                true);

            return Result<DatabaseSwitchResult>.Success(new DatabaseSwitchResult(
                currentProfile.Profile.Id,
                targetProfile.Profile.Id,
                0,
                Environment.ProcessId)
            {
                RuntimeProfileId = currentProfile.Profile.Id,
                PendingRestartProfileId = targetProfile.Profile.Id,
                RequiresRestart = true,
                RuntimeChangedInProcess = false,
                Message = "Database profile activation was saved. Restart the process to make it the canonical runtime database."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Database activation from {PreviousProfileId} to {TargetProfileId} failed.",
                currentProfile.Profile.Id,
                targetProfileId);

            return Result<DatabaseSwitchResult>.Failure(
                Error.Failure($"Database activation failed: {ex.Message}"));
        }
    }
}

