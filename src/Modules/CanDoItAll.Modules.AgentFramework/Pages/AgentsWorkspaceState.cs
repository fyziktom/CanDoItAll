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
