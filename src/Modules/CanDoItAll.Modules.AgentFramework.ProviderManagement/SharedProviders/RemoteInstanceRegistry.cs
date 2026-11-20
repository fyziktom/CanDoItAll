using CanDoItAll.RemoteInstances;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

internal sealed class RemoteInstanceRegistry(SharedProviderSourceService sources) : IRemoteInstanceRegistry {
    public async Task<IReadOnlyList<RemoteInstanceConnection>> ListAsync(CancellationToken cancellationToken) {
        var found = await sources.ListAsync(cancellationToken);
        return Array.AsReadOnly(found.Select(ToConnection).ToArray());
    }

    public async Task<RemoteInstanceConnection> GetAsync(Guid id, CancellationToken cancellationToken)
        => ToConnection(await sources.GetAsync(id, cancellationToken));

    public Task<RemoteInstanceConnection> CreateAsync(Guid id, RemoteInstanceSettings settings, CancellationToken cancellationToken)
        => AcceptAsync(sources.CreateAsync(ToRequest(settings, id), cancellationToken));

    public Task<RemoteInstanceConnection> UpdateAsync(Guid id, Guid expectedConcurrencyToken, RemoteInstanceSettings settings,
        CancellationToken cancellationToken)
        => AcceptAsync(sources.UpdateAsync(id, expectedConcurrencyToken, ToRequest(settings, id), cancellationToken));

    public async Task DeleteAsync(Guid id, Guid expectedConcurrencyToken, CancellationToken cancellationToken) {
        try {
            await sources.DeleteAsync(id, expectedConcurrencyToken, cancellationToken);
        } catch (SharedProviderConcurrencyException conflict) {
            throw new RemoteInstanceConflictException(id, conflict);
        } catch (SharedProviderSourceDeletionBlockedException blocked) {
            throw new RemoteInstanceInUseException(id, $"{blocked.ImportCount} shared-provider import(s) reference this instance.", blocked);
        }
    }

    private static async Task<RemoteInstanceConnection> AcceptAsync(Task<SharedProviderSourceWriteResult> write) {
        try {
            var result = await write;
            var accepted = result.AcceptedSource ?? throw new InvalidOperationException("The source writer did not return the saved connection.");
            return ToConnection(accepted) with { Warning = result.Change?.Warning };
        } catch (SharedProviderConcurrencyException conflict) {
            throw new RemoteInstanceConflictException(conflict.EntityId, conflict);
        }
    }

    private static SharedProviderSourceWriteRequest ToRequest(RemoteInstanceSettings settings, Guid id) {
        ArgumentNullException.ThrowIfNull(settings);
        return new(settings.Name, settings.BaseUri, settings.ApiTokenSecretId, settings.IsEnabled, settings.AllowPrivateNetwork, id);
    }

    private static RemoteInstanceConnection ToConnection(SharedProviderSourceSnapshot source)
        => new(source.Id, source.Name, source.BaseUri, source.ApiTokenSecretId, source.IsEnabled,
            source.NetworkPolicy == SharedProviderSourceNetworkPolicy.AllowPrivateNetwork, source.ConcurrencyToken);
}
