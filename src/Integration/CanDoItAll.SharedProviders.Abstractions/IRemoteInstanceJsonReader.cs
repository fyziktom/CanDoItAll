using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.RemoteInstances;

public interface IRemoteInstanceJsonReader {
    Task<T> ReadAsync<T>(Uri baseUri, SharedProviderSourceNetworkPolicy networkPolicy,
        SharedProviderCatalogAccessToken credential, string relativeApiPath, CancellationToken cancellationToken);
}

public sealed class RemoteInstanceReadException(string message) : InvalidOperationException(message);
