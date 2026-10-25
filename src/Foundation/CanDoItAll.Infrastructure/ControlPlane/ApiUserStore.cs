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

public enum ApiAuthorityReadFailure {
    NotReady,
    MissingPath,
    UnsafePath,
    InvalidFormat,
    SizeLimit,
    IoFailure
}

public sealed class ApiAuthorityReadException(ApiAuthorityReadFailure failure)
    : IOException($"The API authority store could not be observed ({failure}).") {
    public ApiAuthorityReadFailure Failure { get; } = failure;
}

public sealed record ApiUserAuthorityGeneration {
    public ApiUserAuthorityGeneration(Guid value) {
        if (value == Guid.Empty) {
            throw new ArgumentException("An authority generation is required.", nameof(value));
        }
        Value = value;
    }

    public Guid Value { get; }
}

public sealed record ApiUserStoreSnapshot {
    public ApiUserStoreSnapshot(ApiUserAuthorityGeneration generation, IReadOnlyList<ApiUserRecord> users) {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(users);
        Generation = generation;
        Users = Array.AsReadOnly(users.Select(Freeze).ToArray());
    }

    public ApiUserAuthorityGeneration Generation { get; }
    public IReadOnlyList<ApiUserRecord> Users { get; }

    internal static ApiUserRecord Freeze(ApiUserRecord user) {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(user.AllowedScopes);
        return user with { AllowedScopes = Array.AsReadOnly(user.AllowedScopes.ToArray()) };
    }
}

public abstract record ApiUserAuthorityObservation {
    private ApiUserAuthorityObservation() { }

    public sealed record Current : ApiUserAuthorityObservation;
    public sealed record Changed : ApiUserAuthorityObservation;
    public sealed record Unavailable(ApiAuthorityReadFailure Failure) : ApiUserAuthorityObservation;
}

public interface IApiUserStore {
    Task<IReadOnlyList<ApiUserRecord>> ReadAsync(CancellationToken cancellationToken = default);
    Task EnsureAuthorityFormatAsync(CancellationToken cancellationToken = default);
    Task<ApiUserStoreSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default);
    ValueTask<ApiUserAuthorityObservation> ObserveAuthorityAsync(ApiUserAuthorityGeneration expected, CancellationToken cancellationToken = default);
    Task SaveAsync(ApiUserRecord user, long? expectedVersion, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default);
}
