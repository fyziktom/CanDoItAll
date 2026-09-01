using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal sealed class WebHistoryPrincipalResolver(
    IInteractiveAccessPrincipalProvider interactive,
    IHttpContextAccessor http,
    IAuthorizationService authorization,
    IApiTokenRegistry tokens,
    IOptions<ApiAccessOptions> options,
    TimeProvider clock) {
    private const string AuthorizationRevisionClaim = "auth_rev";

    internal async Task<WebHistoryPrincipal> ResolveAsync(HistoryPermission permission, CancellationToken cancellationToken) {
        var principal = interactive.IsAvailable ? await interactive.GetCurrentAsync(cancellationToken) : http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true && interactive.IsAvailable) {
            principal = await interactive.TryGetTrustedLocalOperatorAsync(cancellationToken);
        }
        if (principal?.Identity?.IsAuthenticated != true) {
            throw Denied();
        }
        var local = interactive.IsAvailable &&
            principal.Identity.AuthenticationType == LocalOperatorAuthenticationStateProvider.AuthenticationType &&
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub) == LocalOperatorAuthenticationStateProvider.ActorId;
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var issuer = principal.FindFirstValue(JwtRegisteredClaimNames.Iss);
        var session = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var expiry = principal.FindFirstValue(JwtRegisteredClaimNames.Exp);
        ApiTokenRecord? credential = null;
        if (!local) {
            if (!options.Value.Authorization.Enabled || issuer != options.Value.Authorization.Issuer ||
                !long.TryParse(expiry, NumberStyles.None, CultureInfo.InvariantCulture, out var expires) ||
                expires <= clock.GetUtcNow().ToUnixTimeSeconds()) {
                throw Denied();
            }
            var version = principal.FindFirstValue(ApiManagedTokenClaims.Version);
            if (version is not null) {
                if (version != ApiManagedTokenClaims.CurrentVersion || !Guid.TryParseExact(session, "N", out var id)) {
                    throw Denied();
                }
                credential = await tokens.FindAsync(id, cancellationToken);
                if (credential is null || credential.GetStatus(clock.GetUtcNow()) != ApiTokenStatus.Active || credential.Subject != subject) {
                    throw Denied();
                }
            }
        }
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 512 || issuer?.Length > 512) {
            throw Denied();
        }
        var revision = long.TryParse(principal.FindFirstValue(AuthorizationRevisionClaim), out var value) ? value : 0;
        var caller = new HistoryCaller(local ? HistoryAuthenticationKind.TrustedLocalOperator
            : credential is null ? HistoryAuthenticationKind.LegacyAuthenticated : HistoryAuthenticationKind.ManagedCredential,
            credential is null ? null : new ManagedCredentialId(credential.Id), issuer, subject,
            local ? "Local operator" : credential?.DisplayName);
        var stamp = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            subject, issuer, session, expiry, revision,
            Scopes = ApiAuthorizationPolicies.ScopeValues(principal).Order(StringComparer.Ordinal).ToArray(),
            CredentialScopes = credential?.Scopes.Order(StringComparer.Ordinal).ToArray(),
            credential?.ExpiresAtUtc,
            credential?.RevokedAtUtc
        }))));
        var resolved = new WebHistoryPrincipal(principal, caller, stamp, revision, local, credential);
        var required = permission switch {
            HistoryPermission.ReadMetadata => (ApiAuthorizationPolicies.ReadProviderHistory, ApiAccessScopeNames.ReadProviderHistory),
            HistoryPermission.ReadContent => (ApiAuthorizationPolicies.ReadProviderHistoryContent, ApiAccessScopeNames.ReadProviderHistoryContent),
            HistoryPermission.Manage => (ApiAuthorizationPolicies.ManageProviderHistory, ApiAccessScopeNames.ManageProviderHistory),
            _ => throw Denied()
        };
        await RequireScopeAsync(resolved, required.Item1, required.Item2, cancellationToken);
        return resolved;
    }

    internal async Task RequireOwnerAsync(WebHistoryPrincipal principal, HistorySourceKind kind, CancellationToken cancellationToken) {
        if (kind == HistorySourceKind.SimpleChat) {
            await RequireScopeAsync(principal, ApiAuthorizationPolicies.ReadLlmChats, ApiAccessScopeNames.ReadLlmChats, cancellationToken);
            return;
        }
        if (!principal.IsLocalOperator || kind is not (HistorySourceKind.AgentConversation or HistorySourceKind.Workflow)) {
            throw Denied();
        }
    }

    private async Task RequireScopeAsync(WebHistoryPrincipal principal, string policy, string scope, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!(await authorization.AuthorizeAsync(principal.Principal, policy)).Succeeded ||
            principal.Credential is { } credential && !credential.Scopes.Contains(scope, StringComparer.Ordinal)) {
            throw Denied();
        }
    }

    private static ProviderHistoryException Denied() =>
        new(HistoryFailure.Denied, "Provider history requires explicit current authority for this operation.");
}

internal sealed record WebHistoryPrincipal(ClaimsPrincipal Principal, HistoryCaller Caller, string Stamp,
    long Revision, bool IsLocalOperator, ApiTokenRecord? Credential);
