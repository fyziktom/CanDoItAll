using System.Security.Cryptography;
using System.ComponentModel;
using System.Text;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Workspace.ApiAccess;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Description("Credentials used to create a fresh registered session; accepts no caller-selected authority.")]
public sealed record ApiLoginRequest(
    [property: Description("Account username, compared using invariant ASCII case normalization; 1–64 characters, without spaces.")] string UserName,
    [property: Description("Account password, 12–256 characters without trimming. Never store it in browser storage or logs.")] string Password);

[Description("Safe identity and current capabilities of a validated user or configured administrator session.")]
public sealed record ApiSessionIdentity(
    [property: Description("Stable ordinary-account GUID; null for the deployment-configured administrator.")] Guid? UserId,
    [property: Description("Current account or configured administrator username.")] string UserName,
    [property: Description("Current human-readable name of this session identity.")] string DisplayName,
    [property: Description("True only for a registered session bound to the current configured administrator credentials.")] bool IsAdministrator,
    [property: Description("Effective capabilities granted by the server, including self-session capability.")] IReadOnlyList<string> Scopes);

[Description("Fresh registered session token returned once after successful password verification; response caching is forbidden.")]
public sealed record ApiLoginResult(
    [property: Description("Secret signed JWT to send in the Authorization header; never include it in URLs or logs.")] string Token,
    [property: Description("Authorization scheme for this token; always Bearer.")] string TokenType,
    [property: Description("Registered session expiry instant in UTC.")] DateTimeOffset ExpiresAtUtc,
    [property: Description("Stable ordinary-account GUID; null for the configured administrator.")] Guid? UserId,
    [property: Description("Human-readable name of the authenticated identity.")] string DisplayName,
    [property: Description("Exact server-selected capabilities of the issued session.")] IReadOnlyList<string> Scopes,
    [property: Description("True only for the authenticated deployment-configured administrator.")] bool IsAdministrator);

public sealed class ApiLoginBusyException() : InvalidOperationException("Login capacity is temporarily exhausted. Retry later.");

public sealed class ApiSessionService(
    IOptions<ApiAccessOptions> options,
    IApiUserStore users,
    IApiTokenRegistry registry,
    ApiPasswordService passwords,
    IClock clock) : IDisposable {
    private readonly SemaphoreSlim hashAdmission = new(4, 4);
    private readonly string dummyHash = passwords.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    public async Task<ApiLoginResult?> LoginAsync(ApiLoginRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        if (!options.Value.UserAuthentication.Enabled) {
            throw new InvalidOperationException("API user authentication is disabled.");
        }
        var normalizedName = ApiIdentityRules.NormalizeUserName(request.UserName);
        ApiIdentityRules.ValidatePassword(request.Password);
        if (!await hashAdmission.WaitAsync(0, cancellationToken)) {
            throw new ApiLoginBusyException();
        }
        try {
            var admin = options.Value.BootstrapAdmin;
            var isAdministrator = normalizedName == ApiIdentityRules.NormalizeUserName(admin.UserName);
            var user = isAdministrator ? null : (await users.ReadAsync(cancellationToken)).FirstOrDefault(candidate => candidate.NormalizedUserName == normalizedName);
            var hash = isAdministrator ? admin.PasswordHash : user?.PasswordHash ?? dummyHash;
            var verified = passwords.Verify(hash, request.Password);
            if (verified == PasswordVerificationResult.Failed || !isAdministrator && user?.Enabled != true) {
                return null;
            }
            if (user is not null && verified == PasswordVerificationResult.SuccessRehashNeeded) {
                var updated = user with { PasswordHash = passwords.Hash(request.Password), Version = checked(user.Version + 1), UpdatedAtUtc = clock.GetUtcNow() };
                await users.SaveAsync(updated, user.Version, cancellationToken);
                user = updated;
            }
            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(clock.GetUtcNow().ToUnixTimeSeconds());
            var scopes = isAdministrator
                ? new[] { ApiAccessScopeNames.Session, ApiAccessScopeNames.ManageAccess }
                : ApiScopeCatalog.ValidateGrants(user!.AllowedScopes, forUser: true).Append(ApiAccessScopeNames.Session).Order(StringComparer.Ordinal).ToArray();
            var record = new ApiTokenRecord(Guid.NewGuid(), isAdministrator ? ApiBootstrapAdminOptions.Subject : UserSubject(user!.Id),
                isAdministrator ? admin.UserName : user!.DisplayName, issuedAt,
                issuedAt.AddMinutes(options.Value.UserAuthentication.TokenLifetimeMinutes), scopes,
                Kind: isAdministrator ? ApiCredentialKind.AdministratorSession : ApiCredentialKind.UserSession,
                UserId: user?.Id, AuthenticationRevision: user?.AuthenticationRevision,
                AdministratorCredentialBinding: isAdministrator ? AdministratorBinding() : null);
            registry.Register(record);
            if (await ResolveIdentityAsync(record, cancellationToken) is null) {
                await registry.RevokeAsync(record.Id, clock.GetUtcNow(), cancellationToken);
                return null;
            }
            return new(ApiJwtTokenWriter.Write(options.Value.Authorization, record), "Bearer", record.ExpiresAtUtc,
                record.UserId, record.DisplayName, record.Scopes, isAdministrator);
        } finally {
            hashAdmission.Release();
        }
    }

    public async Task<ApiSessionIdentity?> ResolveIdentityAsync(ApiTokenRecord record, CancellationToken cancellationToken = default) {
        if (!options.Value.UserAuthentication.Enabled || record.GetStatus(clock.GetUtcNow()) != ApiTokenStatus.Active) {
            return null;
        }
        if (record.Kind == ApiCredentialKind.AdministratorSession) {
            if (record.Subject != ApiBootstrapAdminOptions.Subject || record.AdministratorCredentialBinding is null ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(record.AdministratorCredentialBinding), Encoding.UTF8.GetBytes(AdministratorBinding())) ||
                !record.Scopes.Order(StringComparer.Ordinal).SequenceEqual(new[] { ApiAccessScopeNames.ManageAccess, ApiAccessScopeNames.Session }, StringComparer.Ordinal)) {
                return null;
            }
            return new(null, options.Value.BootstrapAdmin.UserName, record.DisplayName, true, record.Scopes);
        }
        if (record.Kind != ApiCredentialKind.UserSession || record.UserId is not { } id || record.Subject != UserSubject(id)) {
            return null;
        }
        var user = (await users.ReadAsync(cancellationToken)).FirstOrDefault(candidate => candidate.Id == id);
        if (user?.Enabled != true || user.AuthenticationRevision != record.AuthenticationRevision ||
            user.NormalizedUserName == ApiIdentityRules.NormalizeUserName(options.Value.BootstrapAdmin.UserName) ||
            !ApiPasswordService.IsSupportedHash(user.PasswordHash)) {
            return null;
        }
        var expectedScopes = ApiScopeCatalog.ValidateGrants(user.AllowedScopes, forUser: true).Append(ApiAccessScopeNames.Session).Order(StringComparer.Ordinal);
        return record.Scopes.Order(StringComparer.Ordinal).SequenceEqual(expectedScopes, StringComparer.Ordinal)
            ? new(user.Id, user.UserName, user.DisplayName, false, record.Scopes) : null;
    }

    private string AdministratorBinding() => Convert.ToHexString(HMACSHA256.HashData(
        Encoding.UTF8.GetBytes(options.Value.Authorization.SigningKey),
        Encoding.UTF8.GetBytes(ApiIdentityRules.NormalizeUserName(options.Value.BootstrapAdmin.UserName) + "\0" + options.Value.BootstrapAdmin.PasswordHash)));

    private static string UserSubject(Guid id) => $"api-user:{id:N}";

    public void Dispose() => hashAdmission.Dispose();
}
