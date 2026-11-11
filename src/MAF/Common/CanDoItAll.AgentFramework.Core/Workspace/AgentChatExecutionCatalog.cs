using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentChatExecutionSnapshot(
    Guid AgentId,
    Guid? ChatSessionId,
    AgentChatHandleId? ConversationHandleId,
    AgentExecutionActivityStreamId StreamId,
    Guid? ExecutionRunId,
    ActiveAgentChatRunState RunState,
    AgentChatReturnLocation? ReturnLocation,
    DateTimeOffset UpdatedAtUtc,
    long Revision,
    AgentConversationContextBinding? Binding) {
    public AgentChatExecutionCompleted? CompletionNotification { get; init; }
}

public interface IAgentChatExecutionCatalog {
    event EventHandler? Changed;

    IReadOnlyList<AgentChatExecutionSnapshot> Snapshot(AgentChatOwnerId owner, Guid profileId, DatabaseProfileGeneration generation);

    bool TryDismiss(AgentChatOwnerId owner, Guid profileId, DatabaseProfileGeneration generation,
        Guid agentId, Guid chatSessionId);
}
