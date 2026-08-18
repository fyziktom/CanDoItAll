using CanDoItAll.Conversations.Components;

namespace CanDoItAll.AgentFramework.Components;

public static class ChatMarkdownRenderer
{
    public static string RenderHtml(string? markdown)
        => ConversationMarkdownRenderer.RenderHtml(markdown);
}
