namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentChatAdmissionConflictException : InvalidOperationException {
    public AgentChatAdmissionConflictException(Guid agentId, Guid? chatSessionId, Exception conflict)
        : base("The conversation changed before this prompt was stored. Review the current thread before sending again.", conflict) {
        if (agentId == Guid.Empty || chatSessionId == Guid.Empty) {
            throw new ArgumentException("The original chat target must be valid.");
        }
        if (conflict is not (SandboxWorkspaceConcurrencyException or SandboxWorkspacePublicationConflictException)) {
            throw new ArgumentException("Only a direct prepublication conflict is a definite chat admission refusal.", nameof(conflict));
        }
        AgentId = agentId;
        ChatSessionId = chatSessionId;
    }

    public Guid AgentId { get; }
    public Guid? ChatSessionId { get; }
}
