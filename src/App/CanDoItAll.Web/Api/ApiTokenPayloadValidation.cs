using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CanDoItAll.Web.Api;

internal static class ApiTokenPayloadValidation {
    public static JsonDocument Read(SecurityToken token) {
        var encoded = token switch {
            JsonWebToken jwt => jwt.EncodedPayload,
            JwtSecurityToken jwt => jwt.RawPayload,
            _ => throw new InvalidDataException("Unsupported API token representation.")
        };
        var document = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(encoded));
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            document.RootElement.EnumerateObject().GroupBy(property => property.Name, StringComparer.Ordinal).Any(group => group.Count() != 1)) {
            document.Dispose();
            throw new InvalidDataException("Duplicate or malformed API token claims.");
        }
        return document;
    }

    public static bool Matches(JsonElement payload, ApiTokenRecord record) {
        if (Text(payload, "sub") != record.Subject || Text(payload, "name") != record.DisplayName ||
            !payload.TryGetProperty("exp", out var expiry) || !expiry.TryGetInt64(out var seconds) || seconds != record.ExpiresAtUtc.ToUnixTimeSeconds() ||
            !payload.TryGetProperty("iat", out var issued) || !issued.TryGetInt64(out var issuedSeconds) || issuedSeconds != record.IssuedAtUtc.ToUnixTimeSeconds() ||
            !payload.TryGetProperty("nbf", out var notBefore) || !notBefore.TryGetInt64(out var notBeforeSeconds) || notBeforeSeconds != issuedSeconds ||
            !payload.TryGetProperty("scopes", out var scopes) || scopes.ValueKind != JsonValueKind.Array ||
            scopes.EnumerateArray().Any(scope => scope.ValueKind != JsonValueKind.String) ||
            !scopes.EnumerateArray().Select(scope => scope.GetString()).Order(StringComparer.Ordinal).SequenceEqual(record.Scopes.Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            Text(payload, "scope") != string.Join(' ', record.Scopes) ||
            payload.TryGetProperty("scp", out _) || payload.TryGetProperty("http://schemas.microsoft.com/identity/claims/scope", out _)) {
            return false;
        }
        if (record.Kind == ApiCredentialKind.Machine) {
            return Text(payload, ApiManagedTokenClaims.Version) == ApiManagedTokenClaims.CurrentVersion &&
                !payload.TryGetProperty(ApiManagedTokenClaims.Kind, out _) && !payload.TryGetProperty(ApiManagedTokenClaims.UserId, out _) &&
                !payload.TryGetProperty(ApiManagedTokenClaims.AuthenticationRevision, out _);
        }
        if (Text(payload, ApiManagedTokenClaims.Version) != ApiManagedTokenClaims.SessionVersion || Text(payload, ApiManagedTokenClaims.Kind) != record.Kind.ToString()) {
            return false;
        }
        return record.Kind == ApiCredentialKind.AdministratorSession
            ? !payload.TryGetProperty(ApiManagedTokenClaims.UserId, out _) && !payload.TryGetProperty(ApiManagedTokenClaims.AuthenticationRevision, out _)
            : Text(payload, ApiManagedTokenClaims.UserId) == record.UserId?.ToString("N") &&
                payload.TryGetProperty(ApiManagedTokenClaims.AuthenticationRevision, out var revision) && revision.TryGetInt64(out var number) && number == record.AuthenticationRevision;
    }

    public static string? Text(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
