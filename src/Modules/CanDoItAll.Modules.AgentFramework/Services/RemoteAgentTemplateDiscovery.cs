using CanDoItAll.AgentFramework.Models;
using CanDoItAll.RemoteInstances;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class RemoteAgentTemplateDiscovery(
    IRemoteInstanceRegistry instances, ISecretRuntimeResolver secrets, IRemoteInstanceJsonReader reader) {
    public async Task<IReadOnlyList<RemoteAgentTemplateSummary>> ListAsync(Guid sourceId, Guid version, CancellationToken token) {
        var rows = await ReadAsync<RemoteAgentTemplateSummary[]>(sourceId, version, RemoteAgentTemplateRoutes.Catalog, token);
        if (rows.Any(row => row is null || row.Id == Guid.Empty || row.UpdatedAtUtc == default ||
                string.IsNullOrWhiteSpace(row.Name) || row.Name.Length > 512 || row.CapabilityCount < 0) ||
            rows.DistinctBy(row => row.Id).Count() != rows.Length) {
            throw new RemoteInstanceReadException("The remote template catalog contains invalid entries.");
        }
        return rows;
    }

    public async Task<RemoteAgentTemplate> FetchAsync(Guid sourceId, Guid version, RemoteAgentTemplateSummary selected, CancellationToken token) {
        var template = await ReadAsync<RemoteAgentTemplate>(sourceId, version, RemoteAgentTemplateRoutes.Details(selected.Id), token);
        if (template.Id != selected.Id || template.UpdatedAtUtc != selected.UpdatedAtUtc ||
            template.Capabilities is null || template.Capabilities.Any(item => item is null)) {
            throw new RemoteInstanceReadException("The selected template changed or is unavailable. Refresh the catalog.");
        }
        return template;
    }

    public async Task<RemoteInstanceConnection> RequireCurrentAsync(Guid sourceId, Guid version, CancellationToken token) {
        var connection = await instances.GetAsync(sourceId, token);
        if (!connection.IsEnabled || connection.ConcurrencyToken != version) {
            throw new RemoteInstanceReadException("The remote connection changed or was disabled. Select it again.");
        }
        return connection;
    }

    private async Task<T> ReadAsync<T>(Guid sourceId, Guid version, string route, CancellationToken token) {
        var connection = await RequireCurrentAsync(sourceId, version, token);
        var value = await secrets.ResolveValueAsync(SecretRuntimeRequests.RemoteInstanceToken(sourceId, connection.ApiTokenSecretId), token);
        if (string.IsNullOrWhiteSpace(value)) {
            throw new RemoteInstanceReadException("The saved credential is unavailable.");
        }
        var network = connection.AllowPrivateNetwork ? SharedProviderSourceNetworkPolicy.AllowPrivateNetwork : SharedProviderSourceNetworkPolicy.PublicOnly;
        await RequireCurrentAsync(sourceId, version, token);
        var result = await reader.ReadAsync<T>(connection.BaseUri, network, new SharedProviderCatalogAccessToken(value), route, token);
        await RequireCurrentAsync(sourceId, version, token);
        return result;
    }
}
