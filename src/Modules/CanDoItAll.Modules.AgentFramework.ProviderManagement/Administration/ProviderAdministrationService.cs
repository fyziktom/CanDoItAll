using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.SharedKernel;
using CanDoItAll.Modules.Security;
using CanDoItAll.Security.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

using AgentFrameworkProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using ProviderModelTokenPriceEditorModel = CanDoItAll.AgentFramework.Models.ProviderModelTokenPriceEditorModel;
using ProviderProfilePurpose = CanDoItAll.AgentFramework.Models.ProviderProfilePurpose;
using ProviderPricingDefaults = CanDoItAll.AgentFramework.Models.ProviderPricingDefaults;
using ProviderPricingMetadata = CanDoItAll.AgentFramework.Models.ProviderPricingMetadata;
using ProviderTransportKind = CanDoItAll.AgentFramework.Models.ProviderTransportKind;

public sealed record ProviderProfileSummary(
    Guid Id,
    string Name,
    ProviderKind? LegacyProviderKind,
    string ConnectorPluginKey,
    string ConnectorDisplayName,
    string BaseUrl,
    string DefaultModel,
    bool IsEnabled,
    bool IsPrivateProvider,
    string? LastHealthStatus,
    DateTimeOffset? LastHealthCheckAtUtc);

public sealed class ProviderProfileEditorModel : CanDoItAll.AgentFramework.Models.IProviderModelPricingEditorModel
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ConnectorPluginKey { get; set; } = string.Empty;

    public string ConfigSchemaVersion { get; set; } = string.Empty;

    public Guid? ApiKeySecretId { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool SupportsStreaming { get; set; }

    public bool SupportsToolCalling { get; set; }

    public bool SupportsStructuredOutput { get; set; }

    public bool SupportsVision { get; set; }

    public ConnectorConfigState Configuration { get; set; } = new();

    public bool IsPrivateProvider { get; set; }

    public List<ProviderModelTokenPriceEditorModel> ModelPrices { get; set; } = [];
}

public sealed record ProviderModelPricingRefreshResult(
    List<ProviderModelTokenPriceEditorModel> ModelPrices,
    int DiscoveredModelCount,
    int ExplicitPriceCount,
    int ModelNameOnlyCount,
    string Message);

internal sealed class ProviderAdministrationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    SecretService secretService,
    ISecretRuntimeResolver secretRuntimeResolver,
    ProviderRegistry providerRegistry,
    IProviderHealthCheckService providerHealthCheckService,
    IActivityStream activityStream,
    IEnumerable<IProviderProfileDeletionGuard> providerProfileDeletionGuards,
    IEnumerable<IProviderProfileCommitObserver>
        providerProfileCommitObservers) :
    IProviderAdministrationService
{
    public IReadOnlyList<ConnectorPluginManifest> ListProviderManifests()
    {
        return providerRegistry.ListManifests();
    }

    public async Task<IReadOnlyList<ProviderProfileSummary>> ListProviderProfilesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profiles = await dbContext.Set<ProviderProfile>()
            .AsNoTracking()
            .OrderBy(profile => profile.Name)
            .ToListAsync(cancellationToken);
        return profiles
            .Select(profile =>
            {
                var providerPlugin = providerRegistry.Resolve(profile);
                var connectorPluginKey = providerPlugin?.Manifest.PluginKey ?? profile.ConnectorPluginKey;
                var pricingMetadata = ProviderPricingMetadata.Read(profile.ExtraSettingsJson);
                var isKnownPricingKind = TryResolveAgentFrameworkProviderKind(connectorPluginKey, out var pricingKind);
                return new ProviderProfileSummary(
                profile.Id,
                profile.Name,
                profile.ProviderKind,
                connectorPluginKey,
                providerPlugin?.Manifest.DisplayName ?? profile.ConnectorPluginKey,
                profile.BaseUrl,
                profile.DefaultModel,
                profile.IsEnabled,
                isKnownPricingKind
                    ? ProviderPricingDefaults.ResolveIsPrivateProvider(pricingKind, pricingMetadata.IsPrivateProvider)
                    : pricingMetadata.IsPrivateProvider ?? false,
                profile.LastHealthStatus,
                profile.LastHealthCheckAtUtc);
            })
            .ToList();
    }

    public async Task<ProviderProfileEditorModel> GetProviderAsync(Guid? id, CancellationToken cancellationToken = default)
    {
        if (!id.HasValue)
        {
            return NewProvider();
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var provider = await dbContext.Set<ProviderProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id.Value, cancellationToken);
        if (provider is null)
        {
            return NewProvider();
        }

        var connectorPluginKey = providerRegistry.Resolve(provider)?.Manifest.PluginKey ?? provider.ConnectorPluginKey;
        var isKnownPricingKind = TryResolveAgentFrameworkProviderKind(connectorPluginKey, out var pricingKind);
        var pricingMetadata = ProviderPricingMetadata.Read(provider.ExtraSettingsJson);
        var modelPrices = isKnownPricingKind
            ? ProviderPricingDefaults.NormalizeModelPrices(
                pricingKind,
                provider.DefaultModel,
                pricingMetadata.ModelPrices)
            : pricingMetadata.ModelPrices;

        return new ProviderProfileEditorModel
        {
            Id = provider.Id,
            Name = provider.Name,
            ConnectorPluginKey = connectorPluginKey,
            ConfigSchemaVersion = provider.ConfigSchemaVersion,
            ApiKeySecretId = provider.ApiKeySecretId,
            IsEnabled = provider.IsEnabled,
            SupportsStreaming = provider.SupportsStreaming,
            SupportsToolCalling = provider.SupportsToolCalling,
            SupportsStructuredOutput = provider.SupportsStructuredOutput,
            SupportsVision = provider.SupportsVision,
            Configuration = BuildProviderConfiguration(provider),
            IsPrivateProvider = isKnownPricingKind
                ? ProviderPricingDefaults.ResolveIsPrivateProvider(
                    pricingKind,
                    pricingMetadata.IsPrivateProvider)
                : pricingMetadata.IsPrivateProvider ?? false,
            ModelPrices = ProviderPricingDefaults.ToEditorModels(modelPrices)
        };
    }

    public async Task<Result<Guid>> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (SharedProviderProfileOwnershipPolicy.IsSourceManagedConnector(
                model.ConnectorPluginKey))
        {
            return Result<Guid>.Failure(Error.Validation(
                SharedProviderProfileOwnershipPolicy.GenericSaveRejectionMessage));
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Result<Guid>.Failure(Error.Validation("Provider profile name is required."));
        }

        var configuredBaseUrl = model.Configuration.GetText(ProviderConnectorFieldKeys.BaseUrl);
        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return Result<Guid>.Failure(Error.Validation("Provider base URL is required."));
        }

        var providerResolutionError = TryResolveProviderPlugin(
            model,
            out var providerPlugin,
            out var providerManifest,
            out var configSchemaVersion);
        if (providerResolutionError is not null)
        {
            return Result<Guid>.Failure(providerResolutionError);
        }

        var secretRecordId = model.ApiKeySecretId;
        var requiresSecret = providerManifest.SecretRequirements.Any(requirement => requirement.IsRequired);
        if (requiresSecret &&
            (!secretRecordId.HasValue ||
                secretRecordId.Value == Guid.Empty))
        {
            return Result<Guid>.Failure(Error.Validation(
                $"{providerManifest.DisplayName} requires a secret reference."));
        }

        var configuredTimeoutSeconds = model.Configuration.GetNumber(ProviderConnectorFieldKeys.TimeoutSeconds) ?? 45;
        if (configuredTimeoutSeconds < 5)
        {
            return Result<Guid>.Failure(Error.Validation("Provider timeout must be at least five seconds."));
        }

        var defaultModel = ResolveDefaultModel(model, providerPlugin.Manifest.PluginKey);
        var hasPricingKind = TryResolveAgentFrameworkProviderKind(providerPlugin.Manifest.PluginKey, out var pricingKind);
        var editorModelPrices = ProviderPricingDefaults.FromEditorModels(model.ModelPrices);
        var modelPrices = hasPricingKind
            ? ProviderPricingDefaults.NormalizeModelPrices(
                pricingKind,
                defaultModel,
                editorModelPrices)
            : editorModelPrices;
        if (!ProviderPricingDefaults.TryValidateModelPrices(modelPrices, out var pricingValidationMessage))
        {
            return Result<Guid>.Failure(Error.Validation(pricingValidationMessage));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var secretMutationScope =
            await ProviderProfileSecretMutationScope.BeginAsync(
                dbContext,
                model.Id,
                secretRecordId,
                cancellationToken);
        var entity = secretMutationScope.Profile;
        var isNewProfile = entity is null;
        if (SharedProviderProfileOwnershipPolicy.IsSourceManagedConnector(
                entity?.ConnectorPluginKey))
        {
            return Result<Guid>.Failure(Error.Validation(
                SharedProviderProfileOwnershipPolicy.GenericSaveRejectionMessage));
        }

        if (secretRecordId is { } targetSecretRecordId)
        {
            var secretExists = targetSecretRecordId != Guid.Empty &&
                await dbContext.Set<SecretRecord>()
                    .AsNoTracking()
                    .AnyAsync(
                        secret => secret.Id == targetSecretRecordId,
                        cancellationToken);
            if (!secretExists)
            {
                return Result<Guid>.Failure(Error.Validation(
                    "The selected provider secret reference does not exist."));
            }
        }

        if (entity is null)
        {
            entity = new ProviderProfile();
            await dbContext.Set<ProviderProfile>().AddAsync(entity, cancellationToken);
        }

        if (!TryResolveSharedProviderPublicationMetadata(
                entity,
                providerPlugin.Manifest.PluginKey,
                out var publicationMetadata))
        {
            return Result<Guid>.Failure(Error.Validation(
                $"Provider connector '{providerPlugin.Manifest.PluginKey}' does not define a supported publication classification."));
        }

        string extraSettingsJson;
        try
        {
            var pricedConfiguration = ProviderPricingMetadata.Write(
                model.Configuration.ToJson(),
                hasPricingKind
                    ? ProviderPricingDefaults.ResolveIsPrivateProvider(
                        pricingKind,
                        model.IsPrivateProvider)
                    : model.IsPrivateProvider,
                modelPrices);
            extraSettingsJson =
                SharedProviderProfilePublicationMetadataWriter.Write(
                    pricedConfiguration,
                    publicationMetadata.ProviderKind,
                    publicationMetadata.Transport,
                    publicationMetadata.Purpose,
                    defaultModel,
                    publicationMetadata.Models);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            return Result<Guid>.Failure(Error.Validation(exception.Message));
        }

        entity.Name = model.Name.Trim();
        entity.ConnectorPluginKey = providerPlugin.Manifest.PluginKey;
        entity.ProviderKind = providerPlugin.LegacyProviderKind;
        entity.ConfigSchemaVersion = configSchemaVersion;
        entity.BaseUrl = configuredBaseUrl.Trim().TrimEnd('/');
        entity.ApiKeySecretId = secretRecordId;
        entity.DefaultModel = defaultModel;
        entity.TimeoutSeconds = Math.Max(5, configuredTimeoutSeconds);
        entity.IsEnabled = model.IsEnabled;
        var capabilityDefaults = ProviderCapabilityDefaults.Resolve(providerPlugin.Manifest.PluginKey);
        entity.SupportsStreaming = capabilityDefaults.SupportsStreaming || model.SupportsStreaming;
        entity.SupportsToolCalling = capabilityDefaults.SupportsToolCalling || model.SupportsToolCalling;
        entity.SupportsStructuredOutput =
            capabilityDefaults.SupportsStructuredOutput &&
            (isNewProfile || model.SupportsStructuredOutput);
        entity.SupportsVision = string.Equals(providerPlugin.Manifest.PluginKey, OpenAiProviderAdapter.PluginKey, StringComparison.OrdinalIgnoreCase) &&
                                model.SupportsVision;
        entity.ExtraSettingsJson = extraSettingsJson;

        await dbContext.SaveChangesAsync(cancellationToken);
        await secretMutationScope.CommitAsync(cancellationToken);
        await secretMutationScope.DisposeAsync();

        foreach (var observer in providerProfileCommitObservers)
        {
            await observer.ProviderSavedAsync(
                entity.Id,
                CancellationToken.None);
        }

        await activityStream.RecordAsync(new ActivityWriteRequest(
            "providers",
            model.Id.HasValue ? "update" : "create",
            $"{(model.Id.HasValue ? "Updated" : "Created")} provider profile",
            $"{entity.Name} ({providerPlugin.Manifest.DisplayName})",
            ArtifactKind: "provider-profile",
            ArtifactId: entity.Id,
            Route: "/agents?tab=providers"), cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task<Result<ProviderModelPricingRefreshResult>> RefreshProviderModelPricesAsync(
        ProviderProfileEditorModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var configuredBaseUrl = model.Configuration.GetText(ProviderConnectorFieldKeys.BaseUrl);
        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return Result<ProviderModelPricingRefreshResult>.Failure(
                Error.Validation("Provider base URL is required before model pricing can be loaded."));
        }

        var providerResolutionError = TryResolveProviderPlugin(
            model,
            out var providerPlugin,
            out var providerManifest,
            out var configSchemaVersion);
        if (providerResolutionError is not null)
        {
            return Result<ProviderModelPricingRefreshResult>.Failure(providerResolutionError);
        }

        if (providerPlugin is not IProviderModelPricingSource pricingSource)
        {
            return Result<ProviderModelPricingRefreshResult>.Failure(Error.Validation(
                $"{providerManifest.DisplayName} does not support provider API pricing refresh. Add model prices manually."));
        }

        var configuredTimeoutSeconds = model.Configuration.GetNumber(ProviderConnectorFieldKeys.TimeoutSeconds) ?? 45;
        if (configuredTimeoutSeconds < 5)
        {
            return Result<ProviderModelPricingRefreshResult>.Failure(
                Error.Validation("Provider timeout must be at least five seconds."));
        }

        var defaultModel = ResolveDefaultModel(model, providerPlugin.Manifest.PluginKey);
        var profile = new ProviderProfile
        {
            Id = model.Id ?? Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(model.Name) ? providerManifest.DisplayName : model.Name.Trim(),
            ConnectorPluginKey = providerPlugin.Manifest.PluginKey,
            ProviderKind = providerPlugin.LegacyProviderKind,
            ConfigSchemaVersion = configSchemaVersion,
            BaseUrl = configuredBaseUrl.Trim().TrimEnd('/'),
            ApiKeySecretId = model.ApiKeySecretId,
            DefaultModel = defaultModel,
            TimeoutSeconds = Math.Max(5, configuredTimeoutSeconds),
            IsEnabled = model.IsEnabled,
            ExtraSettingsJson = model.Configuration.ToJson()
        };

        var secretValue = await ResolveProviderSecretValueAsync(profile, cancellationToken);
        var discoveryResult = await pricingSource.DiscoverModelPricingAsync(profile, secretValue, cancellationToken);
        if (!discoveryResult.IsSuccess)
        {
            return Result<ProviderModelPricingRefreshResult>.Failure(discoveryResult.Errors);
        }

        var pricingKind = ResolveAgentFrameworkProviderKind(providerPlugin.Manifest.PluginKey);
        var mergeResult = ProviderPricingDefaults.MergeDiscoveredModelPrices(
            pricingKind,
            defaultModel,
            ProviderPricingDefaults.FromEditorModels(model.ModelPrices),
            discoveryResult.Value!.Models);
        var refreshResult = new ProviderModelPricingRefreshResult(
            ProviderPricingDefaults.ToEditorModels(mergeResult.ModelPrices),
            mergeResult.DiscoveredModelCount,
            mergeResult.ExplicitPriceCount,
            mergeResult.ModelNameOnlyCount,
            BuildProviderPricingRefreshMessage(providerManifest.DisplayName, discoveryResult.Value.Message, mergeResult));

        return Result<ProviderModelPricingRefreshResult>.Success(refreshResult);
    }

    public async Task DeleteProviderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<ProviderProfile>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        foreach (var deletionGuard in providerProfileDeletionGuards)
        {
            await deletionGuard.EnsureCanDeleteAsync(
                dbContext,
                entity.Id,
                cancellationToken);
        }
        dbContext.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var observer in providerProfileCommitObservers)
        {
            await observer.ProviderDeletedAsync(
                entity.Id,
                CancellationToken.None);
        }

        await activityStream.RecordAsync(new ActivityWriteRequest(
            "providers",
            "delete",
            "Deleted provider profile",
            entity.Name,
            ArtifactKind: "provider-profile",
            ArtifactId: entity.Id,
            Route: "/agents?tab=providers"), cancellationToken);
    }

    public Task<ProviderHealthCheckResult> TestProviderAsync(Guid id, CancellationToken cancellationToken = default)
        => providerHealthCheckService.CheckHealthAsync(id, cancellationToken);

    public Task<IReadOnlyList<SecretListItem>> ListSecretsAsync(CancellationToken cancellationToken = default)
        => secretService.ListForPickerAsync(cancellationToken);

    private async Task<string?> ResolveProviderSecretValueAsync(
        ProviderProfile profile,
        CancellationToken cancellationToken)
    {
        if (profile.ApiKeySecretId is not { } secretId)
        {
            return null;
        }

        return await secretRuntimeResolver.ResolveValueAsync(
            new SecretRuntimeRequest(
                secretId,
                SecretRuntimePurposes.AgentProviderApiKey,
                [secretId],
                ConsumerType: SecretRuntimeConsumerTypes.ProviderProfile,
                ConsumerId: SecretRuntimeConsumerIds.ProviderProfile(profile.Id)),
            cancellationToken);
    }

    private static ProviderProfileEditorModel NewProvider() => new()
    {
        ConnectorPluginKey = OpenAiProviderAdapter.PluginKey,
        ConfigSchemaVersion = "1.0",
        Configuration = new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProviderConnectorFieldKeys.BaseUrl] = "https://api.openai.com/v1/models",
            [ProviderConnectorFieldKeys.DefaultModel] = OpenAiProviderAdapter.DefaultModel,
            [ProviderConnectorFieldKeys.TimeoutSeconds] = "45"
        }),
        IsEnabled = true,
        SupportsStreaming = true,
        SupportsToolCalling = true,
        SupportsStructuredOutput = true,
        IsPrivateProvider = false,
        ModelPrices = ProviderPricingDefaults.CreateDefaultEditorModels(
            AgentFrameworkProviderKind.OpenAi,
            OpenAiProviderAdapter.DefaultModel)
    };

    private static string ResolveDefaultModel(ProviderProfileEditorModel model, string pluginKey)
    {
        var configuredModel = model.Configuration.GetText(ProviderConnectorFieldKeys.DefaultModel);
        if (!string.IsNullOrWhiteSpace(configuredModel))
        {
            return configuredModel.Trim();
        }

        return pluginKey.Trim() switch
        {
            ScenarioHarnessProviderAdapter.PluginKey => ScenarioHarnessProviderAdapter.DefaultModel,
            ProcessMockProviderAdapter.PluginKey => ProcessMockProviderAdapter.DefaultModel,
            OpenAiProviderAdapter.PluginKey => OpenAiProviderAdapter.DefaultModel,
            ComfyUiProviderAdapter.PluginKey => ComfyUiProviderAdapter.DefaultModel,
            OllamaProviderAdapter.PluginKey or OllamaRemoteProviderAdapter.PluginKey => "llama3.1",
            _ => "unknown"
        };
    }

    private Error? TryResolveProviderPlugin(
        ProviderProfileEditorModel model,
        out IProviderAdapter providerPlugin,
        out ConnectorPluginManifest manifest,
        out string configSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(model);

        providerPlugin = default!;
        manifest = default!;
        configSchemaVersion = string.Empty;

        var requestedPluginKey = model.ConnectorPluginKey?.Trim();
        if (!providerRegistry.TryResolve(requestedPluginKey, out providerPlugin))
        {
            return Error.Validation(
                string.IsNullOrWhiteSpace(requestedPluginKey)
                    ? "Select a connector plugin for the provider profile."
                    : $"No provider adapter is registered for plugin '{requestedPluginKey}'.");
        }

        manifest = providerPlugin.Manifest;
        configSchemaVersion = string.IsNullOrWhiteSpace(model.ConfigSchemaVersion)
            ? manifest.ConfigurationSchema.Version
            : model.ConfigSchemaVersion.Trim();
        if (!string.Equals(configSchemaVersion, manifest.ConfigurationSchema.Version, StringComparison.Ordinal))
        {
            return Error.Validation(
                $"Provider plugin '{manifest.PluginKey}' requires config schema version '{manifest.ConfigurationSchema.Version}', but '{configSchemaVersion}' was supplied.");
        }

        return null;
    }

    private static ConnectorConfigState BuildProviderConfiguration(ProviderProfile provider)
    {
        var configuration = ConnectorConfigState.FromJson(provider.ExtraSettingsJson);

        configuration.SetText(ProviderConnectorFieldKeys.BaseUrl, provider.BaseUrl);
        configuration.SetText(ProviderConnectorFieldKeys.DefaultModel, provider.DefaultModel);
        configuration.SetNumber(ProviderConnectorFieldKeys.TimeoutSeconds, provider.TimeoutSeconds);

        return configuration;
    }

    private static AgentFrameworkProviderKind ResolveAgentFrameworkProviderKind(string? connectorPluginKey)
    {
        return TryResolveAgentFrameworkProviderKind(connectorPluginKey, out var kind)
            ? kind
            : throw new InvalidOperationException($"No AgentFramework provider kind mapping exists for connector plugin '{connectorPluginKey}'.");
    }

    private static bool TryResolveAgentFrameworkProviderKind(string? connectorPluginKey, out AgentFrameworkProviderKind kind)
    {
        switch (connectorPluginKey?.Trim())
        {
            case ScenarioHarnessProviderAdapter.PluginKey:
            case ProcessMockProviderAdapter.PluginKey:
            case OpenAiProviderAdapter.PluginKey:
                kind = AgentFrameworkProviderKind.OpenAi;
                return true;
            case ComfyUiProviderAdapter.PluginKey:
                kind = AgentFrameworkProviderKind.ComfyUi;
                return true;
            case OllamaProviderAdapter.PluginKey:
            case OllamaRemoteProviderAdapter.PluginKey:
                kind = AgentFrameworkProviderKind.Ollama;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static bool TryResolveSharedProviderPublicationMetadata(
        ProviderProfile current,
        string connectorPluginKey,
        out SharedProviderProfilePublicationMetadata metadata)
    {
        if (string.Equals(
                current.ConnectorPluginKey,
                connectorPluginKey,
                StringComparison.Ordinal) &&
            SharedProviderProfilePublicationMetadataReader.TryRead(
                current,
                out var currentMetadata,
                out _) &&
            IsCompatiblePublicationMetadata(
                connectorPluginKey,
                currentMetadata))
        {
            metadata = currentMetadata;
            return true;
        }

        SharedProviderProfilePublicationMetadata? resolved =
            connectorPluginKey switch
            {
                OpenAiProviderAdapter.PluginKey => new(
                    AgentFrameworkProviderKind.OpenAi,
                    ProviderTransportKind.Responses,
                    ProviderProfilePurpose.Chat,
                    []),
                OllamaProviderAdapter.PluginKey or
                    OllamaRemoteProviderAdapter.PluginKey => new(
                        AgentFrameworkProviderKind.Ollama,
                        ProviderTransportKind.ChatCompletions,
                        ProviderProfilePurpose.Chat,
                        []),
                ComfyUiProviderAdapter.PluginKey => new(
                    AgentFrameworkProviderKind.ComfyUi,
                    ProviderTransportKind.ChatCompletions,
                    ProviderProfilePurpose.ImageGeneration,
                    []),
                ScenarioHarnessProviderAdapter.PluginKey or
                    ProcessMockProviderAdapter.PluginKey => new(
                        AgentFrameworkProviderKind.OpenAi,
                        ProviderTransportKind.Responses,
                        ProviderProfilePurpose.Chat,
                        []),
                _ => null
            };
        if (resolved is null)
        {
            metadata = null!;
            return false;
        }

        metadata = resolved;
        return true;
    }

    private static bool IsCompatiblePublicationMetadata(
        string connectorPluginKey,
        SharedProviderProfilePublicationMetadata metadata)
        => connectorPluginKey switch
        {
            OpenAiProviderAdapter.PluginKey =>
                metadata.ProviderKind is
                    AgentFrameworkProviderKind.OpenAi or
                    AgentFrameworkProviderKind.AzureOpenAi &&
                metadata.Purpose switch
                {
                    ProviderProfilePurpose.Chat =>
                        metadata.Transport is
                            ProviderTransportKind.Responses or
                            ProviderTransportKind.ChatCompletions,
                    ProviderProfilePurpose.ImageGeneration =>
                        metadata.Transport == ProviderTransportKind.Responses,
                    _ => false
                },
            OllamaProviderAdapter.PluginKey or
                OllamaRemoteProviderAdapter.PluginKey =>
                    metadata.ProviderKind == AgentFrameworkProviderKind.Ollama &&
                    metadata.Transport == ProviderTransportKind.ChatCompletions &&
                    metadata.Purpose == ProviderProfilePurpose.Chat,
            ComfyUiProviderAdapter.PluginKey =>
                metadata.ProviderKind == AgentFrameworkProviderKind.ComfyUi &&
                metadata.Transport == ProviderTransportKind.ChatCompletions &&
                metadata.Purpose == ProviderProfilePurpose.ImageGeneration,
            ScenarioHarnessProviderAdapter.PluginKey or
                ProcessMockProviderAdapter.PluginKey =>
                    metadata.ProviderKind == AgentFrameworkProviderKind.OpenAi &&
                    metadata.Transport == ProviderTransportKind.Responses &&
                    metadata.Purpose == ProviderProfilePurpose.Chat,
            _ => false
        };

    private static string BuildProviderPricingRefreshMessage(
        string providerDisplayName,
        string adapterMessage,
        CanDoItAll.AgentFramework.Models.ProviderModelPricingMergeResult mergeResult)
    {
        var exactPart = mergeResult.ExplicitPriceCount > 0
            ? $"{mergeResult.ExplicitPriceCount} exact price row(s)"
            : "no exact price rows";
        var modelOnlyPart = mergeResult.ModelNameOnlyCount > 0
            ? $"{mergeResult.ModelNameOnlyCount} model-name-only row(s)"
            : "no model-name-only rows";

        return $"{providerDisplayName}: {adapterMessage} Applied {exactPart} and {modelOnlyPart}; manual rows were preserved unless an exact API price matched the same model.";
    }
}
