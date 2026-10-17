using System.Collections.Immutable;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;

namespace CanDoItAll.Web.Api;

internal sealed class ApiAuthenticationObservation {
    private readonly ImmutableArray<ClaimFact> claims;
    private readonly string? authenticationType;

    private ApiAuthenticationObservation(ClaimsPrincipal principal, string issuer, string subject, long expiresAt,
        ApiTokenRecord? credential, ApiSessionAuthoritySnapshot? session) {
        claims = Facts(principal);
        authenticationType = principal.Identity?.AuthenticationType;
        Issuer = issuer;
        Subject = subject;
        ExpiresAt = expiresAt;
        Credential = credential is null ? null : credential with { Scopes = Array.AsReadOnly(credential.Scopes.ToArray()) };
        Session = session;
        HasProjectsAccess = ApiAuthorizationPolicies.HasApiOrSpecificScope(principal, ApiAccessScopeNames.ReadProjects) &&
            ApiAuthorizationPolicies.HasApiOrSpecificScope(principal, ApiAccessScopeNames.WriteProjects) &&
            (Credential is null || HasRequiredGrants(Credential));
    }

    internal string Issuer { get; }
    internal string Subject { get; }
    internal long ExpiresAt { get; }
    internal ApiTokenRecord? Credential { get; }
    internal ApiSessionAuthoritySnapshot? Session { get; }
    internal bool HasProjectsAccess { get; }

    internal static ApiAuthenticationObservation? TryCapture(ClaimsPrincipal principal, string issuer, JsonElement payload,
        ApiTokenRecord? credential, ApiSessionAuthoritySnapshot? session) {
        // Unsupported shapes retain ordinary authentication but cannot authorize protected definition writes.
        var subject = ApiTokenPayloadValidation.Text(payload, JwtRegisteredClaimNames.Sub);
        if (principal.Identity?.IsAuthenticated != true || principal.Identities.Count() != 1 ||
                string.IsNullOrWhiteSpace(issuer) || issuer.Length > 512 ||
                string.IsNullOrWhiteSpace(subject) || subject.Length > 512 ||
                ApiTokenPayloadValidation.Text(payload, JwtRegisteredClaimNames.Iss) != issuer ||
                !payload.TryGetProperty(JwtRegisteredClaimNames.Exp, out var expiry) || !expiry.TryGetInt64(out var seconds) ||
                !Single(principal, JwtRegisteredClaimNames.Iss, issuer) ||
                !Single(principal, JwtRegisteredClaimNames.Exp, seconds.ToString(CultureInfo.InvariantCulture)) ||
                !principal.Claims.Where(claim => claim.Type is JwtRegisteredClaimNames.Sub or ClaimTypes.NameIdentifier)
                    .Select(claim => claim.Value).SequenceEqual([subject], StringComparer.Ordinal)) {
            return null;
        }
        foreach (var type in new[] { JwtRegisteredClaimNames.Jti, ApiManagedTokenClaims.Version, ApiManagedTokenClaims.Kind,
                ApiManagedTokenClaims.UserId, ApiManagedTokenClaims.AuthenticationRevision }) {
            if (principal.Claims.Count(claim => claim.Type == type) > 1) {
                return null;
            }
        }
        return new(principal, issuer, subject, seconds, credential, session);
    }

    internal bool MatchesPrincipal(ClaimsPrincipal? principal)
        => principal?.Identity?.IsAuthenticated == true && principal.Identities.Count() == 1 &&
            principal.Identity.AuthenticationType == authenticationType && claims.SequenceEqual(Facts(principal));

    internal static bool HasRequiredGrants(ApiTokenRecord credential)
        => credential.Scopes.Contains(ApiAccessScopeNames.ReadProjects, StringComparer.Ordinal) &&
            credential.Scopes.Contains(ApiAccessScopeNames.WriteProjects, StringComparer.Ordinal);

    internal static bool MatchesCredential(ApiTokenRecord original, ApiTokenRecord current)
        => original.Id == current.Id && original.Subject == current.Subject && original.DisplayName == current.DisplayName &&
            original.Kind == current.Kind && original.UserId == current.UserId &&
            original.AuthenticationRevision == current.AuthenticationRevision &&
            original.AdministratorCredentialBinding == current.AdministratorCredentialBinding &&
            original.IssuedAtUtc == current.IssuedAtUtc && original.ExpiresAtUtc == current.ExpiresAtUtc &&
            original.Scopes.SequenceEqual(current.Scopes, StringComparer.Ordinal);

    private static bool Single(ClaimsPrincipal principal, string type, string value)
        => principal.Claims.Where(claim => claim.Type == type).Select(claim => claim.Value).SequenceEqual([value], StringComparer.Ordinal);

    private static ImmutableArray<ClaimFact> Facts(ClaimsPrincipal principal) => principal.Claims
        .Where(claim => claim.Type is JwtRegisteredClaimNames.Sub or ClaimTypes.NameIdentifier or JwtRegisteredClaimNames.Iss or
            JwtRegisteredClaimNames.Aud or JwtRegisteredClaimNames.Exp or JwtRegisteredClaimNames.Iat or JwtRegisteredClaimNames.Nbf or
            JwtRegisteredClaimNames.Jti or ApiManagedTokenClaims.Version or ApiManagedTokenClaims.Kind or ApiManagedTokenClaims.UserId or
            ApiManagedTokenClaims.AuthenticationRevision or "scope" or "scopes" or "scp" or "http://schemas.microsoft.com/identity/claims/scope")
        .Select(claim => new ClaimFact(claim.Type, claim.Value, claim.ValueType, claim.Issuer, claim.OriginalIssuer))
        .OrderBy(claim => claim.Type, StringComparer.Ordinal).ThenBy(claim => claim.Value, StringComparer.Ordinal)
        .ThenBy(claim => claim.ValueType, StringComparer.Ordinal).ThenBy(claim => claim.Issuer, StringComparer.Ordinal)
        .ThenBy(claim => claim.OriginalIssuer, StringComparer.Ordinal).ToImmutableArray();

    private sealed record ClaimFact(string Type, string Value, string ValueType, string Issuer, string OriginalIssuer);
}
