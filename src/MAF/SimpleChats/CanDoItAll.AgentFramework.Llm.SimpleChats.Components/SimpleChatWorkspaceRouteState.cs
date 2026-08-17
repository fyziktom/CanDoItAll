namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

public enum SimpleChatWorkspaceView
{
    Conversations,
    Definitions
}

public sealed record SimpleChatWorkspaceRouteState(
    SimpleChatWorkspaceView View,
    Guid? DefinitionId,
    Guid? ConversationId)
{
    public static SimpleChatWorkspaceRouteState Default { get; } = new(
        SimpleChatWorkspaceView.Conversations,
        null,
        null);
}
