using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Plugins.Abstractions;

/// <summary>
/// Authentication method of a plugin connection kind, as a JSON integer: 0 None, 1 ApiKey, 2 Basic, 3 BearerToken,
/// 4 OAuth2, 5 Custom. A plugin must declare the SecretReference capability for 1, 2, 3 and 5, and the OAuth2
/// capability for 4.
/// </summary>
public enum PluginConnectionAuthKind
{
    None,
    ApiKey,
    Basic,
    BearerToken,
    OAuth2,
    Custom
}

/// <summary>
/// A connection kind that a plugin declares: how a saved connection of this kind authenticates and which settings it
/// has.
/// </summary>
/// <param name="Key">Key of the connection kind, for example <c>gmail</c>; saved connections refer to it.</param>
/// <param name="DisplayName">Display name of the connection kind.</param>
/// <param name="Description">Description of the connection kind.</param>
/// <param name="AuthKind">
/// Authentication method, as a JSON integer: 0 None, 1 ApiKey, 2 Basic, 3 BearerToken, 4 OAuth2, 5 Custom.
/// </param>
/// <param name="SettingsSchema">
/// Form of the connection settings (<c>settingsJson</c> of a saved connection).
/// </param>
/// <param name="IsRequired">True when the plugin needs a connection of this kind to work.</param>
public sealed record PluginConnectionDescriptor(
    PluginConnectionKey Key,
    string DisplayName,
    string Description,
    PluginConnectionAuthKind AuthKind,
    ConfigurationSchema SettingsSchema,
    bool IsRequired = false);

public sealed record PluginConnectionSnapshot(
    PluginConnectionId Id,
    PluginConnectionKey Key,
    string DisplayName,
    string SettingsJson);

public static class PluginOAuthConnectionSettingKeys
{
    public const string ClientId = "clientId";
    public const string RedirectUri = "redirectUri";
}

/// <summary>
/// OAuth 2.0 configuration of a plugin, used by <c>POST /api/plugins/{pluginId}/oauth/start</c> and the callback. It
/// contains no secret: the client secret is read on the server from the named environment variable.
/// </summary>
/// <param name="ConnectionKey">Key of the connection kind that uses OAuth, for example <c>gmail</c>.</param>
/// <param name="AuthorizationEndpoint">Authorization endpoint of the identity provider (absolute URL).</param>
/// <param name="TokenEndpoint">Token endpoint of the identity provider (absolute URL).</param>
/// <param name="Scopes">Scopes requested by default and expected on a connected account.</param>
/// <param name="UsesPkce">True when the authorization uses PKCE (proof key for code exchange).</param>
public sealed record PluginOAuth2Descriptor(
    PluginConnectionKey ConnectionKey,
    Uri AuthorizationEndpoint,
    Uri TokenEndpoint,
    IReadOnlyList<string> Scopes,
    bool UsesPkce = true)
{
    /// <summary>
    /// Public OAuth client identifier used when the connection settings do not provide <c>clientId</c>; empty when
    /// every connection must provide its own.
    /// </summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Name of the server environment variable that holds the client secret, for example
    /// <c>CANDOITALL_GMAIL_SECRET</c>. Only the name is exposed, never the secret.
    /// </summary>
    public string ClientSecretEnvironmentVariable { get; init; } = string.Empty;

    /// <summary>
    /// Relative path of the OAuth callback on this host, by default <c>/api/plugins/oauth/callback</c>. The start
    /// operation appends it to the request's base address when neither the request nor the connection settings give
    /// a redirect URI.
    /// </summary>
    public string RedirectPath { get; init; } = "/api/plugins/oauth/callback";

    /// <summary>
    /// Additional query parameters added to the authorization request, for example <c>prompt</c>:
    /// <c>consent</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string> AuthorizationParameters { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record PluginSecretReference(
    PluginConnectionId ConnectionId,
    Guid SecretId,
    string Purpose);

public enum PluginSecretResolutionPurpose
{
    ConnectionSecret,
    WorkflowExecutorSecret,
    SettingsValidation
}
