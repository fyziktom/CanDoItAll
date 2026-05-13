# OAuth2 Extension Point

## Why It Belongs In The Architecture Now

SaaS plugins such as Office365, Gmail, Figma, Slack, and similar integrations will need OAuth2. If the plugin API ignores OAuth2 now, connection settings and secret handling will likely need breaking changes later.

## What To Add Now

Add contracts and storage shape, not full SaaS implementations:

- provider registration;
- authorization start request/result;
- callback completion request/result;
- token acquisition lease;
- scope records;
- plugin connection auth state;
- redacted health status.

## Storage Rule

Plugins must not persist OAuth access tokens or refresh tokens themselves. The OAuth2 broker owns protected token storage and returns short-lived token leases.

## Suggested Interfaces

```csharp
public sealed record PluginOAuth2ProviderDescriptor(
    string ProviderKey,
    string DisplayName,
    Uri AuthorizationEndpoint,
    Uri TokenEndpoint,
    IReadOnlyList<string> DefaultScopes);

public sealed record PluginOAuth2TokenRequest(
    PluginId PluginId,
    PluginConnectionId ConnectionId,
    string ProviderKey,
    IReadOnlyList<string> Scopes,
    string Purpose);

public sealed record PluginOAuth2TokenLease(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string TokenType);
```

## Phase Boundaries

- `SB16` can create contracts, persistence placeholders, and fake/test provider.
- Real Office365/Gmail/Figma provider implementations should be separate later bundles.
- OAuth2 UI must use the same plugin connection settings page and health-check status.
