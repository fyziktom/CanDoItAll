using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Agents.SimpleChats;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Composition.Memory;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.AgentFramework.Llm.SimpleChats;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime;
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
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.SharedProviders.Http;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;
using System.Data.Common;
using System.Reflection;

namespace CanDoItAll.Composition;

public static class RuntimeHostServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllRuntimeModules(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string? contentRootPath = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.Configure<ProviderInitializationOptions>(configuration.GetSection(ProviderInitializationOptions.SectionName));

        services.AddSecurityModule(configuration);
        services.AddAgentFrameworkProviderManagement();
        services.AddWorkspaceModule();
        services.AddScoped<IWorkspaceProviderCatalog, ProviderManagementWorkspaceProviderCatalog>();
        services.AddScoped<IDatabaseTransferHandler, WorkspaceDefaultProviderDatabaseTransferHandler>();
        services.AddSharedProviderHttpDescriptors();
        services.AddSharedProviderRuntimeAccessContextPropagation();
        services.TryAddSingleton<
            IProviderHttpClientSelector,
            SharedProviderRuntimeHttpClientSelector>();
        services.AddProjectsModule();
        services.AddCanDoItAllMemory(configuration);
        services.AddWorkbenchModule(configuration);
        services.AddResourcesModule();
        services.AddPromptsModule();
        services.AddPluginsModule(configuration, contentRootPath);
        services.AddCanDoItAllGmailPlugin();
        services.AddCanDoItAllOffice365Plugin();
        services.AddProcessesModule(configuration);
        services.TryAddScoped<IAgentCatalogReadLeaseStore, CanonicalAgentCatalogLeaseSource>();
        services.TryAddScoped<IProcessProjectAdmissionPolicy, ProjectProcessAdmissionPolicy>();
        services.TryAddScoped<ProjectProcessLaunchTargetQuery>();
        services.TryAddScoped<ProjectProcessLaunchDeliveryService>();
        services.TryAddScoped<ProjectProcessLaunchAuthorityService>();
        services.TryAddScoped<IProcessLaunchAuthorityPolicy>(provider => provider.GetRequiredService<ProjectProcessLaunchAuthorityService>());
        services.TryAddScoped<IProcessLaunchOperatorAuthoritySource>(provider => provider.GetRequiredService<ProjectProcessLaunchAuthorityService>());
        services.TryAddScoped<IProcessSourceAuthorityObservationPolicy>(provider => provider.GetRequiredService<ProjectProcessLaunchAuthorityService>());
        services.TryAddScoped<ProjectProcessExecutionMutationService>();
        services.TryAddScoped<ProjectAgentNativeMutationService>();
        services.TryAddScoped<ProjectAgentSourceMutationAuthority>();
        services.TryAddScoped<IProjectCreationCompensationGuard, ProjectCreationCompensationGuard>();
        services.TryAddSingleton<ProjectProcessAssetProposalCodec>();
        services.TryAddScoped<ProjectProcessAssetToolAdmission>();
        services.AddScoped<IAgentToolReceiptReconciliationProvider>(provider => provider.GetRequiredService<ProjectProcessAssetToolAdmission>());
        services.TryAddScoped<IProcessToolLaunchAdmissionPolicy, ProjectProcessToolLaunchAdmissionPolicy>();
        services.TryAddSingleton<WorkflowProcessToolProposalCodec>();
        services.TryAddScoped<WorkflowProcessToolAdmission>();
        services.AddScoped<IAgentToolReceiptReconciliationProvider>(provider => provider.GetRequiredService<WorkflowProcessToolAdmission>());
        services.TryAddSingleton<ProjectStructureProcessProposalCodec>();
        services.TryAddScoped<ProjectStructureProcessToolAdmission>();
        services.AddScoped<IAgentToolReceiptReconciliationProvider>(provider => provider.GetRequiredService<ProjectStructureProcessToolAdmission>());
        services.TryAddScoped<IWorkflowScheduledSourceAuthorityPolicy, ProjectScheduledWorkflowSourceAuthorityPolicy>();
        services.AddTestLabModule();
        services.AddAgentFrameworkModule(configuration);
        services
            .AddOptions<LlmChatExecutionLeaseOptions>()
            .Bind(configuration.GetSection(LlmChatExecutionLeaseOptions.SectionName))
            .Validate(static options => IsValid(options.Validate), "LLM Chat dispatcher configuration is invalid.")
            .ValidateOnStart();
        services
            .AddOptions<LlmChatStreamingOptions>()
            .Bind(configuration.GetSection(LlmChatStreamingOptions.SectionName))
            .Validate(static options => IsValid(options.Validate), "LLM Chat streaming configuration is invalid.")
            .ValidateOnStart();
        services
            .AddOptions<LlmChatTransferOptions>()
            .Bind(configuration.GetSection(LlmChatTransferOptions.SectionName))
            .Validate(static options => IsValid(options.Validate), "LLM Chat transfer configuration is invalid.")
            .ValidateOnStart();
        services.AddSingleton(provider =>
            provider.GetRequiredService<IOptions<LlmChatExecutionLeaseOptions>>().Value);
        services.AddSingleton(provider =>
            provider.GetRequiredService<IOptions<LlmChatStreamingOptions>>().Value);
        services.AddSingleton(provider =>
            provider.GetRequiredService<IOptions<LlmChatTransferOptions>>().Value);
        services.TryAddSingleton<IAgentToolAdmissionVerifier, AgentToolAdmissionVerifier>();
        services.AddHrSimpleChatDefinitionTools();
        services.AddSimpleChatsApplication();
        services.AddSimpleChatsRuntime();
        services.AddLlmChatsPersistence();
        services.AddProviderHistoryPersistence();
        services.AddHostedService<LlmChatOperationDispatcherHostedService>();
        services.AddSchedulerPlannerModule(configuration);
        services.AddCollaborationModule();
        services.AddCrmHrModule();
        services.AddSchedulerPlannerWorkflowInputOptionProviders();
        services.AddCanDoItAllFileToolsIntegration();
        services.AddRuntimeHostPlatformComposition(configuration, environment);
        return services;
    }

    private static bool IsValid(Action validate)
    {
        try
        {
            validate();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static IServiceCollection AddRuntimeHostPlatformComposition(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var profileOptions =
            configuration.GetSection(RuntimeHostProfileOptions.SectionName).Get<RuntimeHostProfileOptions>() ??
            new RuntimeHostProfileOptions();
        var secretVaultOptions =
            configuration.GetSection(SecretVaultOptions.SectionName).Get<SecretVaultOptions>() ??
            new SecretVaultOptions();
        ResolvedRuntimeHostProfile profile = RuntimeHostProfileResolver.Resolve(
            profileOptions,
            secretVaultOptions.UsageProfile,
            RuntimeHostFacts.DetectCurrent(environment.IsDevelopment()));

        services
            .AddOptions<RuntimeHostProfileOptions>()
            .Bind(configuration.GetSection(RuntimeHostProfileOptions.SectionName))
            .Validate(options => Enum.IsDefined(options.Profile), "Runtime host profile is invalid.")
            .ValidateOnStart();
        services.AddSingleton<ResolvedRuntimeHostProfile>(profile);
        services.PostConfigure<FileToolsDesktopLaunchOptions>(options =>
            options.HostProfileAllowsDesktop = profile.IsInteractive);
        services.AddSingleton<IRuntimeDeploymentSupportProvider, EmbeddedRuntimeDeploymentSupportProvider>();
        services.AddSingleton<IHostCapabilitySnapshotProvider, HostCapabilitySnapshotService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IProcessHostCapabilitySource,
            ApplicationProcessHostCapabilitySource>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IProcessHostProfileSource,
            ApplicationProcessHostProfileSource>());
        services.AddHostedService<HostCapabilityStartupValidator>();
        services.AddHealthChecks()
            .AddCheck<HostCapabilityHealthCheck>("host-capabilities");
        return services;
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
    IDatabaseDriverRegistry driverRegistry,
    IProfileAppDbContextFactory dbContextFactory,
    EnvironmentSecretBootstrapService secretBootstrap,
    ProviderDefaultsBootstrapService providerBootstrap,
    WorkspaceDefaultsBootstrapService workspaceBootstrap,
    IEnumerable<IProviderRuntimeProfileSnapshotInitializer>
        providerRuntimeProfileSnapshotInitializers,
    ILogger<AppDatabaseBootstrapper> logger,
    IOptions<ProviderInitializationOptions> providerInitialization) : IAppDatabaseBootstrapper
{
    private static readonly Guid ManagedDeliveryUnitPartyId = Guid.Parse("10BE49B1-EF4D-4A58-B9EA-B3F7D40F31A1");
    private static readonly Guid ManagedProductOwnerPartyId = Guid.Parse("A6BBAD2B-9D18-40EA-95B5-6D73C20C3078");
    private static readonly Guid ManagedDeliveryManagerPartyId = Guid.Parse("4B4718D5-4F86-4A6A-9BE7-3ACCA7E0F2AB");
    private static readonly Guid ManagedDeliveryUnitRoleId = Guid.Parse("1A8A7BB6-10B5-4D18-A91F-00F25E045DBF");
    private static readonly Guid ManagedProductOwnerRoleId = Guid.Parse("DBF3B8E6-77D2-49D5-924A-74CA8FFFBFD3");
    private static readonly Guid ManagedDeliveryManagerRoleId = Guid.Parse("2D9DF6AC-8B49-43EA-960E-8B912A758296");
    private static readonly Guid ManagedProductOwnerProfileId = Guid.Parse("61C29FAE-C560-4C2D-993E-BE842FD635FB");
    private static readonly Guid ManagedDeliveryManagerProfileId = Guid.Parse("E0EBEC09-C37B-4F42-9FA4-1B2DDAC20572");
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

        await driverRegistry.Resolve(profile.Profile.ProviderKind)
            .EnsureDatabaseAsync(profile, cancellationToken);
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
                cancellationToken);
            logger.LogInformation(
                "Non-relational database profile {ProfileId} is ready.",
                profile.Profile.Id);
            return;
        }

        logger.LogInformation(
            "Applying EF migrations for profile {ProfileId}.",
            profile.Profile.Id);
        await ReconcilePostgreSqlMigrationBaselineIfNeededAsync(profile, dbContext, cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation(
            "Ensuring CRM/HR schema for profile {ProfileId}.",
            profile.Profile.Id);
        var crmOptions = new DbContextOptionsBuilder<CrmHrDbContext>();
        AppDbContextOptionsConfigurator.Configure(crmOptions, profile);
        await using (var crm = new CrmHrDbContext(crmOptions.Options)) {
            await CrmHrSchemaInitializer.EnsureAsync(crm, cancellationToken);
        }
        logger.LogInformation(
            "Ensuring agent provider bootstrap for profile {ProfileId}.",
            profile.Profile.Id);
        await EnsureAgentProviderBootstrapAsync(profile, cancellationToken);
        logger.LogInformation(
            "Runtime database profile {ProfileId} is ready.",
            profile.Profile.Id);
    }

    private async Task ReconcilePostgreSqlMigrationBaselineIfNeededAsync(
        ResolvedDatabaseProfile profile,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (profile.Profile.ProviderKind != DatabaseProviderKind.PostgreSql)
        {
            return;
        }

        var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var knownMigrationIds = dbContext.Database.GetMigrations().ToArray();

        if (!knownMigrationIds.Contains(PostgreSqlMigrationBaseline.CurrentMigrationId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"The configured PostgreSQL migrations assembly does not contain baseline '{PostgreSqlMigrationBaseline.CurrentMigrationId}'.");
        }

        if (appliedMigrations.Contains(PostgreSqlMigrationBaseline.CurrentMigrationId))
        {
            return;
        }

        var existingSentinelTables = new List<string>();
        foreach (var tableName in BaselineSentinelTables)
        {
            if (await PostgreSqlTableExistsAsync(dbContext, tableName, cancellationToken))
            {
                existingSentinelTables.Add(tableName);
            }
        }

        if (existingSentinelTables.Count == 0 && appliedMigrations.Count == 0)
        {
            return;
        }

        if (existingSentinelTables.Count != BaselineSentinelTables.Length)
        {
            throw new InvalidOperationException(
                $"PostgreSQL profile '{profile.Profile.DisplayName}' does not have the complete CanDoItAll baseline schema. Existing sentinel tables: {string.Join(", ", existingSentinelTables)}. Refusing to change EF migration history.");
        }

        var unexpectedMigrations = appliedMigrations
            .Except(PostgreSqlMigrationBaseline.LegacyMigrationIds, StringComparer.Ordinal)
            .Where(migrationId =>
                string.CompareOrdinal(migrationId, PostgreSqlMigrationBaseline.FirstLegacyMigrationId) >= 0)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var hasLegacyMigrationHistory =
            PostgreSqlMigrationBaseline.LegacyMigrationIds.IsSubsetOf(appliedMigrations) &&
            unexpectedMigrations.Length == 0;
        if (appliedMigrations.Count > 0 && !hasLegacyMigrationHistory)
        {
            var missingLegacyMigrations = PostgreSqlMigrationBaseline.LegacyMigrationIds
                .Except(appliedMigrations, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal);
            throw new InvalidOperationException(
                $"PostgreSQL profile '{profile.Profile.DisplayName}' has migration history that cannot be reconciled with baseline '{PostgreSqlMigrationBaseline.CurrentMigrationId}'. Missing legacy migrations: {FormatValues(missingLegacyMigrations)}. Unexpected migrations: {FormatValues(unexpectedMigrations)}.");
        }

        var baselineModel = ResolvePostgreSqlBaselineModel(dbContext);
        var missingRequirements = await FindMissingPostgreSqlBaselineRequirementsAsync(
            dbContext,
            baselineModel,
            cancellationToken);
        var baselineOwnedIndexRequirements = PostgreSqlMigrationBaseline.CustomIndexNames
            .Select(indexName => $"index {indexName}")
            .ToHashSet(StringComparer.Ordinal);
        if (missingRequirements.Count > 0 &&
            missingRequirements.All(baselineOwnedIndexRequirements.Contains))
        {
            logger.LogWarning(
                "PostgreSQL profile {ProfileId} is missing baseline-owned custom indexes {MissingCustomIndexes}. Restoring those indexes before migration-history reconciliation.",
                profile.Profile.Id,
                string.Join(", ", missingRequirements));
            await dbContext.Database.ExecuteSqlRawAsync(
                PostgreSqlMigrationBaseline.CreateCustomObjectsSql,
                cancellationToken);
            missingRequirements = await FindMissingPostgreSqlBaselineRequirementsAsync(
                dbContext,
                baselineModel,
                cancellationToken);
        }

        if (missingRequirements.Count > 0)
        {
            throw new InvalidOperationException(
                $"PostgreSQL profile '{profile.Profile.DisplayName}' does not match the squashed PostgreSQL baseline. Missing schema requirements: {string.Join(", ", missingRequirements)}. Refusing to change EF migration history.");
        }

        if (hasLegacyMigrationHistory)
        {
            logger.LogWarning(
                "PostgreSQL profile {ProfileId} has the complete pre-squash migration chain. Replacing {MigrationHistoryRowCount} pre-squash history rows with baseline {BaselineMigrationId} before applying later migrations.",
                profile.Profile.Id,
                appliedMigrations.Count,
                PostgreSqlMigrationBaseline.CurrentMigrationId);
            await ReplacePostgreSqlMigrationHistoryWithBaselineAsync(dbContext, cancellationToken);
            return;
        }

        logger.LogWarning(
            "PostgreSQL profile {ProfileId} has the complete baseline schema without EF migration history. Recording baseline {BaselineMigrationId} before applying later migrations.",
            profile.Profile.Id,
            PostgreSqlMigrationBaseline.CurrentMigrationId);
        await EnsurePostgreSqlMigrationHistoryTableAsync(dbContext, cancellationToken);
        await MarkPostgreSqlMigrationAppliedAsync(
            dbContext,
            PostgreSqlMigrationBaseline.CurrentMigrationId,
            cancellationToken);
    }

    private static string FormatValues(IEnumerable<string> values)
    {
        var materializedValues = values.ToArray();
        return materializedValues.Length == 0
            ? "none"
            : string.Join(", ", materializedValues);
    }

    private static IModel ResolvePostgreSqlBaselineModel(AppDbContext dbContext)
    {
        var migrationsAssembly = dbContext.GetService<IMigrationsAssembly>();
        if (!migrationsAssembly.Migrations.TryGetValue(
                PostgreSqlMigrationBaseline.CurrentMigrationId,
                out var migrationType))
        {
            throw new InvalidOperationException(
                $"Migration '{PostgreSqlMigrationBaseline.CurrentMigrationId}' was not found in the configured PostgreSQL migrations assembly.");
        }

        var providerName = dbContext.Database.ProviderName
            ?? throw new InvalidOperationException("The active EF Core database provider name is unavailable.");
        return migrationsAssembly.CreateMigration(migrationType, providerName).TargetModel;
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

    private static async Task<List<string>> FindMissingPostgreSqlBaselineRequirementsAsync(
        AppDbContext dbContext,
        IModel baselineModel,
        CancellationToken cancellationToken)
    {
        var missingRequirements = new List<string>();

        var expectedSchema = BuildExpectedPostgreSqlSchema(baselineModel);
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

        var expectedIndexNames = baselineModel.GetEntityTypes()
            .SelectMany(entityType => entityType.GetIndexes())
            .Select(index => index.GetDatabaseName())
            .OfType<string>()
            .Concat(PostgreSqlMigrationBaseline.CustomIndexNames)
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

    private static Dictionary<string, HashSet<string>> BuildExpectedPostgreSqlSchema(IModel baselineModel)
    {
        var expectedSchema = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var entityType in baselineModel.GetEntityTypes())
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

    private static async Task ReplacePostgreSqlMigrationHistoryWithBaselineAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             DELETE FROM "__EFMigrationsHistory";

             INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
             VALUES ({PostgreSqlMigrationBaseline.CurrentMigrationId}, {ResolveEfCoreProductVersion()});
             """,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static string ResolveEfCoreProductVersion()
    {
        var informationalVersion = typeof(DbContext).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
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
        CancellationToken cancellationToken) {
        if (!providerInitialization.Value.SeedDefaults) {
            logger.LogInformation("Default provider seeding is disabled for profile {ProfileId}; explicit provider configuration is required.", profile.Profile.Id);
            return;
        }

        var timestamp = DateTimeOffset.UtcNow;
        var secretId = await secretBootstrap.EnsureOpenAiEnvironmentSecretAsync(profile, cancellationToken);
        await using var providers = await providerBootstrap.PrepareAsync(profile, secretId, cancellationToken);
        using var participation = providers.EnterTransaction();
        var workspaceChanged = await workspaceBootstrap.EnsureAsync(profile, providers.Transactions,
            providers.NewWorkspaceDefaultProviderId, providers.MatchedProviderId,
            providers.ProviderExistsAsync, timestamp, cancellationToken);
        if (await providers.CommitAsync(workspaceChanged, cancellationToken)) {
            logger.LogInformation("Seeded OpenAI provider bootstrap for profile {ProfileId}.", profile.Profile.Id);
        }
    }

}

public sealed class DatabaseSwitchCoordinator(
    IDatabaseProfileRuntimeAccessor profileAccessor,
    IDatabaseProfileService profileService,
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
