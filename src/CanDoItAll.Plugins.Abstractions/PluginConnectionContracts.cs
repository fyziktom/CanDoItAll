using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Plugins.Abstractions;

public enum PluginConnectionAuthKind
{
    None,
    ApiKey,
    Basic,
    BearerToken,
    OAuth2,
    Custom
}

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

public sealed record PluginOAuth2Descriptor(
    PluginConnectionKey ConnectionKey,
    Uri AuthorizationEndpoint,
    Uri TokenEndpoint,
    IReadOnlyList<string> Scopes,
    bool UsesPkce = true);

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
