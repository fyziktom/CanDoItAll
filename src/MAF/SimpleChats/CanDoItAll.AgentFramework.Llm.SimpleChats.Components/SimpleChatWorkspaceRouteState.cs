namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

public enum SimpleChatWorkspaceView
{
    Definitions,
    Conversations
}

public sealed record SimpleChatWorkspaceRouteState(
    SimpleChatWorkspaceView View,
    Guid? DefinitionId,
    Guid? ConversationId)
{
    public static SimpleChatWorkspaceRouteState Default { get; } = new(
        SimpleChatWorkspaceView.Definitions,
        null,
        null);
}
