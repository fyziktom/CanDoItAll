using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.Pages;

public static class AgentWorkspaceSections {
    public static string ToTabKey(this AgentWorkspaceSection section) => section switch {
        AgentWorkspaceSection.Overview => AgentWorkspaceTabs.Overview,
        AgentWorkspaceSection.Agents => AgentWorkspaceTabs.Agents,
        AgentWorkspaceSection.SimpleChats => AgentWorkspaceTabs.SimpleChats,
        AgentWorkspaceSection.Providers => AgentWorkspaceTabs.Providers,
        AgentWorkspaceSection.RequestHistory => AgentWorkspaceTabs.RequestHistory,
        AgentWorkspaceSection.Voice => AgentWorkspaceTabs.Voice,
        AgentWorkspaceSection.FloatingChat => AgentWorkspaceTabs.FloatingChat,
        AgentWorkspaceSection.Chat => AgentWorkspaceTabs.Chat,
        AgentWorkspaceSection.Capabilities => AgentWorkspaceTabs.Capabilities,
        AgentWorkspaceSection.Governance => AgentWorkspaceTabs.Governance,
        AgentWorkspaceSection.Diagnostics => AgentWorkspaceTabs.Diagnostics,
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown Agents section.")
    };

    public static AgentWorkspaceSection FromTabKey(string key) => key switch {
        AgentWorkspaceTabs.Overview => AgentWorkspaceSection.Overview,
        AgentWorkspaceTabs.Agents => AgentWorkspaceSection.Agents,
        AgentWorkspaceTabs.SimpleChats => AgentWorkspaceSection.SimpleChats,
        AgentWorkspaceTabs.Providers => AgentWorkspaceSection.Providers,
        AgentWorkspaceTabs.RequestHistory => AgentWorkspaceSection.RequestHistory,
        AgentWorkspaceTabs.Voice => AgentWorkspaceSection.Voice,
        AgentWorkspaceTabs.FloatingChat => AgentWorkspaceSection.FloatingChat,
        AgentWorkspaceTabs.Chat => AgentWorkspaceSection.Chat,
        AgentWorkspaceTabs.Capabilities => AgentWorkspaceSection.Capabilities,
        AgentWorkspaceTabs.Governance => AgentWorkspaceSection.Governance,
        AgentWorkspaceTabs.Diagnostics => AgentWorkspaceSection.Diagnostics,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown Agents tab key.")
    };

}

public sealed record AgentsWorkspaceState {
    public AgentWorkspaceSection Section { get; init; } = AgentWorkspaceSection.Overview;
    public Guid? AgentId { get; init; }
    public Guid? TeamId { get; init; }
    public SimpleChatWorkspaceRouteState SimpleChat { get; init; } = SimpleChatWorkspaceRouteState.Default;
    public ProviderUsageWorkloadSelection UsageSelection { get; init; } = ProviderUsageWorkloadSelection.Both;
    public AgentChatContextAccessState SelectionAccess { get; init; } = AgentChatContextAccessState.Loading;

    public AgentsWorkspaceState SelectSection(AgentWorkspaceSection section) {
        _ = section.ToTabKey();
        return this with {
            Section = section,
            TeamId = section == AgentWorkspaceSection.Agents ? TeamId : null,
            SelectionAccess = Section == section ? SelectionAccess : InitialAccess(section)
        };
    }

    public AgentsWorkspaceState ApplyRoute(AgentWorkspaceRouteState route) {
        ArgumentNullException.ThrowIfNull(route);
        var section = AgentWorkspaceSections.FromTabKey(route.Tab);
        return this with {
            Section = section,
            AgentId = route.AgentId,
            TeamId = route.TeamId,
            SimpleChat = route.SimpleChat,
            UsageSelection = route.UsageSelection,
            SelectionAccess = Section == section && AgentId == route.AgentId && TeamId == route.TeamId
                ? SelectionAccess
                : InitialAccess(section)
        };
    }

    public AgentWorkspaceRouteState ToRoute()
        => new(Section.ToTabKey(), AgentId, TeamId, SimpleChat, UsageSelection);

    private static AgentChatContextAccessState InitialAccess(AgentWorkspaceSection section)
        => section.UsesAgentSelection() ? AgentChatContextAccessState.Loading : AgentChatContextAccessState.Ready;
}
