namespace CanDoItAll.Modules.AgentFramework;

public enum AgentWorkspaceSection {
    Overview,
    Agents,
    SimpleChats,
    Providers,
    RequestHistory,
    Voice,
    FloatingChat,
    Chat,
    Capabilities,
    Governance,
    Diagnostics
}

public static class AgentWorkspaceSectionPolicy {
    public static bool IsHistoryHost(this AgentWorkspaceSection section)
        => section is AgentWorkspaceSection.Providers or AgentWorkspaceSection.RequestHistory;

    public static bool UsesAgentSelection(this AgentWorkspaceSection section)
        => section is AgentWorkspaceSection.Agents or AgentWorkspaceSection.Chat or
            AgentWorkspaceSection.Capabilities or AgentWorkspaceSection.Governance;
}
