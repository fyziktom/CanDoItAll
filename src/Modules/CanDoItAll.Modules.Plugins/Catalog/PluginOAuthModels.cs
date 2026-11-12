using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

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
