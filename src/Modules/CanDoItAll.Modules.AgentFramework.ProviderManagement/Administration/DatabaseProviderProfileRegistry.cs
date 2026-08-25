using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

using AgentFrameworkProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using AgentFrameworkProviderProfileEditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;

public enum ProviderCatalogProjectionOperationKind
{
    Upsert,
    Delete
}

public sealed class ProviderCatalogProjectionException(
    Guid providerId,
    ProviderCatalogProjectionOperationKind operationKind,
    string repairAction,
    Exception innerException) :
    InvalidOperationException(
        $"Canonical provider '{providerId:D}' committed successfully, but catalog projection '{operationKind}' failed. {repairAction}",
        innerException)
{
    public Guid ProviderId { get; } = providerId;

    public ProviderCatalogProjectionOperationKind OperationKind { get; } =
        operationKind;

    public bool CanonicalCommitSucceeded { get; } = true;

    public string RepairAction { get; } = repairAction;
}

internal sealed class DatabaseProviderProfileRegistry(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ISandboxWorkspaceStore store,
    ProviderRegistry providerRegistry,
    IProviderProfileService providerProfileService,
    ProviderProfileMapper providerMapper,
    IProviderRuntimeProfileSnapshotLoader runtimeProfileLoader,
    IEnumerable<IProviderProfileDeletionGuard> providerProfileDeletionGuards,
    IEnumerable<IProviderProfileCommitObserver>
        providerProfileCommitObservers,
    ILogger<DatabaseProviderProfileRegistry> logger) :
    IProviderProfileRegistry
{
    public async Task<IReadOnlyList<AgentFrameworkProviderProfile>> ListProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        var mappedProviders = await LoadDatabaseProvidersAsync(cancellationToken);
        return mappedProviders
            .Where(item =>
                item.Id != ProviderProfileWellKnownIds.RuntimeFallbackOllama)
            .Append(providerMapper.CreateRuntimeFallback())
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<AgentFrameworkProviderProfile?> GetProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        if (providerId ==
            ProviderProfileWellKnownIds.RuntimeFallbackOllama)
        {
            return providerMapper.CreateRuntimeFallback();
        }

        var provider = await LoadDatabaseProviderAsync(providerId, cancellationToken);
        return provider;
    }

    public async Task<AgentFrameworkProviderProfileEditorModel> GetProviderEditorAsync(
        Guid? providerId = null,
        CancellationToken cancellationToken = default)
    {
        if (!providerId.HasValue)
        {
            return providerProfileService.CreateEditor();
        }

        var provider = await GetProviderAsync(providerId.Value, cancellationToken)
            ?? throw new InvalidOperationException("Provider profile was not found.");
        return providerProfileService.CreateEditor(provider);
    }

    public async Task<Guid> SaveProviderAsync(
        AgentFrameworkProviderProfileEditorModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        var capabilityProfile = providerProfileService.CreateProfile(model);
        var secretRecordId = ResolveSecretRecordIdForSave(model);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var secretMutationScope =
            await ProviderProfileSecretMutationScope.BeginAsync(
                dbContext,
                model.Id,
                secretRecordId,
                cancellationToken);
        var current = secretMutationScope.Profile;
        if (SharedProviderProfileOwnershipPolicy.IsSourceManagedConnector(
                current?.ConnectorPluginKey))
        {
            throw new ProviderProfileValidationException(
                SharedProviderProfileOwnershipPolicy.GenericSaveRejectionMessage);
        }

        var currentProfile = current is null
            ? null
            : providerMapper.Map(current);
        if (currentProfile is not null)
        {
            capabilityProfile = providerProfileService.CreateProfile(
                model,
                currentProfile);
        }

        var connectorPluginKey = ResolveConnectorPluginKeyForSave(model, current);
        if (SharedProviderProfileOwnershipPolicy.IsSourceManagedConnector(
                connectorPluginKey))
        {
            throw new ProviderProfileValidationException(
                SharedProviderProfileOwnershipPolicy.GenericSaveRejectionMessage);
        }

        if (!providerRegistry.TryResolve(connectorPluginKey, out var providerAdapter))
        {
            throw new ProviderProfileValidationException(
                $"No workspace provider adapter is registered for plugin '{connectorPluginKey}'.");
        }

        ValidateConnectorBaseUrl(
            capabilityProfile.BaseUrl,
            providerAdapter.Manifest.PluginKey);

        var configSchemaVersion = ResolveConfigSchemaVersionForSave(
            model,
            current,
            providerAdapter.Manifest.ConfigurationSchema.Version);
        if (!string.Equals(configSchemaVersion, providerAdapter.Manifest.ConfigurationSchema.Version, StringComparison.Ordinal))
        {
            throw new ProviderProfileValidationException(
                $"Provider plugin '{providerAdapter.Manifest.PluginKey}' requires schema '{providerAdapter.Manifest.ConfigurationSchema.Version}', but '{configSchemaVersion}' was supplied.");
        }

        if (providerAdapter.Manifest.SecretRequirements.Any(item => item.IsRequired) &&
            (!secretRecordId.HasValue || secretRecordId.Value == Guid.Empty))
        {
            throw new ProviderProfileValidationException(
                $"{providerAdapter.Manifest.DisplayName} requires an explicit secret record reference.");
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
                throw new ProviderProfileValidationException(
                    "The selected provider secret reference does not exist.");
            }
        }

        var timeoutSeconds = ResolveTimeoutSecondsForSave(
            model,
            current?.TimeoutSeconds ?? 45);
        var configuredThinkingEffortCapabilities =
            ResolveThinkingEffortCapabilitiesForSave(model);
        var normalizedEditor = providerProfileService.CreateEditor(capabilityProfile);
        normalizedEditor.ModelThinkingEffortCapabilities =
            configuredThinkingEffortCapabilities.ToList();
        capabilityProfile = providerProfileService.CreateProfile(
            normalizedEditor,
            currentProfile);
        var defaultModel = string.IsNullOrWhiteSpace(capabilityProfile.DefaultModel)
            ? ProviderProfileMapper.ResolveDefaultModel(
                providerAdapter.Manifest.PluginKey)
            : capabilityProfile.DefaultModel;
        capabilityProfile = capabilityProfile with
        {
            DefaultModel = defaultModel,
            ModelPrices = ProviderPricingDefaults.NormalizeModelPrices(
                capabilityProfile.Kind,
                defaultModel,
                capabilityProfile.ModelPrices)
        };
        var featureMatrix = providerProfileService.ResolveFeatureMatrix(capabilityProfile);
        var entity = current ?? new ProviderProfile
        {
            Id = capabilityProfile.Id
        };
        if (current is null)
        {
            dbContext.Set<ProviderProfile>().Add(entity);
        }

        entity.Name = capabilityProfile.Name;
        entity.ProviderKind = providerAdapter.LegacyProviderKind;
        entity.ConnectorPluginKey = providerAdapter.Manifest.PluginKey;
        entity.ConfigSchemaVersion = configSchemaVersion;
        entity.BaseUrl = capabilityProfile.BaseUrl;
        entity.ApiKeySecretId = secretRecordId;
        entity.DefaultModel = capabilityProfile.DefaultModel;
        entity.TimeoutSeconds = timeoutSeconds;
        entity.IsEnabled = capabilityProfile.IsEnabled;
        entity.SupportsStreaming = capabilityProfile.SupportsStreaming;
        entity.SupportsToolCalling = capabilityProfile.SupportsTools;
        entity.SupportsStructuredOutput = featureMatrix.SupportsStructuredOutput;
        entity.SupportsVision = featureMatrix.SupportsVision;
        entity.ExtraSettingsJson = ProviderPricingMetadata.Write(
            ProviderMetadata.BuildExtraSettingsJson(
                capabilityProfile.ConfigurationJson,
                providerAdapter.Manifest.PluginKey,
                configSchemaVersion,
                secretRecordId,
                timeoutSeconds,
                capabilityProfile.Kind,
                capabilityProfile.Transport,
                capabilityProfile.Purpose,
                capabilityProfile.DefaultModel,
                capabilityProfile.ModelThinkingEffortCapabilities,
                capabilityProfile.Tags,
                capabilityProfile.SuggestedModels),
            capabilityProfile.IsPrivateProvider,
            capabilityProfile.ModelPrices);

        await dbContext.SaveChangesAsync(cancellationToken);
        await secretMutationScope.CommitAsync(cancellationToken);
        await secretMutationScope.DisposeAsync();

        await NotifyProviderSavedAsync(entity.Id);
        await ProjectCatalogAsync(
            entity.Id,
            ProviderCatalogProjectionOperationKind.Upsert,
            projectionCancellationToken =>
                UpsertCatalogProvidersAsync(
                    [providerMapper.Map(entity)],
                    projectionCancellationToken));

        return entity.Id;
    }

    public async Task DeleteProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var provider = await dbContext.Set<ProviderProfile>()
            .SingleOrDefaultAsync(item => item.Id == providerId, cancellationToken);
        if (provider is not null)
        {
            foreach (var deletionGuard in providerProfileDeletionGuards)
            {
                await deletionGuard.EnsureCanDeleteAsync(
                    dbContext,
                    provider.Id,
                    cancellationToken);
            }
            dbContext.Remove(provider);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await NotifyProviderDeletedAsync(providerId);
        await ProjectCatalogAsync(
            providerId,
            ProviderCatalogProjectionOperationKind.Delete,
            projectionCancellationToken =>
                store.UpdateCatalogAsync(catalog => catalog with
                {
                    Providers = catalog.Providers
                        .Where(item => item.Id != providerId)
                        .ToList()
                }, projectionCancellationToken));
    }

    public async Task<AgentFrameworkProviderProfile> UpdateProviderAsync(
        Guid providerId,
        Func<AgentFrameworkProviderProfile, AgentFrameworkProviderProfile> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var current = await GetProviderAsync(providerId, cancellationToken)
            ?? throw new InvalidOperationException("Provider profile was not found.");
        var updated = update(current);
        var editor = providerProfileService.CreateEditor(updated);
        editor.ConfigurationJson =
            ProviderMetadata.WriteThinkingEffortCapabilities(
                editor.ConfigurationJson,
                updated.ModelThinkingEffortCapabilities);
        await SaveProviderAsync(editor, cancellationToken);
        return (await GetProviderAsync(providerId, cancellationToken))
            ?? throw new InvalidOperationException("Provider profile was not found after update.");
    }

    private async Task UpsertCatalogProvidersAsync(
        IReadOnlyList<AgentFrameworkProviderProfile> providers,
        CancellationToken cancellationToken)
    {
        if (providers.Count == 0)
        {
            return;
        }

        await store.UpdateCatalogAsync(catalog =>
        {
            var providerIds = providers
                .Select(item => item.Id)
                .ToHashSet();

            return catalog with
            {
                Providers = catalog.Providers
                    .Where(item => !providerIds.Contains(item.Id))
                    .Concat(providers)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }, cancellationToken);
    }

    private async Task NotifyProviderSavedAsync(Guid providerId)
    {
        foreach (var observer in providerProfileCommitObservers)
        {
            await observer.ProviderSavedAsync(
                providerId,
                CancellationToken.None);
        }
    }

    private async Task NotifyProviderDeletedAsync(Guid providerId)
    {
        foreach (var observer in providerProfileCommitObservers)
        {
            await observer.ProviderDeletedAsync(
                providerId,
                CancellationToken.None);
        }
    }

    private async Task ProjectCatalogAsync(
        Guid providerId,
        ProviderCatalogProjectionOperationKind operationKind,
        Func<CancellationToken, Task> projection)
    {
        try
        {
            await projection(CancellationToken.None);
        }
        catch (Exception exception)
        {
            var repairAction = operationKind switch
            {
                ProviderCatalogProjectionOperationKind.Upsert =>
                    $"Set the provider editor Id to '{providerId:D}' and retry SaveProviderAsync to upsert the catalog projection.",
                ProviderCatalogProjectionOperationKind.Delete =>
                    $"Retry DeleteProviderAsync for provider '{providerId:D}' to remove the catalog projection.",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(operationKind),
                    operationKind,
                    "Unsupported provider catalog projection operation.")
            };
            var projectionException =
                new ProviderCatalogProjectionException(
                    providerId,
                    operationKind,
                    repairAction,
                    exception);
            logger.LogError(
                projectionException,
                "Provider catalog projection failed after the canonical database commit. ProviderId={ProviderId} OperationKind={OperationKind} CanonicalCommitSucceeded={CanonicalCommitSucceeded} RepairAction={RepairAction}",
                projectionException.ProviderId,
                projectionException.OperationKind,
                projectionException.CanonicalCommitSucceeded,
                projectionException.RepairAction);
            throw projectionException;
        }
    }

    private async Task<IReadOnlyList<AgentFrameworkProviderProfile>>
        LoadDatabaseProvidersAsync(
        CancellationToken cancellationToken)
    {
        var providers = await runtimeProfileLoader
            .LoadAllAsync(cancellationToken);
        return providers
            .Select(item => item.Profile)
            .ToArray();
    }

    private async Task<AgentFrameworkProviderProfile?>
        LoadDatabaseProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken)
    {
        var provider = await runtimeProfileLoader.LoadAsync(
            providerId,
            cancellationToken);
        return provider?.Profile;
    }

    private static void ValidateConnectorBaseUrl(
        string baseUrl,
        string connectorPluginKey)
    {
        if (string.Equals(
                connectorPluginKey,
                ScenarioHarnessProviderAdapter.PluginKey,
                StringComparison.Ordinal))
        {
            if (string.Equals(
                    baseUrl,
                    ScenarioHarnessProviderAdapter.BaseUrl,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new ProviderProfileValidationException(
                "The scenario-harness connector requires its canonical scenario endpoint.");
        }

        if (string.Equals(
                connectorPluginKey,
                ProcessMockProviderAdapter.PluginKey,
                StringComparison.Ordinal))
        {
            if (string.Equals(
                    baseUrl,
                    ProcessMockProviderAdapter.BaseUrl,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new ProviderProfileValidationException(
                "The process-mock connector requires its canonical process endpoint.");
        }

        var endpoint = new Uri(baseUrl, UriKind.Absolute);
        if (string.Equals(
                endpoint.Scheme,
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                endpoint.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new ProviderProfileValidationException(
            $"Provider connector '{connectorPluginKey}' requires an HTTP or HTTPS base URL.");
    }

    private static string ResolveConnectorPluginKeyForSave(
        AgentFrameworkProviderProfileEditorModel model,
        ProviderProfile? current)
    {
        try
        {
            return ProviderMetadata.ResolveConnectorPluginKey(
                model,
                current);
        }
        catch (InvalidOperationException)
        {
            throw new ProviderProfileValidationException(
                "Provider connector metadata is invalid.");
        }
    }

    private static string ResolveConfigSchemaVersionForSave(
        AgentFrameworkProviderProfileEditorModel model,
        ProviderProfile? current,
        string defaultVersion)
    {
        try
        {
            return ProviderMetadata.ResolveConfigSchemaVersion(
                model,
                current,
                defaultVersion);
        }
        catch (InvalidOperationException)
        {
            throw new ProviderProfileValidationException(
                "Provider configuration schema metadata is invalid.");
        }
    }

    private static Guid? ResolveSecretRecordIdForSave(
        AgentFrameworkProviderProfileEditorModel model)
    {
        try
        {
            return ProviderMetadata.ResolveSecretRecordId(model);
        }
        catch (InvalidOperationException)
        {
            throw new ProviderProfileValidationException(
                "Provider secret-reference metadata is invalid.");
        }
    }

    private static int ResolveTimeoutSecondsForSave(
        AgentFrameworkProviderProfileEditorModel model,
        int fallbackValue)
    {
        try
        {
            return ProviderMetadata.ResolveTimeoutSeconds(
                model,
                fallbackValue);
        }
        catch (InvalidOperationException)
        {
            throw new ProviderProfileValidationException(
                "Provider timeout metadata is invalid.");
        }
    }

    private static IReadOnlyList<ProviderModelThinkingEffortCapability>
        ResolveThinkingEffortCapabilitiesForSave(
            AgentFrameworkProviderProfileEditorModel model)
    {
        try
        {
            return ProviderMetadata.HasThinkingEffortCapabilities(
                    model.ConfigurationJson)
                ? ProviderMetadata.ReadThinkingEffortCapabilities(
                    model.ConfigurationJson)
                : model.ModelThinkingEffortCapabilities ?? [];
        }
        catch (InvalidOperationException)
        {
            throw new ProviderProfileValidationException(
                "Provider thinking-effort capability metadata is invalid.");
        }
    }
}
