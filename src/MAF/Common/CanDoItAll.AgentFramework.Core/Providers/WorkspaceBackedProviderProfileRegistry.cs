using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using System.Security.Cryptography;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Core;

internal interface ICatalogShadowProviderProfileRegistry {
    ProviderProfile? TryGetProviderFromCatalog(SandboxWorkspaceCatalog catalog, Guid providerId);
}

public sealed class WorkspaceBackedProviderProfileRegistry(
    ISandboxWorkspaceStore store,
    IProviderProfileService providerProfileService,
    AgentExecutionActivityWorkspaceIdentity workspaceIdentity) :
    IProviderProfileRegistry,
    IProviderRuntimeProfileSource,
    IProviderRuntimeProfileSnapshotSource,
    ICatalogShadowProviderProfileRegistry {
    public async Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default) {
        var catalog = await store.LoadCatalogAsync(cancellationToken);
        return catalog.Providers
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ProviderProfile?> GetProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default) {
        var catalog = await store.LoadCatalogAsync(cancellationToken);
        return catalog.Providers.FirstOrDefault(item => item.Id == providerId);
    }

    public async Task<ProviderProfileEditorModel> GetProviderEditorAsync(
        Guid? providerId = null,
        CancellationToken cancellationToken = default) {
        if (!providerId.HasValue) {
            return providerProfileService.CreateEditor();
        }

        var observation = await GetProviderObservationAsync(providerId.Value, cancellationToken);
        return CreateEditor(observation.Profile, observation.ExpectedConcurrencyToken);
    }

    public async Task<ProviderProfileObservation> GetProviderObservationAsync(Guid providerId, CancellationToken cancellationToken = default) {
        var catalog = await store.LoadCatalogAsync(cancellationToken);
        var provider = catalog.Providers.SingleOrDefault(item => item.Id == providerId)
            ?? throw new KeyNotFoundException("Provider profile was not found.");
        return new(workspaceIdentity, provider, Token(catalog, providerId));
    }

    public async Task<Guid> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default)
        => (await SaveProviderEditorAsync(model, cancellationToken)).Id!.Value;

    public async Task<ProviderProfileEditorModel> SaveProviderEditorAsync(
        ProviderProfileEditorModel model,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(model);
        model = ProviderEditorModelSnapshot.Copy(model);
        var validatedProfile = providerProfileService.CreateProfile(model);
        var providerId = validatedProfile.Id;
        var accepted = await CommitAsync(providerId, catalog => {
            var current = model.Id.HasValue
                ? catalog.Providers.FirstOrDefault(item => item.Id == model.Id.Value)
                : null;

            if (current is not null && model.ExpectedConcurrencyToken != Token(catalog, current.Id) ||
                current is null && (model.Id.HasValue && model.ExpectedConcurrencyToken != Guid.Empty ||
                    model.ExpectedConcurrencyToken is { } expected && expected != Guid.Empty)) {
                throw new ProviderConfigurationConflictException(model.Id ?? Guid.Empty);
            }

            var provider = current is null
                ? validatedProfile
                : providerProfileService.CreateProfile(model, current);
            providerId = provider.Id;
            EnsureUniqueProviderIdentity(catalog.Providers, provider);

            return catalog with {
                Providers = catalog.Providers
                    .Where(item => item.Id != provider.Id)
                    .Append(provider)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }, cancellationToken);

        return CreateEditor(accepted.Providers.Single(provider => provider.Id == providerId), Token(accepted, providerId));
    }

    public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
        => DeleteProviderCoreAsync(providerId, null, cancellationToken);

    public Task DeleteProviderAsync(Guid providerId, Guid expectedConcurrencyToken, CancellationToken cancellationToken = default)
        => DeleteProviderCoreAsync(providerId, expectedConcurrencyToken, cancellationToken);

    private async Task DeleteProviderCoreAsync(Guid providerId, Guid? expectedConcurrencyToken, CancellationToken cancellationToken) {
        await CommitAsync(providerId, catalog => {
            if (catalog.Providers.All(provider => provider.Id != providerId)) {
                return catalog;
            }
            if (expectedConcurrencyToken != Token(catalog, providerId)) {
                throw new ProviderConfigurationConflictException(providerId);
            }
            return catalog with {
            Providers = catalog.Providers.Where(item => item.Id != providerId).ToList(),
            Agents = catalog.Agents
                .Select(item => item.ProviderProfileId == providerId
                    ? item with { ProviderProfileId = null,
                        UpdatedAtUtc = AgentConfigurationVersion.NextRevision(item.UpdatedAtUtc, DateTimeOffset.UtcNow) }
                    : item)
                .ToList()
            };
        }, cancellationToken);
    }

    public async Task RecordProviderHealthAsync(ProviderProfileObservation observation, ProviderHealthResult result,
        DateTimeOffset checkedAtUtc, CancellationToken cancellationToken = default) {
        if (observation.Workspace != workspaceIdentity) {
            throw new UnauthorizedAccessException("The provider diagnostic belongs to another workspace or activation.");
        }
        await CommitAsync(observation.Profile.Id, catalog => {
            var provider = catalog.Providers.SingleOrDefault(item => item.Id == observation.Profile.Id);
            if (provider is null || Token(catalog, provider.Id) != observation.ExpectedConcurrencyToken) {
                throw new ProviderConfigurationConflictException(observation.Profile.Id);
            }
            if (provider.LastCheckedAtUtc > checkedAtUtc) {
                return catalog;
            }
            var updated = provider with { HealthStatus = result.Success ? HealthyStatus : UnhealthyStatus, LastCheckedAtUtc = checkedAtUtc };
            return catalog with { Providers = catalog.Providers.Select(item => item.Id == provider.Id ? updated : item).ToList() };
        }, cancellationToken);
    }

    private const string HealthyStatus = "Healthy";
    private const string UnhealthyStatus = "Unhealthy";

    private async Task<SandboxWorkspaceCatalog> CommitAsync(Guid providerId,
        Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update, CancellationToken cancellationToken) {
        try {
            return await store.UpdateCatalogAsync(update, cancellationToken);
        } catch (SandboxWorkspaceCatalogCommittedException exception) {
            var provider = exception.Accepted.Providers.SingleOrDefault(item => item.Id == providerId);
            var accepted = provider is null ? null : CreateEditor(provider, Token(exception.Accepted, providerId));
            throw new ProviderConfigurationCommittedException(providerId, accepted, exception);
        }
    }

    private ProviderProfileEditorModel CreateEditor(ProviderProfile provider, Guid token) {
        var result = providerProfileService.CreateEditor(provider);
        result.ExpectedConcurrencyToken = token;
        return result;
    }

    private Guid Token(SandboxWorkspaceCatalog catalog, Guid providerId) {
        if (!catalog.CatalogDataRevision.IsAssigned) {
            throw new NotSupportedException("Provider editing requires an assigned, coordinated catalog revision.");
        }
        var digest = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new CatalogObservation(workspaceIdentity, providerId, catalog.CatalogDataRevision)));
        return new(digest.AsSpan(0, 16));
    }

    private sealed record CatalogObservation(AgentExecutionActivityWorkspaceIdentity Workspace, Guid ProviderId, CatalogDataRevision Revision);

    public async Task<ProviderProfile> UpdateProviderAsync(
        Guid providerId,
        Func<ProviderProfile, ProviderProfile> update,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(update);

        ProviderProfile? updatedProvider = null;
        await CommitAsync(providerId, catalog => {
            var currentProvider = catalog.Providers.FirstOrDefault(item => item.Id == providerId)
                ?? throw new InvalidOperationException("Provider profile was not found.");
            var candidate = update(currentProvider);
            if (candidate.Id != providerId) {
                throw new ProviderProfileValidationException(
                    "Provider updates cannot change the provider id.");
            }

            _ = providerProfileService.CreateProfile(
                ProviderProfileEditorModel.FromDefinition(candidate));
            updatedProvider = providerProfileService.NormalizeImportedProfile(candidate);
            EnsureUniqueProviderIdentity(catalog.Providers, updatedProvider);

            return catalog with {
                Providers = catalog.Providers
                    .Where(item => item.Id != providerId)
                    .Append(updatedProvider)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }, cancellationToken);

        return updatedProvider ?? throw new InvalidOperationException("Provider profile was not updated.");
    }

    private void EnsureUniqueProviderIdentity(
        IEnumerable<ProviderProfile> existingProviders,
        ProviderProfile provider) {
        var identityKey = providerProfileService.GetIdentityKey(provider);
        var collisions = existingProviders
            .Where(item => item.Id != provider.Id)
            .Where(item => string.Equals(providerProfileService.GetIdentityKey(item), identityKey, StringComparison.Ordinal))
            .Select(item => item.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (collisions.Count == 0) {
            return;
        }

        throw new InvalidOperationException(
            $"Provider save would reuse canonical provider identity '{identityKey}', which already belongs to: {string.Join(", ", collisions)}.");
    }

    ProviderProfile? ICatalogShadowProviderProfileRegistry.TryGetProviderFromCatalog(
        SandboxWorkspaceCatalog catalog,
        Guid providerId) {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.Providers.FirstOrDefault(item => item.Id == providerId);
    }

    public ProviderRuntimeProfileSnapshotLease? CaptureProvider(
        Guid providerId,
        SandboxWorkspaceCatalogSnapshot catalogSnapshot) {
        ArgumentNullException.ThrowIfNull(catalogSnapshot);
        var provider = catalogSnapshot.Catalog.Providers
            .FirstOrDefault(item => item.Id == providerId);
        return provider is null
            ? null
            : new ProviderRuntimeProfileSnapshotLease(
                provider,
                ProviderConfigurationFingerprintFactory.Create(provider));
    }
}
