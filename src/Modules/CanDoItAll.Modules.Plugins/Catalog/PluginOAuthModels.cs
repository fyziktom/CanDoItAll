using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

/// <summary>
/// OAuth state of a plugin connection, as a JSON integer: 0 NotConnected (no token is stored, for example after a
/// disconnect), 1 Connected (a token is stored), 2 ReconnectRequired (the token cannot be refreshed or lacks a
/// required scope; authorize again), 3 Error (the provider or the token exchange reported an error).
/// </summary>
public enum PluginOAuthConnectionStatusKind
{
    NotConnected,
    Connected,
    ReconnectRequired,
    Error
}

/// <summary>
/// OAuth state of one plugin connection, returned by <c>GET /api/plugins/{pluginId}/oauth/status</c>. It never
/// contains tokens.
/// </summary>
/// <param name="ConnectionId">Identifier of the plugin connection.</param>
/// <param name="PluginId">Identifier of the plugin.</param>
/// <param name="ConnectionKey">Key of the connection kind, for example <c>gmail</c>.</param>
/// <param name="Status">
/// OAuth state, as a JSON integer: 0 NotConnected, 1 Connected, 2 ReconnectRequired, 3 Error. A connection whose
/// stored token lacks a scope that the plugin requires is reported as 2 with <c>lastErrorCode</c>
/// <c>oauth-scope-missing</c>.
/// </param>
/// <param name="AccountDisplay">Display text of the connected account; currently always empty.</param>
/// <param name="GrantedScopes">Scopes that the provider granted.</param>
/// <param name="AccessTokenExpiresAtUtc">
/// When the stored access token expires; the server refreshes it when needed. Null when unknown.
/// </param>
/// <param name="RefreshTokenExpiresAtUtc">When the refresh token expires; currently always null.</param>
/// <param name="LastErrorCode">
/// Code of the last error, for example <c>oauth-scope-missing</c> or an error code of the provider; empty when there is
/// none.
/// </param>
/// <param name="LastErrorDescription">Description of the last error; empty when there is none.</param>
/// <param name="UpdatedAtUtc">When the OAuth state last changed; null when unknown.</param>
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

/// <summary>
/// Body of <c>POST /api/plugins/{pluginId}/oauth/start</c>. Only <c>connectionKey</c> is required.
/// </summary>
/// <param name="ConnectionKey">
/// Key of the OAuth connection kind declared by the plugin, for example <c>gmail</c>.
/// </param>
/// <param name="ConnectionId">
/// Identifier (a GUID string) of an existing connection of this plugin to authorize. Omitted or null: the most
/// recently updated connection with this key is reused, or a new connection is created before the authorization
/// starts.
/// </param>
/// <param name="DisplayName">
/// Display name for a connection created by this request; empty means the connection key.
/// </param>
/// <param name="ReturnPath">
/// Path in this web application to return the browser to after the callback, for example <c>/plugins</c> (the
/// default). It must start with a single <c>/</c>; any other value is replaced by <c>/plugins</c>.
/// </param>
/// <param name="Scopes">
/// Scopes to request instead of the plugin's default scopes; omitted, null or empty means the defaults. Requested
/// scopes are not checked against the plugin's scopes.
/// </param>
/// <param name="RedirectUri">
/// Callback address registered with the identity provider, used as given. Omitted: the connection setting
/// <c>redirectUri</c>, or else this host's <c>/api/plugins/oauth/callback</c> address.
/// </param>
public sealed record PluginOAuthStartRequest(
    PluginConnectionKey ConnectionKey,
    PluginConnectionId? ConnectionId = null,
    string DisplayName = "",
    string ReturnPath = "/plugins",
    IReadOnlyList<string>? Scopes = null,
    string? RedirectUri = null);

/// <summary>
/// Result of <c>POST /api/plugins/{pluginId}/oauth/start</c>: the address where the user signs in.
/// </summary>
/// <param name="ConnectionId">Identifier of the connection being authorized, possibly created by this request.</param>
/// <param name="AuthorizationUrl">
/// Address of the identity provider's authorization page, including the one-time <c>state</c> and, when PKCE is used,
/// the code challenge. Open it in the user's browser; the pending authorization expires after 10 minutes and can be
/// completed once.
/// </param>
/// <param name="RedirectUri">Callback address used for this authorization.</param>
/// <param name="Scopes">Scopes requested from the identity provider, sorted and without duplicates.</param>
public sealed record PluginOAuthStartResponse(
    PluginConnectionId ConnectionId,
    string AuthorizationUrl,
    string RedirectUri,
    IReadOnlyList<string> Scopes);

/// <summary>
/// Result of <c>POST /api/plugins/{pluginId}/connections/{connectionId}/oauth/disconnect</c>.
/// </summary>
/// <param name="ConnectionId">Identifier of the disconnected connection.</param>
/// <param name="Status">
/// OAuth state after the disconnect, as a JSON integer: always 0 (NotConnected) on success. The other values are
/// 1 Connected, 2 ReconnectRequired, 3 Error.
/// </param>
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
