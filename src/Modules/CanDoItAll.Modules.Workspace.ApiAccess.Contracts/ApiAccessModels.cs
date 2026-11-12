using System.Collections.Immutable;

namespace CanDoItAll.Modules.Workspace.ApiAccess.Contracts;

public sealed record ApiScopeChoice(string Name, string Label, string Description, string Section,
    bool UserSelectable, bool MachineSelectable, bool Sensitive);

public sealed record ApiAccessConfiguration(bool ApiEnabled, bool OpenApiEnabled, bool SwaggerUiEnabled,
    bool AuthorizationEnabled, bool SigningKeyConfigured, string Issuer, string Audience,
    int DefaultTokenLifetimeMinutes, int MaxTokenLifetimeMinutes, bool UserAuthenticationEnabled,
    bool AccessManagementEnabled, int UserTokenLifetimeMinutes, string AdministratorName,
    ImmutableArray<ApiScopeChoice> Scopes, string DefaultMachineScopes);

public enum ApiTokenCategory { Machine = 0, UserSession = 1, AdministratorSession = 2 }
public enum ApiCredentialStatus { Active = 0, Revoked = 1, Expired = 2 }
public enum ApiWriteAction { IssueToken, RevokeToken, DeleteToken, CreateAccount, UpdateAccount, ResetPassword, DeleteAccount }
public enum ApiWriteState { Pending, Refused, Committed, CommittedWithWarning, Unknown }
public enum ApiFailure { None, Denied, Invalid, Conflict, Missing, Unavailable, Diagnostic, UnknownAcknowledgement }

public sealed record ApiTokenMetadata(Guid Id, string Subject, string DisplayName, DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc, ImmutableArray<string> Scopes, DateTimeOffset? RevokedAtUtc, ApiTokenCategory Kind) {
    public ApiCredentialStatus GetStatus(DateTimeOffset now) => RevokedAtUtc.HasValue ? ApiCredentialStatus.Revoked
        : ExpiresAtUtc <= now ? ApiCredentialStatus.Expired : ApiCredentialStatus.Active;
}

public sealed record ApiAccountMetadata(Guid Id, string UserName, string DisplayName, bool Enabled,
    ImmutableArray<string> Scopes, long Version, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record ApiPageQuery(string Search = "", int Offset = 0) {
    public const int PageSize = 25;
}

public sealed record ApiPage<T>(ImmutableArray<T> Items, int TotalCount);

public sealed record ApiWriteResult(ApiWriteState State, ApiFailure Failure = ApiFailure.None,
    Guid? Identity = null, long? Version = null) {
    public bool IsCommitted => State is ApiWriteState.Committed or ApiWriteState.CommittedWithWarning;
}

public sealed record ApiAccountWriteResult(ApiWriteResult Outcome, ApiAccountMetadata? Account = null);

public sealed record ApiTokenIntent(string Subject, string DisplayName, int? LifetimeMinutes, string ScopeText);
public sealed record ApiTokenAction(Guid Id, ApiTokenCategory Kind, ApiWriteAction Action);

public sealed class ApiAccountIntent : IDisposable {
    private string? password;

    public ApiAccountIntent(ApiWriteAction action, Guid? id, long? expectedVersion, string userName,
        string displayName, bool enabled, string scopeText, string? password) {
        Action = action;
        Id = id;
        ExpectedVersion = expectedVersion;
        UserName = userName;
        DisplayName = displayName;
        Enabled = enabled;
        ScopeText = scopeText;
        this.password = password;
    }

    public ApiWriteAction Action { get; }
    public Guid? Id { get; }
    public long? ExpectedVersion { get; }
    public string UserName { get; }
    public string DisplayName { get; }
    public bool Enabled { get; }
    public string ScopeText { get; }
    public string TakePassword() => Interlocked.Exchange(ref password, null) ?? string.Empty;
    public void Dispose() => password = null;
}

public sealed class ApiTokenDisclosure(ApiWriteResult outcome, ApiTokenMetadata? metadata = null, string? value = null) : IDisposable {
    private string? bearer = value;
    public ApiWriteResult Outcome { get; } = outcome;
    public ApiTokenMetadata? Metadata { get; } = metadata;
    public string? TakeValue() => Interlocked.Exchange(ref bearer, null);
    public void Dispose() => bearer = null;
}
