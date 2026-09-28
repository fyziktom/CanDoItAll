namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed record ApiUserRecord(
    Guid Id,
    string UserName,
    string NormalizedUserName,
    string DisplayName,
    bool Enabled,
    string PasswordHash,
    IReadOnlyList<string> AllowedScopes,
    long AuthenticationRevision,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed class ApiUserConflictException(string message) : InvalidOperationException(message);

public interface IApiUserStore {
    Task<IReadOnlyList<ApiUserRecord>> ReadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ApiUserRecord user, long? expectedVersion, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default);
}
