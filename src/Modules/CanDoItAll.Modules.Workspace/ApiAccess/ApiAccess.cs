using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Options;

using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Modules.Workspace.ApiAccess;

public sealed class ApiAccessOptions
{
    public const string SectionName = "Api";
    public const int MinimumSigningKeyBytes = 32;

    public bool Enabled { get; set; } = true;

    public bool OpenApiEnabled { get; set; } = true;

    public bool SwaggerUiEnabled { get; set; } = true;

    public ApiServerSentEventsOptions ServerSentEvents { get; set; } = new();

    public ApiAuthorizationOptions Authorization { get; set; } = new();

    public static IReadOnlyList<string> Validate(ApiAccessOptions options)
    {
        var errors = new List<string>();
        if (options.ServerSentEvents is null)
        {
            errors.Add("Api:ServerSentEvents configuration is required.");
        }
        else if (options.ServerSentEvents.ReplayCapacity <= 0)
        {
            errors.Add("Api:ServerSentEvents:ReplayCapacity must be greater than zero.");
        }

        if (options.ServerSentEvents is not null &&
            options.ServerSentEvents.MaxBatchSize <= 0)
        {
            errors.Add("Api:ServerSentEvents:MaxBatchSize must be greater than zero.");
        }

        if (options.ServerSentEvents is not null &&
            options.ServerSentEvents.MaxBatchSize > options.ServerSentEvents.ReplayCapacity)
        {
            errors.Add("Api:ServerSentEvents:MaxBatchSize cannot exceed ReplayCapacity.");
        }

        if (options.ServerSentEvents is not null &&
            options.ServerSentEvents.HeartbeatIntervalSeconds <= 0)
        {
            errors.Add("Api:ServerSentEvents:HeartbeatIntervalSeconds must be greater than zero.");
        }

        if (options.Authorization.DefaultTokenLifetimeMinutes <= 0)
        {
            errors.Add("Api:Authorization:DefaultTokenLifetimeMinutes must be greater than zero.");
        }

        if (options.Authorization.MaxTokenLifetimeMinutes <= 0)
        {
            errors.Add("Api:Authorization:MaxTokenLifetimeMinutes must be greater than zero.");
        }

        if (options.Authorization.DefaultTokenLifetimeMinutes > options.Authorization.MaxTokenLifetimeMinutes)
        {
            errors.Add("Api:Authorization:DefaultTokenLifetimeMinutes cannot exceed MaxTokenLifetimeMinutes.");
        }

        if (!options.Authorization.Enabled)
        {
            return errors;
        }

        if (string.IsNullOrWhiteSpace(options.Authorization.Issuer))
        {
            errors.Add("Api:Authorization:Issuer is required when authorization is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.Authorization.Audience))
        {
            errors.Add("Api:Authorization:Audience is required when authorization is enabled.");
        }

        var signingKeyLength = Encoding.UTF8.GetByteCount(options.Authorization.SigningKey ?? string.Empty);
        if (signingKeyLength < MinimumSigningKeyBytes)
        {
            errors.Add($"Api:Authorization:SigningKey must be at least {MinimumSigningKeyBytes} UTF-8 bytes when authorization is enabled.");
        }

        return errors;
    }
}

public sealed class ApiServerSentEventsOptions
{
    public int ReplayCapacity { get; set; } = 1024;

    public int MaxBatchSize { get; set; } = 128;

    public int HeartbeatIntervalSeconds { get; set; } = 15;

    [JsonIgnore]
    public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(HeartbeatIntervalSeconds);
}

public sealed class ApiAuthorizationOptions
{
    public bool Enabled { get; set; }

    public string Issuer { get; set; } = "CanDoItAll.Api";

    public string Audience { get; set; } = "CanDoItAll.Api";

    public string SigningKey { get; set; } = string.Empty;

    public int DefaultTokenLifetimeMinutes { get; set; } = 480;

    public int MaxTokenLifetimeMinutes { get; set; } = 1440;
}

/// <summary>
/// HTTP API access configuration of this host, returned by <c>GET /api/access/status</c>. It reflects the host's
/// <c>Api</c> configuration section and never contains the signing key or a token.
/// </summary>
/// <param name="ApiEnabled">
/// True when <c>Api:Enabled</c> is set, which maps the <c>/api</c> route families. The Project Structure
/// (<c>/api/project-structure</c>) and runtime (<c>/api/runtime</c>) routes stay available even when it is false.
/// Always true in a response of <c>GET /api/access/status</c>, which exists only while the API is enabled.
/// </param>
/// <param name="OpenApiEnabled">
/// True when the OpenAPI document is served at <c>/openapi/v1.json</c> and <c>/swagger/v1/swagger.json</c>.
/// </param>
/// <param name="SwaggerUiEnabled">
/// True when the interactive Swagger UI is served at <c>/swagger</c>; false whenever the OpenAPI document is
/// disabled.
/// </param>
/// <param name="AuthorizationEnabled">
/// True when API operations require a bearer token issued by this host (<c>Api:Authorization:Enabled</c>). When
/// false, most operations accept calls without a token and tokens cannot be issued; operations that need an
/// authenticated caller (answering workflow external requests, agent recruiting reviews, storage placement
/// recovery) then always fail with HTTP 401 or 403.
/// </param>
/// <param name="SigningKeyConfigured">
/// True when a token signing key is configured. Only its presence is reported, never its value.
/// </param>
/// <param name="Issuer">Issuer (<c>iss</c> claim) that tokens must carry to be accepted by this host.</param>
/// <param name="Audience">Audience (<c>aud</c> claim) that tokens must carry to be accepted by this host.</param>
/// <param name="DefaultTokenLifetimeMinutes">
/// Lifetime in minutes given to a token issued without <c>lifetimeMinutes</c>.
/// </param>
/// <param name="MaxTokenLifetimeMinutes">Largest <c>lifetimeMinutes</c> accepted when a token is issued.</param>
public sealed record ApiAccessStatus(
    bool ApiEnabled,
    bool OpenApiEnabled,
    bool SwaggerUiEnabled,
    bool AuthorizationEnabled,
    bool SigningKeyConfigured,
    string Issuer,
    string Audience,
    int DefaultTokenLifetimeMinutes,
    int MaxTokenLifetimeMinutes);

/// <summary>
/// Request of <c>POST /api/access/tokens</c>: who the new bearer token represents, how long it lives and which scopes
/// it grants. Every member is optional; an omitted member takes the default stated on it. Do not send null for
/// <c>subject</c> or <c>displayName</c>: an explicit null is not handled and fails the request, so omit the member
/// instead.
/// </summary>
public sealed class ApiTokenIssueRequest
{
    /// <summary>
    /// Subject (<c>sub</c> claim) of the token: the caller identity recorded for the token and used by operations that
    /// scope records to their caller, for example memory-provider operation status. Surrounding whitespace is
    /// removed; a blank value is rejected. Omitted means <c>api-client</c>. Tokens with the same subject share that
    /// identity.
    /// </summary>
    public string Subject { get; set; } = "api-client";

    /// <summary>
    /// Human-readable name of the token, shown in token administration. Surrounding whitespace is removed; omitted or
    /// blank means <c>API client</c>.
    /// </summary>
    public string DisplayName { get; set; } = "API client";

    /// <summary>
    /// Token lifetime in minutes, from 1 through the configured maximum (<c>maxTokenLifetimeMinutes</c> of
    /// <c>GET /api/access/status</c>). Omitted or null means the configured default lifetime.
    /// </summary>
    public int? LifetimeMinutes { get; set; }

    /// <summary>
    /// Scopes to grant, for example <c>api</c> or <c>api.memory-providers.read</c>. Values are trimmed, blank values
    /// are dropped and duplicates that differ only in letter case are merged; at least one scope must remain. Names
    /// are not checked against the known scopes, and operations compare them exactly and case-sensitively. Omitted
    /// means <c>["api"]</c>.
    /// </summary>
    public List<string> Scopes { get; set; } = [ApiAccessScopeNames.Api];
}

/// <summary>
/// A newly issued bearer token, returned once by <c>POST /api/access/tokens</c>. The token value cannot be read again
/// later; treat it as a secret.
/// </summary>
/// <param name="Token">
/// The signed JSON Web Token. Send it as <c>Authorization: Bearer {token}</c>; never put it in a URL or log.
/// </param>
/// <param name="TokenType">Authorization scheme to use with the token; always <c>Bearer</c>.</param>
/// <param name="ExpiresAtUtc">Instant (UTC, with offset) after which the host rejects the token.</param>
/// <param name="Subject">The normalized subject (<c>sub</c> claim) of the token.</param>
/// <param name="DisplayName">The normalized display name of the token.</param>
/// <param name="Scopes">
/// The scopes granted by the token after normalization, sorted alphabetically without regard to letter case.
/// </param>
public sealed record ApiTokenIssueResult(
    string Token,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    string Subject,
    string DisplayName,
    IReadOnlyList<string> Scopes);

public interface IApiTokenService
{
    ApiAccessStatus GetStatus();

    ApiTokenIssueResult IssueToken(ApiTokenIssueRequest request);
}

public sealed class ApiTokenService(
    IOptions<ApiAccessOptions> options,
    IClock clock,
    IApiTokenRegistry registry) : IApiTokenService
{
    private static readonly JsonSerializerOptions JwtJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ApiAccessStatus GetStatus()
    {
        var value = options.Value;
        return new ApiAccessStatus(
            value.Enabled,
            value.OpenApiEnabled,
            value.SwaggerUiEnabled && value.OpenApiEnabled,
            value.Authorization.Enabled,
            !string.IsNullOrWhiteSpace(value.Authorization.SigningKey),
            value.Authorization.Issuer,
            value.Authorization.Audience,
            value.Authorization.DefaultTokenLifetimeMinutes,
            value.Authorization.MaxTokenLifetimeMinutes);
    }

    public ApiTokenIssueResult IssueToken(ApiTokenIssueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var value = options.Value;
        var configurationErrors = ApiAccessOptions.Validate(value);
        if (configurationErrors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", configurationErrors));
        }

        if (!value.Authorization.Enabled)
        {
            throw new InvalidOperationException("API authorization is not enabled.");
        }

        var subject = NormalizeSubject(request.Subject);
        var displayName = NormalizeDisplayName(request.DisplayName);
        var scopes = NormalizeScopes(request.Scopes);
        var issuedAt = clock.GetUtcNow();
        var lifetimeMinutes = ResolveLifetimeMinutes(request.LifetimeMinutes, value.Authorization);
        var expiresAt = issuedAt.AddMinutes(lifetimeMinutes);
        var tokenId = Guid.NewGuid();

        var header = new Dictionary<string, object?>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT"
        };
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = value.Authorization.Issuer,
            ["aud"] = value.Authorization.Audience,
            ["sub"] = subject,
            ["name"] = displayName,
            ["iat"] = ToUnixTimeSeconds(issuedAt),
            ["nbf"] = ToUnixTimeSeconds(issuedAt),
            ["exp"] = ToUnixTimeSeconds(expiresAt),
            [ApiManagedTokenClaims.TokenId] = tokenId.ToString("N"),
            [ApiManagedTokenClaims.Version] = ApiManagedTokenClaims.CurrentVersion,
            ["scope"] = string.Join(' ', scopes),
            ["scopes"] = scopes
        };

        var encodedHeader = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header, JwtJsonOptions));
        var encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload, JwtJsonOptions));
        var unsignedToken = $"{encodedHeader}.{encodedPayload}";
        var signature = Sign(unsignedToken, value.Authorization.SigningKey);

        registry.Register(new ApiTokenRecord(tokenId, subject, displayName, issuedAt, expiresAt, scopes));

        return new ApiTokenIssueResult(
            $"{unsignedToken}.{signature}",
            "Bearer",
            expiresAt,
            subject,
            displayName,
            scopes);
    }

    private static int ResolveLifetimeMinutes(
        int? requestedLifetimeMinutes,
        ApiAuthorizationOptions options)
    {
        var requested = requestedLifetimeMinutes.GetValueOrDefault(options.DefaultTokenLifetimeMinutes);
        if (requested <= 0)
        {
            throw new InvalidOperationException("Token lifetime must be greater than zero minutes.");
        }

        if (requested > options.MaxTokenLifetimeMinutes)
        {
            throw new InvalidOperationException($"Token lifetime cannot exceed {options.MaxTokenLifetimeMinutes} minutes.");
        }

        return requested;
    }

    private static string NormalizeSubject(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Token subject is required.");
        }

        return normalized;
    }

    private static string NormalizeDisplayName(string value)
    {
        var normalized = value.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? "API client"
            : normalized;
    }

    private static IReadOnlyList<string> NormalizeScopes(IReadOnlyCollection<string>? values)
    {
        var scopes = (values ?? [])
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (scopes.Count == 0) {
            throw new InvalidOperationException("Select at least one API scope.");
        }
        return scopes;
    }

    private static long ToUnixTimeSeconds(DateTimeOffset value)
    {
        return value.ToUnixTimeSeconds();
    }

    private static string Sign(string unsignedToken, string signingKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(signingKey);
        var tokenBytes = Encoding.UTF8.GetBytes(unsignedToken);
        using var hmac = new HMACSHA256(keyBytes);
        return Base64UrlEncode(hmac.ComputeHash(tokenBytes));
    }

    private static string Base64UrlEncode(byte[] value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
