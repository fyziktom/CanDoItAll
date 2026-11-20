namespace CanDoItAll.RemoteInstances;

public sealed record RemoteInstanceSettings(
    string Name,
    Uri BaseUri,
    Guid ApiTokenSecretId,
    bool IsEnabled,
    bool AllowPrivateNetwork);

public sealed record RemoteInstanceConnection(
    Guid Id,
    string Name,
    Uri BaseUri,
    Guid ApiTokenSecretId,
    bool IsEnabled,
    bool AllowPrivateNetwork,
    Guid ConcurrencyToken) {
    public string? Warning { get; init; }
}

public interface IRemoteInstanceRegistry {
    Task<IReadOnlyList<RemoteInstanceConnection>> ListAsync(CancellationToken cancellationToken = default);
    Task<RemoteInstanceConnection> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RemoteInstanceConnection> CreateAsync(Guid id, RemoteInstanceSettings settings, CancellationToken cancellationToken = default);
    Task<RemoteInstanceConnection> UpdateAsync(Guid id, Guid expectedConcurrencyToken, RemoteInstanceSettings settings,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid expectedConcurrencyToken, CancellationToken cancellationToken = default);
}

public sealed class RemoteInstanceConflictException(Guid id, Exception innerException)
    : InvalidOperationException($"Remote instance '{id:D}' was changed by another request.", innerException);

public sealed class RemoteInstanceInUseException(Guid id, string reason, Exception innerException)
    : InvalidOperationException($"Remote instance '{id:D}' cannot be removed. {reason}", innerException);
