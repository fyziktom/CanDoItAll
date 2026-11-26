namespace CanDoItAll.RemoteInstances;

public enum RemoteInstanceNetworkPolicy { PublicOnly, AllowPrivateNetwork }

public interface IRemoteInstanceJsonReader {
    Task<T> ReadAsync<T>(Uri baseUri, RemoteInstanceNetworkPolicy networkPolicy,
        RemoteInstanceAccessToken credential, string relativeApiPath, CancellationToken cancellationToken);
}

public sealed class RemoteInstanceReadException(string message) : InvalidOperationException(message);
