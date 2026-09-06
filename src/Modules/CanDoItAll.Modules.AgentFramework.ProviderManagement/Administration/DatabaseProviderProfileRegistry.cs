using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
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
    ProviderMutationCommittedException(
        new(providerId, operationKind),
        "The provider change is saved, but its catalog projection needs reconciliation.",
        innerException) {
    public string RepairAction { get; } = repairAction;
}

internal sealed class DatabaseProviderProfileRegistry(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ISandboxWorkspaceStore store,
    ProviderAdministrationConnectorCatalog providerConnectorCatalog,
    IProviderProfileService providerProfileService,
    ProviderProfileMapper providerMapper,
    IProviderRuntimeProfileSnapshotLoader runtimeProfileLoader,
    IEnumerable<IProviderProfileDeletionGuard> providerProfileDeletionGuards,
    IEnumerable<IProviderProfileCommitObserver>
        providerProfileCommitObservers,
    ILogger<DatabaseProviderProfileRegistry> logger) :
    IProviderProfileRegistry, IProviderCatalogReconciliation
{
    public Task<IReadOnlyList<AgentFrameworkProviderProfile>> ListProvidersAsync(
        CancellationToken cancellationToken = default)
        => LoadDatabaseProvidersAsync(cancellationToken);

    public Task<AgentFrameworkProviderProfile?> GetProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
        => LoadDatabaseProviderAsync(providerId, cancellationToken);

    public async Task<AgentFrameworkProviderProfileEditorModel> GetProviderEditorAsync(
        Guid? providerId = null,
        CancellationToken cancellationToken = default)
    {
        if (!providerId.HasValue)
        {
            return providerProfileService.CreateEditor();
        }

        var provider = await runtimeProfileLoader.LoadAsync(providerId.Value, cancellationToken)
            ?? throw new KeyNotFoundException("Provider profile was not found.");
        if (SharedProviderProfileOwnershipPolicy.IsSourceManagedConnector(provider.Profile.ConnectorPluginKey)) {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            if (await db.Set<SharedProviderImport>().AsNoTracking().AnyAsync(
                import => import.ProviderProfileId == providerId &&
                    import.SelectionState == SharedProviderSelectionState.Retired, cancellationToken)) {
                throw new ProviderRuntimeProfileUnavailableException(providerId.Value);
            }
        }
        var editor = providerProfileService.CreateEditor(provider.Profile);
        editor.ExpectedConcurrencyToken = provider.ConfigurationRevision?.Value;
        return editor;
    }

    public Task<Guid> SaveProviderAsync(
        AgentFrameworkProviderProfileEditorModel model,
        CancellationToken cancellationToken = default) => SaveProviderCoreAsync(model, null, cancellationToken);

    private async Task<Guid> SaveProviderCoreAsync(
        AgentFrameworkProviderProfileEditorModel model,
        ProviderDiagnosticState? diagnostic,
        CancellationToken cancellationToken) {

        ArgumentNullException.ThrowIfNull(model);
        cancellationToken.ThrowIfCancellationRequested();

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

        if (model.ExpectedConcurrencyToken is { } expectedToken &&
            (expectedToken == Guid.Empty ? current is not null : current is null || current.ConcurrencyToken != expectedToken)) {
            throw new ProviderProfileConcurrencyException(model.Id ?? Guid.Empty);
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

        if (!providerConnectorCatalog.TryResolve(connectorPluginKey, out var providerConnector))
        {
            throw new ProviderProfileValidationException(
                $"No provider administration connector is registered for plugin '{connectorPluginKey}'.");
        }

        ValidateConnectorBaseUrl(
            capabilityProfile.BaseUrl,
            providerConnector.Manifest.PluginKey);

        var configSchemaVersion = ResolveConfigSchemaVersionForSave(
            model,
            current,
            providerConnector.Manifest.ConfigurationSchema.Version);
        if (!string.Equals(configSchemaVersion, providerConnector.Manifest.ConfigurationSchema.Version, StringComparison.Ordinal))
        {
            throw new ProviderProfileValidationException(
                $"Provider plugin '{providerConnector.Manifest.PluginKey}' requires schema '{providerConnector.Manifest.ConfigurationSchema.Version}', but '{configSchemaVersion}' was supplied.");
        }

        if (providerConnector.Manifest.SecretRequirements.Any(item => item.IsRequired) &&
            (!secretRecordId.HasValue || secretRecordId.Value == Guid.Empty))
        {
            throw new ProviderProfileValidationException(
                $"{providerConnector.Manifest.DisplayName} requires an explicit secret record reference.");
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
                providerConnector.Manifest.PluginKey)
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
        entity.ProviderKind = providerConnector.LegacyProviderKind;
        entity.ConnectorPluginKey = providerConnector.Manifest.PluginKey;
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
        if (diagnostic is not null) {
            entity.LastHealthCheckAtUtc = diagnostic.CheckedAtUtc;
            entity.LastHealthStatus = diagnostic.Status[..Math.Min(diagnostic.Status.Length, ProviderProfile.MaximumHealthStatusLength)];
        }
        entity.ExtraSettingsJson = ProviderPricingMetadata.Write(
            ProviderMetadata.BuildExtraSettingsJson(
                capabilityProfile.ConfigurationJson,
                providerConnector.Manifest.PluginKey,
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

        cancellationToken.ThrowIfCancellationRequested();
        var attempt = ProviderMutationAttempt.Capture(model, entity.Id,
            current is null ? ProviderMutationKind.Create : ProviderMutationKind.Update) with {
            ExpectedConcurrencyToken = current?.ConcurrencyToken
        };
        var committed = false;
        try {
            await dbContext.SaveChangesAsync(cancellationToken);
            committed = dbContext.Database.CurrentTransaction is null;
            await secretMutationScope.CommitAsync(cancellationToken);
            committed = true;
            await secretMutationScope.DisposeAsync();
            await NotifyProviderSavedAsync(entity.Id);
            await ProjectCatalogAsync(
                entity.Id,
                ProviderCatalogProjectionOperationKind.Upsert,
                token => UpsertCatalogProvidersAsync([providerMapper.Map(entity)], token));
            return entity.Id;
        } catch (ProviderMutationCommittedException) {
            throw;
        } catch (Exception exception) when (committed) {
            throw new ProviderMutationCommittedException(
                new(entity.Id, ProviderCatalogProjectionOperationKind.Upsert, entity.ConcurrencyToken),
                "The provider is saved, but a secondary update needs reconciliation.", exception);
        } catch (Exception exception) when (exception is DbUpdateConcurrencyException ||
            SerializableMutationScope.IsConflict(exception)) {
            throw new ProviderProfileConcurrencyException(entity.Id, exception);
        } catch (Exception exception) {
            throw new ProviderMutationUnconfirmedException(attempt with {
                IntendedConcurrencyToken = entity.ConcurrencyToken == Guid.Empty ? null : entity.ConcurrencyToken
            }, exception);
        }
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
            cancellationToken.ThrowIfCancellationRequested();
            try {
                await dbContext.SaveChangesAsync(cancellationToken);
            } catch (DbUpdateConcurrencyException exception) {
                throw new ProviderProfileConcurrencyException(providerId, exception);
            } catch (Exception exception) {
                throw new ProviderMutationUnconfirmedException(
                    new(Guid.NewGuid(), providerId, ProviderMutationKind.Delete, provider.ConcurrencyToken), exception);
            }
        }

        try {
            await NotifyProviderDeletedAsync(providerId);
        } catch (Exception exception) {
            throw new ProviderMutationCommittedException(
                new(providerId, ProviderCatalogProjectionOperationKind.Delete),
                "The provider is deleted, but a secondary update needs reconciliation.", exception);
        }
        await ProjectCatalogAsync(
            providerId,
            ProviderCatalogProjectionOperationKind.Delete,
            projectionCancellationToken =>
                store.UpdateCatalogAsync(catalog => catalog with
                {
                    Providers = catalog.Providers
                        .Where(item => item.Id != providerId)
                        .ToList(),
                    Agents = catalog.Agents
                        .Select(agent => agent.ProviderProfileId == providerId
                            ? agent with { ProviderProfileId = null }
                            : agent)
                        .ToList()
                }, projectionCancellationToken));
    }

    public async Task<AgentFrameworkProviderProfile> UpdateProviderAsync(
        Guid providerId,
        Func<AgentFrameworkProviderProfile, AgentFrameworkProviderProfile> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var snapshot = await runtimeProfileLoader.LoadAsync(providerId, cancellationToken)
            ?? throw new InvalidOperationException("Provider profile was not found.");
        var updated = update(snapshot.Profile);
        var editor = providerProfileService.CreateEditor(updated);
        editor.ExpectedConcurrencyToken = snapshot.ConfigurationRevision?.Value;
        editor.ConfigurationJson =
            ProviderMetadata.WriteThinkingEffortCapabilities(
                editor.ConfigurationJson,
                updated.ModelThinkingEffortCapabilities);
        await SaveProviderCoreAsync(editor, new(updated.HealthStatus, updated.LastCheckedAtUtc), cancellationToken);
        try {
            return await GetProviderAsync(providerId, cancellationToken)
                ?? throw new KeyNotFoundException("Provider profile was not found after update.");
        } catch (Exception exception) {
            throw new ProviderMutationCommittedException(
                new(providerId, ProviderCatalogProjectionOperationKind.Upsert),
                "The provider update is saved, but its refreshed state is unavailable.", exception);
        }
    }

    private sealed record ProviderDiagnosticState(string Status, DateTimeOffset? CheckedAtUtc);

    public async Task ReconcileAsync(Guid providerId, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        var canonical = await GetProviderAsync(providerId, cancellationToken);
        if (canonical is not null) {
            await NotifyProviderSavedAsync(providerId);
        } else {
            await NotifyProviderDeletedAsync(providerId);
        }
        await store.UpdateCatalogAsync(catalog => catalog with {
            Providers = catalog.Providers.Where(item => item.Id != providerId)
                .Concat(canonical is null ? [] : new[] { canonical }).ToArray(),
            Agents = canonical is null
                ? catalog.Agents.Select(agent => agent.ProviderProfileId == providerId
                    ? agent with { ProviderProfileId = null } : agent).ToArray()
                : catalog.Agents
        }, cancellationToken);
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
                    $"Call {nameof(IProviderCatalogReconciliation.ReconcileAsync)} for provider {providerId:D} to refresh the committed provider without replaying Save.",
                ProviderCatalogProjectionOperationKind.Delete =>
                    $"Call {nameof(IProviderCatalogReconciliation.ReconcileAsync)} for provider {providerId:D} to remove the stale projection without replaying Delete.",
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
                ScenarioHarnessProviderAdministrationConnector.PluginKey,
                StringComparison.Ordinal))
        {
            if (string.Equals(
                    baseUrl,
                    ScenarioHarnessProviderAdministrationConnector.BaseUrl,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new ProviderProfileValidationException(
                "The scenario-harness connector requires its canonical scenario endpoint.");
        }

        if (string.Equals(
                connectorPluginKey,
                ProcessMockProviderAdministrationConnector.PluginKey,
                StringComparison.Ordinal))
        {
            if (string.Equals(
                    baseUrl,
                    ProcessMockProviderAdministrationConnector.BaseUrl,
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
