using System.ComponentModel;

namespace CanDoItAll.Infrastructure.ControlPlane;

public enum ApiTokenStatus {
    Active,
    Revoked,
    Expired
}

[Description("Registered credential category; 0 Machine, 1 UserSession, 2 AdministratorSession. Claims cannot change this server-owned category.")]
public enum ApiCredentialKind {
    Machine,
    UserSession,
    AdministratorSession
}

public sealed record ApiTokenRecord(
    Guid Id,
    string Subject,
    string DisplayName,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? RevokedAtUtc = null,
    ApiCredentialKind Kind = ApiCredentialKind.Machine,
    Guid? UserId = null,
    long? AuthenticationRevision = null,
    string? AdministratorCredentialBinding = null) {
    public ApiTokenStatus GetStatus(DateTimeOffset now) => RevokedAtUtc.HasValue
        ? ApiTokenStatus.Revoked
        : ExpiresAtUtc <= now ? ApiTokenStatus.Expired : ApiTokenStatus.Active;
}

public sealed record ApiTokenQuery(string Search = "", int Offset = 0, int PageSize = 25,
    ApiCredentialKind? Kind = ApiCredentialKind.Machine);

[Description("Safe registered-token metadata; never contains token plaintext, password hashes or administrator credential bindings.")]
public sealed record ApiTokenSummary(
    [property: Description("Registry GUID used to revoke or delete this credential.")] Guid Id,
    [property: Description("Registered subject identifying this credential's caller.")] string Subject,
    [property: Description("Human-readable label recorded at issuance.")] string DisplayName,
    [property: Description("Credential issuance instant in UTC.")] DateTimeOffset IssuedAtUtc,
    [property: Description("Registered credential expiry instant in UTC.")] DateTimeOffset ExpiresAtUtc,
    [property: Description("Capabilities recorded when this credential was issued; current account state is also checked for sessions.")] IReadOnlyList<string> Scopes,
    [property: Description("Revocation instant in UTC, or null when not explicitly revoked.")] DateTimeOffset? RevokedAtUtc,
    [property: Description("Server-owned credential category; 0 Machine, 1 UserSession, 2 AdministratorSession.")] ApiCredentialKind Kind) {
    public ApiTokenStatus GetStatus(DateTimeOffset now) => RevokedAtUtc.HasValue ? ApiTokenStatus.Revoked :
        ExpiresAtUtc <= now ? ApiTokenStatus.Expired : ApiTokenStatus.Active;

    public static ApiTokenSummary FromRecord(ApiTokenRecord record) => new(record.Id, record.Subject, record.DisplayName,
        record.IssuedAtUtc, record.ExpiresAtUtc, record.Scopes, record.RevokedAtUtc, record.Kind);
}

[Description("Bounded search page of safe registered-credential metadata.")]
public sealed record ApiTokenPage(
    [property: Description("Matching registered credentials in this page.")] IReadOnlyList<ApiTokenSummary> Items,
    [property: Description("Number of matching registered credentials before paging.")] int TotalCount);

public interface IApiTokenRegistry {
    void Register(ApiTokenRecord token);
    Task<ApiTokenRecord?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiTokenPage> SearchAsync(ApiTokenQuery query, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid id, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
