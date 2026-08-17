using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Conversations.Shell;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentChatLauncherCompatibilityFacade(
    IFloatingAgentChatCoordinator coordinator,
    IConversationShellLauncher shell) : IAgentChatLauncher
{
    public void ShowCatalog(AgentChatCatalogTab tab = AgentChatCatalogTab.Agents)
    {
        coordinator.ShowCatalog(tab);
        shell.ShowCatalog(
            ConversationCatalogKindFilter.Agents,
            tab == AgentChatCatalogTab.ActiveChats
                ? ConversationCatalogLifecycle.Active
                : ConversationCatalogLifecycle.Available);
    }

    public async Task<ActiveAgentChat> StartNewChatAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        var chat = await coordinator.StartNewChatAsync(agentId, cancellationToken);
        shell.FocusWindow(
            AgentConversationShellContributor.SourceIdentifier,
            AgentConversationShellContributor.BuildWindowId(chat.HandleId));
        return chat;
    }

    public async Task<ActiveAgentChat> OpenChatAsync(
        Guid agentId,
        Guid chatSessionId,
        CancellationToken cancellationToken = default)
    {
        var chat = await coordinator.OpenChatAsync(agentId, chatSessionId, cancellationToken);
        shell.FocusWindow(
            AgentConversationShellContributor.SourceIdentifier,
            AgentConversationShellContributor.BuildWindowId(chat.HandleId));
        return chat;
    }
}
