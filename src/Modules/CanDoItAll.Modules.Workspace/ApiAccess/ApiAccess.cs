using System.Security.Cryptography;
using System.ComponentModel;
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

    public ApiUserAuthenticationOptions UserAuthentication { get; set; } = new();

    public ApiAccessManagementOptions AccessManagement { get; set; } = new();

    public ApiBootstrapAdminOptions BootstrapAdmin { get; set; } = new();

    public static IReadOnlyList<string> Validate(ApiAccessOptions options)
    {
        var errors = new List<string>();
        if (options.Authorization is null || options.UserAuthentication is null || options.AccessManagement is null || options.BootstrapAdmin is null) {
            return ["API authorization, user authentication, access management and bootstrap configuration are required."];
        }
        if (options.UserAuthentication.Enabled && (!options.Enabled || !options.Authorization.Enabled)) {
            errors.Add("API user authentication requires the main API and JWT authorization enabled.");
        }
        if (options.AccessManagement.Enabled && !options.UserAuthentication.Enabled) {
            errors.Add("HTTP access management requires API user authentication enabled.");
        }
        if (options.UserAuthentication.Enabled && (options.UserAuthentication.TokenLifetimeMinutes <= 0 ||
            options.UserAuthentication.TokenLifetimeMinutes > options.Authorization.MaxTokenLifetimeMinutes)) {
            errors.Add("The user session lifetime must be positive and within the configured maximum token lifetime.");
        }
        try {
            ApiIdentityRules.UserName(options.BootstrapAdmin.UserName);
        } catch (ArgumentException) {
            errors.Add("Api:BootstrapAdmin:UserName is invalid.");
        }
        if (options.UserAuthentication.Enabled && !ApiPasswordService.IsSupportedHash(options.BootstrapAdmin.PasswordHash, requireCurrentWorkFactor: true)) {
            errors.Add("Api:BootstrapAdmin:PasswordHash must be a supported Identity V3 SHA512 hash with at least 220000 iterations.");
        }
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
    public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(Math.Min(HeartbeatIntervalSeconds, 15));
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
    int MaxTokenLifetimeMinutes) {
    [Description("Whether login, current-session and logout routes are enabled on this host.")]
    public bool UserAuthenticationEnabled { get; init; }
    [Description("Whether HTTP account and token administration is exposed; a registered administrator session is still required.")]
    public bool AccessManagementEnabled { get; init; }
    [Description("Configured lifetime in minutes for newly issued user and administrator sessions.")]
    public int UserTokenLifetimeMinutes { get; init; } = 60;
}

/// <summary>
/// Request of <c>POST /api/access/tokens</c>: who the new bearer token represents, how long it lives and which scopes
/// it grants. Every member is optional; an omitted member takes the default stated on it. An explicit null
/// <c>subject</c> is rejected; a null <c>displayName</c> uses <c>API client</c>.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
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
    /// null or blank means <c>API client</c>.
    /// </summary>
    public string DisplayName { get; set; } = "API client";

    /// <summary>
    /// Token lifetime in minutes, from 1 through the configured maximum (<c>maxTokenLifetimeMinutes</c> of
    /// <c>GET /api/access/status</c>). Omitted or null means the configured default lifetime.
    /// </summary>
    public int? LifetimeMinutes { get; set; }

    /// <summary>
    /// Catalog scopes to grant, for example <c>api</c> or <c>api.memory-providers.read</c>. Values are canonicalized
    /// case-insensitively and duplicates are merged; at least one must remain. Unknown, blank and reserved session
    /// or administration capabilities are rejected. Omitted means <c>["api"]</c>.
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
            value.Authorization.MaxTokenLifetimeMinutes) {
            UserAuthenticationEnabled = value.UserAuthentication.Enabled,
            AccessManagementEnabled = value.AccessManagement.Enabled,
            UserTokenLifetimeMinutes = value.UserAuthentication.TokenLifetimeMinutes
        };
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

        var record = new ApiTokenRecord(tokenId, subject, displayName, issuedAt, expiresAt, scopes);
        var token = ApiJwtTokenWriter.Write(value.Authorization, record);
        registry.Register(record);

        return new ApiTokenIssueResult(
            token,
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
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Token subject is required.");
        }

        if (normalized.Length > 128 || normalized.Any(char.IsControl)) {
            throw new InvalidOperationException("Token subject must contain at most 128 characters without control characters.");
        }
        return normalized;
    }

    private static string NormalizeDisplayName(string value)
    {
        var normalized = value?.Trim();
        if (normalized?.Length > 128 || normalized?.Any(char.IsControl) == true) {
            throw new InvalidOperationException("Token display name must contain at most 128 characters without control characters.");
        }
        return string.IsNullOrWhiteSpace(normalized)
            ? "API client"
            : normalized;
    }

    private static IReadOnlyList<string> NormalizeScopes(IReadOnlyCollection<string>? values)
    {
        var scopes = ApiScopeCatalog.ValidateGrants(values, forUser: false);

        if (scopes.Count == 0) {
            throw new InvalidOperationException("Select at least one API scope.");
        }
        return scopes;
    }

}
