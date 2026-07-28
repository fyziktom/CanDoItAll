using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal interface ICatalogShadowProviderProfileRegistry
{
    ProviderProfile? TryGetProviderFromCatalog(SandboxWorkspaceCatalog catalog, Guid providerId);
}

public sealed class WorkspaceBackedProviderProfileRegistry(
    ISandboxWorkspaceStore store,
    IProviderProfileService providerProfileService) :
    IProviderProfileRegistry,
    IProviderRuntimeProfileSource,
    IProviderRuntimeProfileSnapshotSource,
    ICatalogShadowProviderProfileRegistry
{
    public async Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
    {
        var catalog = await store.LoadCatalogAsync(cancellationToken);
        return catalog.Providers
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ProviderProfile?> GetProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await store.LoadCatalogAsync(cancellationToken);
        return catalog.Providers.FirstOrDefault(item => item.Id == providerId);
    }

    public async Task<ProviderProfileEditorModel> GetProviderEditorAsync(
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
        ProviderProfileEditorModel model,
        CancellationToken cancellationToken = default)
    {
        Guid providerId = Guid.Empty;
        await store.UpdateCatalogAsync(catalog =>
        {
            var current = model.Id.HasValue
                ? catalog.Providers.FirstOrDefault(item => item.Id == model.Id.Value)
                : null;

            var provider = providerProfileService.CreateProfile(model, current);
            providerId = provider.Id;
            EnsureUniqueProviderIdentity(catalog.Providers, provider);

            return catalog with
            {
                Providers = catalog.Providers
                    .Where(item => item.Id != provider.Id)
                    .Append(provider)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }, cancellationToken);

        return providerId;
    }

    public async Task DeleteProviderAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        await store.UpdateCatalogAsync(catalog => catalog with
        {
            Providers = catalog.Providers.Where(item => item.Id != providerId).ToList(),
            Agents = catalog.Agents
                .Select(item => item.ProviderProfileId == providerId
                    ? item with { ProviderProfileId = null }
                    : item)
                .ToList()
        }, cancellationToken);
    }

    public async Task<ProviderProfile> UpdateProviderAsync(
        Guid providerId,
        Func<ProviderProfile, ProviderProfile> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        ProviderProfile? updatedProvider = null;
        await store.UpdateCatalogAsync(catalog =>
        {
            var currentProvider = catalog.Providers.FirstOrDefault(item => item.Id == providerId)
                ?? throw new InvalidOperationException("Provider profile was not found.");
            updatedProvider = update(currentProvider);
            EnsureUniqueProviderIdentity(catalog.Providers, updatedProvider);

            return catalog with
            {
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
        ProviderProfile provider)
    {
        var identityKey = providerProfileService.GetIdentityKey(provider);
        var collisions = existingProviders
            .Where(item => item.Id != provider.Id)
            .Where(item => string.Equals(providerProfileService.GetIdentityKey(item), identityKey, StringComparison.Ordinal))
            .Select(item => item.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (collisions.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Provider save would reuse canonical provider identity '{identityKey}', which already belongs to: {string.Join(", ", collisions)}.");
    }

    ProviderProfile? ICatalogShadowProviderProfileRegistry.TryGetProviderFromCatalog(
        SandboxWorkspaceCatalog catalog,
        Guid providerId)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.Providers.FirstOrDefault(item => item.Id == providerId);
    }

    public ProviderRuntimeProfileSnapshotLease? CaptureProvider(
        Guid providerId,
        SandboxWorkspaceCatalogSnapshot catalogSnapshot)
    {
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
