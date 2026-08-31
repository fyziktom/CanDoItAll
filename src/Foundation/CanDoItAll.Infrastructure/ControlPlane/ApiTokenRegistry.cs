namespace CanDoItAll.Infrastructure.ControlPlane;

public enum ApiTokenStatus {
    Active,
    Revoked,
    Expired
}

public sealed record ApiTokenRecord(
    Guid Id,
    string Subject,
    string DisplayName,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? RevokedAtUtc = null) {
    public ApiTokenStatus GetStatus(DateTimeOffset now) => RevokedAtUtc.HasValue
        ? ApiTokenStatus.Revoked
        : ExpiresAtUtc <= now ? ApiTokenStatus.Expired : ApiTokenStatus.Active;
}

public sealed record ApiTokenQuery(string Search = "", int Offset = 0, int PageSize = 25);

public sealed record ApiTokenPage(IReadOnlyList<ApiTokenRecord> Items, int TotalCount);

public interface IApiTokenRegistry {
    void Register(ApiTokenRecord token);
    Task<ApiTokenRecord?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiTokenPage> SearchAsync(ApiTokenQuery query, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid id, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
