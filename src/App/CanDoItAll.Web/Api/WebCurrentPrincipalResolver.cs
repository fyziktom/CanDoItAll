using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal sealed class WebCurrentPrincipalResolver(
    IInteractiveAccessPrincipalProvider interactive,
    IHttpContextAccessor http,
    IAuthorizationService authorization,
    IApiTokenRegistry tokens,
    IOptions<ApiAccessOptions> options,
    TimeProvider clock) {
    private const string AuthorizationRevisionClaim = "auth_rev";

    internal async Task<WebCurrentPrincipal> ResolveAsync(CancellationToken cancellationToken) {
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
        var stamp = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            subject, issuer, session, expiry, revision,
            Scopes = ApiAuthorizationPolicies.ScopeValues(principal).Order(StringComparer.Ordinal).ToArray(),
            CredentialScopes = credential?.Scopes.Order(StringComparer.Ordinal).ToArray(),
            credential?.ExpiresAtUtc,
            credential?.RevokedAtUtc
        }))));
        return new(principal, subject, issuer, stamp, revision, local, credential);
    }

    internal async Task RequireScopeAsync(WebCurrentPrincipal principal, string policy, string scope, CancellationToken cancellationToken) {
        if (!await HasScopeAsync(principal, policy, scope, cancellationToken)) {
            throw Denied();
        }
    }

    internal async Task<bool> HasScopeAsync(WebCurrentPrincipal principal, string policy, string scope, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return (await authorization.AuthorizeAsync(principal.Principal, policy)).Succeeded &&
            (principal.Credential is null || principal.Credential.Scopes.Contains(scope, StringComparer.Ordinal));
    }

    private static WebCurrentPrincipalDeniedException Denied() => new();
}

internal sealed record WebCurrentPrincipal(ClaimsPrincipal Principal, string Subject, string? Issuer,
    string Stamp, long Revision, bool IsLocalOperator, ApiTokenRecord? Credential);

internal sealed class WebCurrentPrincipalDeniedException : InvalidOperationException {
}
