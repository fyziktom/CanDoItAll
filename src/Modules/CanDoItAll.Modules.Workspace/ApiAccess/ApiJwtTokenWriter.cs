using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Modules.Workspace.ApiAccess;

internal static class ApiJwtTokenWriter {
    public static string Write(ApiAuthorizationOptions options, ApiTokenRecord record) {
        var payload = new Dictionary<string, object?> {
            ["iss"] = options.Issuer,
            ["aud"] = options.Audience,
            ["sub"] = record.Subject,
            ["name"] = record.DisplayName,
            ["iat"] = record.IssuedAtUtc.ToUnixTimeSeconds(),
            ["nbf"] = record.IssuedAtUtc.ToUnixTimeSeconds(),
            ["exp"] = record.ExpiresAtUtc.ToUnixTimeSeconds(),
            [ApiManagedTokenClaims.TokenId] = record.Id.ToString("N"),
            [ApiManagedTokenClaims.Version] = record.Kind == ApiCredentialKind.Machine ? ApiManagedTokenClaims.CurrentVersion : ApiManagedTokenClaims.SessionVersion,
            ["scope"] = string.Join(' ', record.Scopes),
            ["scopes"] = record.Scopes
        };
        if (record.Kind != ApiCredentialKind.Machine) {
            payload[ApiManagedTokenClaims.Kind] = record.Kind.ToString();
        }
        if (record.UserId is { } userId) {
            payload[ApiManagedTokenClaims.UserId] = userId.ToString("N");
            payload[ApiManagedTokenClaims.AuthenticationRevision] = record.AuthenticationRevision;
        }
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var body = Encode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var unsigned = $"{header}.{body}";
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.SigningKey), Encoding.UTF8.GetBytes(unsigned));
        return $"{unsigned}.{Encode(signature)}";
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
