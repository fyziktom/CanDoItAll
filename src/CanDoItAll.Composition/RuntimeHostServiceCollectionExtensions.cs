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
    ISwitchableAppDbContextFactory dbContextFactory,
    IAgentProviderCredentialResolver providerCredentialResolver,
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
    private static readonly Guid RuntimeBootstrapOpenAiProviderId = Guid.Parse("2DB76580-21A4-B156-81A7-68DC0EE7513C");
    private const string RuntimeBootstrapOpenAiProviderName = ManagedSeedProviderFallbacks.OpenAiDefaultProviderName;
    private const string RuntimeBootstrapOpenAiBaseUrl = "https://api.openai.com/v1";
    private const string RuntimeBootstrapOpenAiApiKeyEnvironmentVariable = "OPENAI_API_KEY";
    private const string RuntimeBootstrapOpenAiModel = ManagedSeedProviderFallbacks.OpenAiDefaultModel;
    private const string RuntimeBootstrapProviderSchemaVersion = "1.0";
    private const int RuntimeBootstrapOpenAiTimeoutSeconds = 600;
    private static readonly Guid DefaultOpenAiApiKeySecretId = Guid.Parse("86F781F1-1E76-4B45-9F1A-42B8CF13D8C7");
    private const string DefaultOpenAiApiKeySecretName = "OpenAI API key";

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
    IDatabaseRuntimeState runtimeState,
    IAppDatabaseBootstrapper bootstrapper,
    IOptions<DatabaseOptions> databaseOptions,
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
            var snapshot = runtimeState.GetSnapshot();
            return Result<DatabaseSwitchResult>.Success(new DatabaseSwitchResult(
                currentProfile.Profile.Id,
                currentProfile.Profile.Id,
                snapshot.Generation,
                Environment.ProcessId)
            {
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
                "Persisted database profile activation from {PreviousProfileId} to {CurrentProfileId}. RestartRequired={RestartRequired}. MaintenanceHotSwitchEnabled={MaintenanceHotSwitchEnabled}.",
                currentProfile.Profile.Id,
                targetProfile.Profile.Id,
                true,
                databaseOptions.Value.EnableMaintenanceHotSwitch);

            var snapshot = runtimeState.GetSnapshot();
            return Result<DatabaseSwitchResult>.Success(new DatabaseSwitchResult(
                currentProfile.Profile.Id,
                targetProfile.Profile.Id,
                snapshot.Generation,
                Environment.ProcessId)
            {
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
