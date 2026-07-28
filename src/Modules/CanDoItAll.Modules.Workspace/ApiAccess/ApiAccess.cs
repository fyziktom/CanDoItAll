using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Options;

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

public sealed class ApiTokenIssueRequest
{
    public string Subject { get; set; } = "api-client";

    public string DisplayName { get; set; } = "API client";

    public int? LifetimeMinutes { get; set; }

    public List<string> Scopes { get; set; } = [ApiAccessScopeNames.Api];
}

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
    IClock clock) : IApiTokenService
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
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["scope"] = string.Join(' ', scopes),
            ["scopes"] = scopes
        };

        var encodedHeader = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header, JwtJsonOptions));
        var encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload, JwtJsonOptions));
        var unsignedToken = $"{encodedHeader}.{encodedPayload}";
        var signature = Sign(unsignedToken, value.Authorization.SigningKey);

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

        return scopes.Count == 0 ? [ApiAccessScopeNames.Api] : scopes;
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
