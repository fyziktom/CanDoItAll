namespace CanDoItAll.Modules.AgentFramework.Pages;

public static class AgentWorkspaceTabs
{
    public const string Overview = "overview";
    public const string Agents = "agents";
    public const string SimpleChats = "simple-chats";
    public const string Providers = "providers";
    public const string Voice = "voice";
    public const string FloatingChat = "floating-chat";
    public const string Chat = "chat";
    public const string Capabilities = "capabilities";
    public const string Governance = "governance";
    public const string Diagnostics = "diagnostics";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Overview,
        Agents,
        SimpleChats,
        Providers,
        Voice,
        FloatingChat,
        Chat,
        Capabilities,
        Governance,
        Diagnostics
    };
}
