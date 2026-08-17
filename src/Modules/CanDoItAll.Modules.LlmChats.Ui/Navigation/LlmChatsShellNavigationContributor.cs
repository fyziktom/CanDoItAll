using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.LlmChats.Ui;

public sealed class LlmChatsShellNavigationContributor : IShellNavigationContributor
{
    private static readonly ShellNavigationContribution ChatsContribution = new(
        ModuleId: "llm-chats",
        ParentRoute: "/agents",
        Item: new ShellNavigationItem(
            "Simple Chats",
            "/chats",
            "chat",
            "Reusable LLM definitions and durable conversations without agent orchestration.",
            PinnedByDefault: false),
        IsSubItem: true,
        Order: 20,
        DesignNote: "Rendered as a flat main menu item beside other conversation runtimes until nested shell navigation is introduced.");

    public IEnumerable<ShellNavigationContribution> GetShellNavigationContributions()
        => [ChatsContribution];
}
