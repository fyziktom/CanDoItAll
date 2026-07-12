using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;

public interface IPluginWorkflowOAuthService
{
    ValueTask<PluginConnectionId> ResolveConnectionIdAsync(
        PluginId pluginId,
        PluginConnectionKey connectionKey,
        string configuredConnectionId,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken = default);

    ValueTask<PluginOAuth2TokenSnapshot> GetAccessTokenAsync(
        PluginId pluginId,
        PluginConnectionId connectionId,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken = default);
}
