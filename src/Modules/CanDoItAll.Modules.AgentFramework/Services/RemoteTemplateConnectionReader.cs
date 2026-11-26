using CanDoItAll.RemoteInstances;
using CanDoItAll.Security.Abstractions;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class RemoteTemplateConnectionReader(
    IRemoteInstanceRegistry instances, ISecretRuntimeResolver secrets, IRemoteInstanceJsonReader reader) {
    public async Task<RemoteInstanceConnection> RequireCurrentAsync(Guid sourceId, Guid version, CancellationToken token) {
        var connection = await instances.GetAsync(sourceId, token);
        if (!connection.IsEnabled || connection.ConcurrencyToken != version) {
            throw new RemoteInstanceReadException("The remote connection changed or was disabled. Select it again.");
        }
        return connection;
    }

    public async Task<T> ReadAsync<T>(Guid sourceId, Guid version, string route, CancellationToken token) {
        var connection = await RequireCurrentAsync(sourceId, version, token);
        var value = await secrets.ResolveValueAsync(SecretRuntimeRequests.RemoteInstanceToken(sourceId, connection.ApiTokenSecretId), token);
        if (string.IsNullOrWhiteSpace(value)) {
            throw new RemoteInstanceReadException("The saved credential is unavailable.");
        }
        var network = connection.AllowPrivateNetwork ? RemoteInstanceNetworkPolicy.AllowPrivateNetwork : RemoteInstanceNetworkPolicy.PublicOnly;
        await RequireCurrentAsync(sourceId, version, token);
        var result = await reader.ReadAsync<T>(connection.BaseUri, network, new RemoteInstanceAccessToken(value), route, token);
        await RequireCurrentAsync(sourceId, version, token);
        return result;
    }
}
