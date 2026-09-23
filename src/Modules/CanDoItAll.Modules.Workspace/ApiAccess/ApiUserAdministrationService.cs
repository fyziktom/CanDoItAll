using System.Text.Json.Serialization;
using System.ComponentModel;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Workspace.ApiAccess;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Description("New ordinary account with explicit business grants; cannot create an administrator.")]
public sealed record ApiUserCreateRequest(
    [property: Description("Unique ASCII username, 1–64 characters; compared case-insensitively and cannot equal the configured administrator name.")] string UserName,
    [property: Description("Human-readable account name, 1–128 characters with no control characters.")] string DisplayName,
    [property: Description("Initial password, 12–256 characters without trimming; stored only as a salted hash.")] string Password,
    [property: Description("Whether this account may authenticate immediately.")] bool Enabled,
    [property: Description("Explicit ordinary-user capabilities from the catalog; an empty list grants no business access.")] IReadOnlyList<string> Scopes);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Description("Replacement ordinary-account profile; every successful update invalidates its existing sessions.")]
public sealed record ApiUserUpdateRequest(
    [property: Description("Unique account username under the same normalization rules as creation.")] string UserName,
    [property: Description("Human-readable account name, 1–128 characters with no control characters.")] string DisplayName,
    [property: Description("Whether new logins are allowed; re-enabling never revives old sessions.")] bool Enabled,
    [property: Description("Complete replacement set of explicit ordinary-user business capabilities.")] IReadOnlyList<string> Scopes,
    [property: Description("Version returned by the last account read; a stale value returns 409 without writing.")] long ExpectedVersion);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Description("Version-checked password replacement that invalidates every existing session of this account.")]
public sealed record ApiUserPasswordResetRequest(
    [property: Description("New password, 12–256 characters without trimming; never returned by account reads.")] string Password,
    [property: Description("Version returned by the last account read; a stale value returns 409 without writing.")] long ExpectedVersion);

[Description("Safe ordinary-account profile; excludes password hashes and private credential-revision material.")]
public sealed record ApiUserDetails(
    [property: Description("Immutable account GUID; recreating a deleted username creates a different identity.")] Guid Id,
    [property: Description("Stored account username; comparisons use invariant ASCII case normalization.")] string UserName,
    [property: Description("Human-readable account name.")] string DisplayName,
    [property: Description("Whether this account may authenticate.")] bool Enabled,
    [property: Description("Explicit business capabilities currently assigned to this account.")] IReadOnlyList<string> Scopes,
    [property: Description("Current account version required by update, reset and delete operations.")] long Version,
    [property: Description("Account creation instant in UTC.")] DateTimeOffset CreatedAtUtc,
    [property: Description("Most recent account mutation instant in UTC.")] DateTimeOffset UpdatedAtUtc);

[Description("Bounded account search page without credential material.")]
public sealed record ApiUserPage(
    [property: Description("Accounts in this page, ordered by normalized username.")] IReadOnlyList<ApiUserDetails> Items,
    [property: Description("Total accounts matching the search before paging.")] int TotalCount);

public sealed class ApiUserAdministrationService(
    IApiUserStore store,
    ApiPasswordService passwords,
    IApiTokenAdministrationAccess access,
    IOptions<ApiAccessOptions> options,
    IClock clock,
    ILogger<ApiUserAdministrationService> logger) {

    public ValueTask<bool> CanManageAsync(CancellationToken cancellationToken = default) => access.CanManageAsync(cancellationToken);

    public async Task<ApiUserPage> SearchAsync(string? search = null, int offset = 0, int pageSize = 25, CancellationToken cancellationToken = default) {
        await EnsureAccessAsync(cancellationToken);
        if (offset < 0 || pageSize is < 1 or > 100 || search?.Length > 128) {
            throw new ArgumentException("Invalid account search or page bounds.");
        }
        var users = (await store.ReadAsync(cancellationToken)).Where(user => string.IsNullOrEmpty(search) ||
            user.UserName.Contains(search, StringComparison.OrdinalIgnoreCase) || user.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(user => user.NormalizedUserName, StringComparer.Ordinal).ToArray();
        return new(users.Skip(offset).Take(pageSize).Select(ToDetails).ToArray(), users.Length);
    }

    public async Task<ApiUserDetails> GetAsync(Guid id, CancellationToken cancellationToken = default) {
        await EnsureAccessAsync(cancellationToken);
        return ToDetails(await FindAsync(id, cancellationToken));
    }

    public async Task<ApiUserDetails> CreateAsync(ApiUserCreateRequest request, CancellationToken cancellationToken = default) {
        await EnsureAccessAsync(cancellationToken);
        ArgumentNullException.ThrowIfNull(request);
        var userName = ValidateUserName(request.UserName);
        var displayName = ApiIdentityRules.DisplayName(request.DisplayName);
        var scopes = ApiScopeCatalog.ValidateGrants(request.Scopes, forUser: true);
        var hash = passwords.Hash(request.Password);
        var now = clock.GetUtcNow();
        var user = new ApiUserRecord(Guid.NewGuid(), userName, userName.ToUpperInvariant(), displayName,
            request.Enabled, hash, scopes, 1, 1, now, now);
        await store.SaveAsync(user, expectedVersion: null, cancellationToken);
        logger.LogInformation("Created API account {AccountId} with {ScopeCount} capabilities.", user.Id, scopes.Count);
        return ToDetails(user);
    }

    public async Task<ApiUserDetails> UpdateAsync(Guid id, ApiUserUpdateRequest request, CancellationToken cancellationToken = default) {
        await EnsureAccessAsync(cancellationToken);
        ArgumentNullException.ThrowIfNull(request);
        var userName = ValidateUserName(request.UserName);
        var current = await FindAsync(id, cancellationToken);
        var user = current with {
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            DisplayName = ApiIdentityRules.DisplayName(request.DisplayName),
            Enabled = request.Enabled,
            AllowedScopes = ApiScopeCatalog.ValidateGrants(request.Scopes, forUser: true),
            Version = checked(current.Version + 1),
            AuthenticationRevision = checked(current.AuthenticationRevision + 1),
            UpdatedAtUtc = clock.GetUtcNow()
        };
        await store.SaveAsync(user, request.ExpectedVersion, cancellationToken);
        logger.LogInformation("Updated API account {AccountId}; existing sessions invalidated.", id);
        return ToDetails(user);
    }

    public async Task<ApiUserDetails> ResetPasswordAsync(Guid id, ApiUserPasswordResetRequest request, CancellationToken cancellationToken = default) {
        await EnsureAccessAsync(cancellationToken);
        ArgumentNullException.ThrowIfNull(request);
        var current = await FindAsync(id, cancellationToken);
        var user = current with {
            PasswordHash = passwords.Hash(request.Password),
            Version = checked(current.Version + 1),
            AuthenticationRevision = checked(current.AuthenticationRevision + 1),
            UpdatedAtUtc = clock.GetUtcNow()
        };
        await store.SaveAsync(user, request.ExpectedVersion, cancellationToken);
        logger.LogInformation("Reset API account {AccountId} password; existing sessions invalidated.", id);
        return ToDetails(user);
    }

    public async Task DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) {
        await EnsureAccessAsync(cancellationToken);
        await store.DeleteAsync(id, expectedVersion, cancellationToken);
        logger.LogInformation("Deleted API account {AccountId}; existing sessions invalidated.", id);
    }

    private string ValidateUserName(string? value) {
        var userName = ApiIdentityRules.UserName(value);
        if (userName.ToUpperInvariant() == ApiIdentityRules.NormalizeUserName(options.Value.BootstrapAdmin.UserName)) {
            throw new ApiUserConflictException("This username belongs to the configuration-owned administrator.");
        }
        return userName;
    }

    private async Task<ApiUserRecord> FindAsync(Guid id, CancellationToken cancellationToken) =>
        (await store.ReadAsync(cancellationToken)).FirstOrDefault(user => user.Id == id)
        ?? throw new KeyNotFoundException("The account no longer exists.");

    private async Task EnsureAccessAsync(CancellationToken cancellationToken) {
        if (!await access.CanManageAsync(cancellationToken)) {
            throw new UnauthorizedAccessException("API access administration requires an authorized administrator.");
        }
    }

    private static ApiUserDetails ToDetails(ApiUserRecord user) => new(user.Id, user.UserName, user.DisplayName,
        user.Enabled, user.AllowedScopes, user.Version, user.CreatedAtUtc, user.UpdatedAtUtc);
}
