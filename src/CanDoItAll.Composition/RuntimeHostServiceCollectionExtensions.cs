using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Rag.Driver.Models;
using CanDoItAll.AgentFramework.Rag.Qdrant.DependencyInjection;
using CanDoItAll.AgentFramework.SemanticCompletion.Driver.Embeddings;
using CanDoItAll.Modules.Activity;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Validation;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    private const string QdrantRagConfigurationSection = "Rag:Qdrant";

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
        services.AddWorkbenchModule();
        services.AddResourcesModule();
        services.AddPromptsModule();
        services.AddFactoryModule();
        services.AddPluginsModule(configuration, contentRootPath);
        services.AddCanDoItAllGmailPlugin();
        services.AddCanDoItAllOffice365Plugin();
        services.AddProcessesModule(configuration);
        services.AddValidationModule();
        services.AddTestLabModule();
        services.AddActivityModule();
        services.AddAgentFrameworkModule(configuration);
        services.AddAutomationModule(configuration, contentRootPath);
        services.AddConfiguredQdrantRagDriver(configuration);
        services.AddCognitiveMemoryModule();
        services.AddSchedulerPlannerModule();
        services.AddCollaborationModule();
        services.AddCrmHrModule();
        services.AddSchedulerPlannerWorkflowInputOptionProviders();
        return services;
    }

    private static IServiceCollection AddConfiguredQdrantRagDriver(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(QdrantRagConfigurationSection);
        if (!section.Exists() || !(section.GetValue<bool?>("Enabled") ?? false))
        {
            return services;
        }

        var collectionName = section["CollectionName"];
        var vectorSize = section.GetValue<int?>("VectorSize") ?? 384;
        var effectiveCollectionName = string.IsNullOrWhiteSpace(collectionName)
            ? "candoitall-knowledge"
            : collectionName.Trim();
        var embeddingProfileId = string.IsNullOrWhiteSpace(section["EmbeddingProfileId"])
            ? $"local-hashing-v1:dimension={vectorSize}"
            : section["EmbeddingProfileId"]!.Trim();
        var projectionProfileId = string.IsNullOrWhiteSpace(section["ProjectionProfileId"])
            ? "qdrant-default-v1"
            : section["ProjectionProfileId"]!.Trim();
        var grpcPort = section.GetValue<int?>("GrpcPort") ??
                       section.GetValue<int?>("Port") ??
                       6334;
        var distance = ReadQdrantDistance(section["Distance"]);

        services.TryAddSingleton<IAgentTextEmbeddingGenerator>(_ =>
            new LocalHashingAgentTextEmbeddingGenerator(new LocalHashingAgentTextEmbeddingOptions
            {
                Dimension = vectorSize,
                ProfileId = embeddingProfileId
            }));
        services.Configure<CognitiveMemoryProjectionOptions>(options =>
        {
            options.Enabled = true;
            options.CollectionName = effectiveCollectionName;
            options.ProjectionProfileId = projectionProfileId;
            options.EmbeddingProfileId = embeddingProfileId;
            options.TargetProviderName = RagDriverProviderNames.Qdrant;
            options.ProjectionStoreKind = CognitiveMemoryProjectionStoreKind.Qdrant;
            options.VectorDimensions = vectorSize;
        });

        services.AddQdrantRagDriver(
            configureQdrant: options =>
            {
                options.Host = string.IsNullOrWhiteSpace(section["Host"]) ? "localhost" : section["Host"]!.Trim();
                options.Port = grpcPort;
                options.Https = section.GetValue<bool?>("Https") ?? false;
                options.ApiKey = string.IsNullOrWhiteSpace(section["ApiKey"]) ? null : section["ApiKey"]!.Trim();
                options.CreateCollectionIfMissing = section.GetValue<bool?>("CreateCollectionIfMissing") ?? true;
                options.WaitForWrites = section.GetValue<bool?>("WaitForWrites") ?? true;

                var grpcTimeout = section.GetValue<TimeSpan?>("GrpcTimeout");
                if (grpcTimeout.HasValue)
                {
                    options.GrpcTimeout = grpcTimeout.Value;
                }
            },
            configureFactory: options =>
            {
                options.DefaultCollection = new RagCollectionOptions
                {
                    CollectionName = effectiveCollectionName,
                    VectorSize = vectorSize,
                    Distance = distance
                };
            },
            configureEmbedding: options => options.Dimension = vectorSize);

        return services;
    }

    private static RagDistanceMetric ReadQdrantDistance(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return RagDistanceMetric.Cosine;
        }

        if (Enum.TryParse<RagDistanceMetric>(value.Trim(), ignoreCase: true, out var distance))
        {
            return distance;
        }

        throw new InvalidOperationException(
            $"Unsupported {QdrantRagConfigurationSection}:Distance value '{value}'.");
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
    private const string RuntimeBootstrapOpenAiProviderName = ManagedSeedProviderFallbacks.OpenAiDefaultProviderName;
    private const string RuntimeBootstrapOpenAiBaseUrl = "https://api.openai.com/v1";
    private const string RuntimeBootstrapOpenAiApiKeyEnvironmentVariable = "OPENAI_API_KEY";
    private const string RuntimeBootstrapOpenAiModel = ManagedSeedProviderFallbacks.OpenAiDefaultModel;
    private const string RuntimeBootstrapProviderSchemaVersion = "1.0";
    private const int RuntimeBootstrapOpenAiTimeoutSeconds = 600;
    private static readonly Guid DefaultOpenAiApiKeySecretId = Guid.Parse("86F781F1-1E76-4B45-9F1A-42B8CF13D8C7");
    private const string DefaultOpenAiApiKeySecretName = "OpenAI API key";
    private const string InitialPostgreSqlBaselineMigrationId = "20260528182412_InitialPostgreSqlBaseline";
    private const string StepDispatchClaimIndexName = "IX_Processes_StepRuns_ProcessRunId_AutomationDispatchLeaseExpi~";
    private static readonly string[] BaselineSentinelTables =
    [
        "Activity_Entries",
        "Projects_Projects",
        "Processes_Outbox",
        "Workspace_ProviderProfiles"
    ];
    private static readonly string[] ProcessStepDispatchClaimColumns =
    [
        "AutomationDispatchAttemptCount",
        "AutomationDispatchClaimToken",
        "AutomationDispatchClaimedAtUtc",
        "AutomationDispatchClaimedBy",
        "AutomationDispatchLeaseExpiresAtUtc"
    ];
    private static readonly PostgreSqlColumnRequirement[] MergedBaselineColumnRequirements =
    [
        new("Processes_StepRuns", ProcessStepDispatchClaimColumns),
        new("Processes_StepRuns",
        [
            "BlockReasonCode",
            "RecoveryOptionsJson",
            "NextRecoveryAction"
        ]),
        new("Processes_ArtifactRecords",
        [
            "ProjectionLineageJson",
            "ProjectionIdentityHash"
        ]),
        new("Processes_StepDefinitions",
        [
            "AllowedOperations",
            "OperationTargetScope"
        ]),
        new("Processes_DefinitionVersions", ["ContractMode"]),
        new("Processes_ArtifactExpectations",
        [
            "SubprocessChildArtifactExpectationId",
            "WorkflowOutputId",
            "WorkflowOutputKind",
            "WorkflowOutputName"
        ])
    ];
    private static readonly string[] MergedBaselineIndexRequirements =
    [
        "IX_Processes_ArtifactExpectations_SubprocessChildArtifactExpec~",
        "IX_Processes_ArtifactRecords_ProcessRunId_ProjectionIdentityHa~"
    ];

    private readonly record struct PostgreSqlColumnRequirement(string TableName, string[] ColumnNames);

    public Task EnsureCurrentProfileReadyAsync(CancellationToken cancellationToken = default)
    {
        return EnsureProfileReadyAsync(profileAccessor.ResolveCurrentProfile(), cancellationToken);
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
            "Ensuring Quartz automation schema for profile {ProfileId}.",
            profile.Profile.Id);
        await AutomationQuartzSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
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
                $"PostgreSQL profile '{profile.Profile.DisplayName}' has CanDoItAll tables but does not match the merged PostgreSQL baseline. Missing schema requirements: {string.Join(", ", missingRequirements)}. Refusing to record migration {InitialPostgreSqlBaselineMigrationId} automatically.");
        }

        logger.LogWarning(
            "PostgreSQL profile {ProfileId} has an existing current CanDoItAll schema but no recorded merged EF migration. Recording baseline migration {MigrationId} before applying pending migrations.",
            profile.Profile.Id,
            InitialPostgreSqlBaselineMigrationId);

        await EnsurePostgreSqlMigrationHistoryTableAsync(dbContext, cancellationToken);
        await EnsureProcessStepDispatchClaimIndexAsync(dbContext, cancellationToken);
        await EnsureProcessClaimHotPathIndexesAsync(dbContext, cancellationToken);
        await MarkPostgreSqlMigrationAppliedAsync(dbContext, InitialPostgreSqlBaselineMigrationId, cancellationToken);
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

        var modelTableNames = dbContext.Model.GetEntityTypes()
            .Select(entityType => entityType.GetTableName())
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tableName => tableName, StringComparer.Ordinal);

        foreach (var tableName in modelTableNames) {
            if (!await PostgreSqlTableExistsAsync(dbContext, tableName, cancellationToken)) {
                missingRequirements.Add($"table {tableName}");
            }
        }

        foreach (var requirement in MergedBaselineColumnRequirements) {
            foreach (var columnName in requirement.ColumnNames) {
                if (!await PostgreSqlColumnExistsAsync(dbContext, requirement.TableName, columnName, cancellationToken)) {
                    missingRequirements.Add($"column {requirement.TableName}.{columnName}");
                }
            }
        }

        foreach (var indexName in MergedBaselineIndexRequirements) {
            if (!await PostgreSqlIndexExistsAsync(dbContext, indexName, cancellationToken)) {
                missingRequirements.Add($"index {indexName}");
            }
        }

        return missingRequirements;
    }

    private static async Task<bool> PostgreSqlColumnExistsAsync(
        AppDbContext dbContext,
        string tableName,
        string columnName,
        CancellationToken cancellationToken) {
        var result = await ExecutePostgreSqlScalarAsync(
            dbContext,
            """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = @tableName
                  AND column_name = @columnName
            );
            """,
            cancellationToken,
            ("@tableName", tableName),
            ("@columnName", columnName));

        return result is true;
    }

    private static async Task<bool> PostgreSqlIndexExistsAsync(
        AppDbContext dbContext,
        string indexName,
        CancellationToken cancellationToken) {
        var result = await ExecutePostgreSqlScalarAsync(
            dbContext,
            """
            SELECT EXISTS (
                SELECT 1
                FROM pg_indexes
                WHERE schemaname = current_schema()
                  AND indexname = @indexName
            );
            """,
            cancellationToken,
            ("@indexName", indexName));

        return result is true;
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

    private static Task EnsureProcessStepDispatchClaimIndexAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
        => ExecutePostgreSqlNonQueryAsync(
            dbContext,
            $"""
            CREATE INDEX IF NOT EXISTS "{StepDispatchClaimIndexName}"
            ON "Processes_StepRuns" ("ProcessRunId", "AutomationDispatchLeaseExpiresAtUtc");
            """,
            cancellationToken);

    private static Task EnsureProcessClaimHotPathIndexesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
        => ExecutePostgreSqlNonQueryAsync(
            dbContext,
            """
            CREATE INDEX IF NOT EXISTS "IX_Processes_Outbox_PendingClaimOrder"
            ON "Processes_Outbox" ((COALESCE("NextAttemptAtUtc", "CreatedAtUtc")), "CreatedAtUtc")
            INCLUDE ("Id", "CommandKey", "ProcessRunId", "LeaseExpiresAtUtc")
            WHERE "Status" = 0;

            CREATE INDEX IF NOT EXISTS "IX_Automation_EnvelopeDeliveries_DueClaimOrder"
            ON "Automation_EnvelopeDeliveries" ("AvailableAtUtc", "CreatedAtUtc")
            INCLUDE ("Id", "EnvelopeId", "State", "LockedAtUtc")
            WHERE "State" IN (0, 1, 2);

            CREATE INDEX IF NOT EXISTS "IX_Workspace_ConnectorCommands_PendingClaimOrder"
            ON "Workspace_ConnectorCommands" ((COALESCE("NextAttemptAtUtc", "CreatedAtUtc")), "CreatedAtUtc")
            INCLUDE ("Id", "ProjectId", "ConnectorPluginKey", "CommandKey", "LeaseExpiresAtUtc")
            WHERE "Status" = 0 AND "ApprovalState" <> 1;
            """,
            cancellationToken);

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
