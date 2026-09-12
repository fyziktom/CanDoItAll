namespace CanDoItAll.AgentFramework.Core;

public enum AgentChatSessionBlockReason {
    ActiveExecution,
    PendingApproval,
    UnresolvedEffects
}

public sealed class AgentChatSessionBlockedException : InvalidOperationException {
    public AgentChatSessionBlockedException(Guid agentId, Guid chatSessionId, Guid executionRunId,
        AgentChatSessionBlockReason reason) : base(Describe(reason)) {
        if (agentId == Guid.Empty || chatSessionId == Guid.Empty || executionRunId == Guid.Empty) {
            throw new ArgumentException("A blocked chat start requires the original Agent, thread and execution identifiers.");
        }

        AgentId = agentId;
        ChatSessionId = chatSessionId;
        ExecutionRunId = executionRunId;
        Reason = reason;
    }

    public Guid AgentId { get; }
    public Guid ChatSessionId { get; }
    public Guid ExecutionRunId { get; }
    public AgentChatSessionBlockReason Reason { get; }

    private static string Describe(AgentChatSessionBlockReason reason) => reason switch {
        AgentChatSessionBlockReason.ActiveExecution =>
            "This thread already has an active execution. Wait for it to finish before sending a new prompt.",
        AgentChatSessionBlockReason.PendingApproval =>
            "This thread has pending tool approvals. Approve or reject them before sending a new prompt.",
        AgentChatSessionBlockReason.UnresolvedEffects =>
            "This thread has unresolved tool execution. Inspect Runtime details and reconcile the original run before continuing. Use New thread for an independent read; this prompt was not sent and no earlier tool call was retried.",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
    };
}
