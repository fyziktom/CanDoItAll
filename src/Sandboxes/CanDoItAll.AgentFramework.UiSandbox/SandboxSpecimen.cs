namespace CanDoItAll.AgentFramework.UiSandbox;

public enum SandboxSpecimen {
    Catalog,
    Capabilities,
    Overview,
    Governance,
    Diagnostics,
    History,
    SimpleChatDefinitions,
    SimpleChatDefinitionEditor,
    SimpleChatConversation,
    VoiceSettings,
    FloatingChatSettings,
    AgentChat,
    Workflows
}

public static class SandboxSpecimens {
    public static SandboxSpecimen Parse(string? token) => token?.Trim().ToLowerInvariant() switch {
        "capabilities" => SandboxSpecimen.Capabilities,
        "overview" => SandboxSpecimen.Overview,
        "history" => SandboxSpecimen.History,
        "simple-chat-definitions" => SandboxSpecimen.SimpleChatDefinitions,
        "simple-chat-definition-editor" => SandboxSpecimen.SimpleChatDefinitionEditor,
        "simple-chat-conversation" => SandboxSpecimen.SimpleChatConversation,
        "voice-settings" => SandboxSpecimen.VoiceSettings,
        "floating-chat-settings" => SandboxSpecimen.FloatingChatSettings,
        "agent-chat" => SandboxSpecimen.AgentChat,
        "workflows" => SandboxSpecimen.Workflows,
        "diagnostics" => SandboxSpecimen.Diagnostics,
        "governance" => SandboxSpecimen.Governance,
        _ => SandboxSpecimen.Catalog
    };
}
