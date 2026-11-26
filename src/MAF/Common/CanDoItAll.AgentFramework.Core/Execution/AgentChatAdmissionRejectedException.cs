namespace CanDoItAll.AgentFramework.Core;

public class AgentChatAdmissionRejectedException : InvalidOperationException {
    internal AgentChatAdmissionRejectedException(Guid agentId, Guid? chatSessionId, string message, Exception? cause = null)
        : base(message, cause) {
        if (agentId == Guid.Empty || chatSessionId == Guid.Empty) {
            throw new ArgumentException("The original chat target must be valid.");
        }
        AgentId = agentId;
        ChatSessionId = chatSessionId;
    }

    public Guid AgentId { get; }
    public Guid? ChatSessionId { get; }
}
