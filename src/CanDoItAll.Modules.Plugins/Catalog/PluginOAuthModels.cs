using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

public enum PluginOAuthConnectionStatusKind
{
    NotConnected,
    Connected,
    ReconnectRequired,
    Error
}

public sealed record PluginOAuthConnectionStatusItem(
    PluginConnectionId ConnectionId,
    PluginId PluginId,
    PluginConnectionKey ConnectionKey,
    PluginOAuthConnectionStatusKind Status,
    string AccountDisplay,
    IReadOnlyList<string> GrantedScopes,
    DateTimeOffset? AccessTokenExpiresAtUtc,
    DateTimeOffset? RefreshTokenExpiresAtUtc,
    string LastErrorCode,
    string LastErrorDescription,
    DateTimeOffset? UpdatedAtUtc);

public sealed record PluginOAuthStartRequest(
    PluginConnectionKey ConnectionKey,
    PluginConnectionId? ConnectionId = null,
    string DisplayName = "",
    string ReturnPath = "/plugins",
    IReadOnlyList<string>? Scopes = null,
    string? RedirectUri = null);

public sealed record PluginOAuthStartResponse(
    PluginConnectionId ConnectionId,
    string AuthorizationUrl,
    string RedirectUri,
    IReadOnlyList<string> Scopes);

public sealed record PluginOAuthDisconnectResponse(
    PluginConnectionId ConnectionId,
    PluginOAuthConnectionStatusKind Status);

internal sealed record PluginOAuthTokenEnvelope
{
    public string ProviderKey { get; init; } = string.Empty;

    public string AccessToken { get; init; } = string.Empty;

    public string RefreshToken { get; init; } = string.Empty;

    public string TokenType { get; init; } = "Bearer";

    public DateTimeOffset AccessTokenExpiresAtUtc { get; init; }

    public DateTimeOffset? RefreshTokenExpiresAtUtc { get; init; }

    public IReadOnlyList<string> Scopes { get; init; } = [];

    public string AccountDisplay { get; init; } = string.Empty;
}
