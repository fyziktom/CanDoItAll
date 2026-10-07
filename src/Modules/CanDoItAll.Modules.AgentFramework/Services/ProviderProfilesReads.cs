using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Security;
using IProviderAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderAdministrationService;
using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;
using IProviderRuntimeProfileSnapshotLoader = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeProfileSnapshotLoader;

namespace CanDoItAll.Modules.AgentFramework;

public sealed record ProviderSecretReferences(IReadOnlyList<SecretListItem> Items, string? Error = null);
public sealed record ProviderProfilesCatalog(IReadOnlyList<ProviderProfile> Providers, ProviderSecretReferences Secrets) {
    public IReadOnlyDictionary<Guid, ProviderConfigurationRevision> Revisions { get; init; } = new Dictionary<Guid, ProviderConfigurationRevision>();
}

public interface IProviderProfilesReads {
    Task<ProviderProfilesCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default);
    Task<ProviderProfileEditorModel> LoadEditorAsync(Guid providerId, CancellationToken cancellationToken = default);
}

public sealed class ProviderProfilesReads(
    IProviderRuntimeAdministrationService runtime,
    IProviderAdministrationService administration,
    IProviderRuntimeProfileSnapshotLoader snapshots) : IProviderProfilesReads {
    public async Task<ProviderProfilesCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default) {
        var providers = snapshots.LoadAllAsync(cancellationToken);
        var secrets = ReadSecretsAsync(cancellationToken);
        await Task.WhenAll(providers, secrets);
        cancellationToken.ThrowIfCancellationRequested();
        var loaded = await providers;
        return new(loaded.Select(item => item.Profile).ToArray(), await secrets) {
            Revisions = loaded.Where(item => item.ConfigurationRevision.HasValue)
                .ToDictionary(item => item.Profile.Id, item => item.ConfigurationRevision!.Value)
        };
    }

    public Task<ProviderProfileEditorModel> LoadEditorAsync(Guid providerId, CancellationToken cancellationToken = default)
        => runtime.GetProviderEditorAsync(providerId, cancellationToken);

    private async Task<ProviderSecretReferences> ReadSecretsAsync(CancellationToken cancellationToken) {
        try {
            return new(await administration.ListSecretsAsync(cancellationToken));
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception) {
            return new([], "Secret metadata could not be loaded. Saved references are retained.");
        }
    }
}
