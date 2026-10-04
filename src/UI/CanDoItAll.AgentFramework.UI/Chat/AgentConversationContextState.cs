using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.UI.Chat;

public sealed record AgentConversationContextOrigin(Guid Activation, AgentChatHandleId HandleId,
    Guid? SessionId, AgentContextEpochId? Epoch, AgentConversationBindingRevision? Revision);

public sealed record AgentConversationContextState(AgentConversationContextOrigin Origin, string? ContextLabel,
    bool ContextAllowed, bool Detached, string Affinity, string? PendingContext);

public sealed record AgentConversationContextIntent(AgentConversationContextOrigin Origin, AgentConversationContextMode Mode);

public enum AgentFloatingCloseChoice { Cancel, KeepActive, Stop }

public sealed record AgentFloatingCloseState(Guid Origin, AgentChatHandleId HandleId, string AgentName, bool CanStop, bool AwaitingApproval);

public sealed record AgentFloatingCloseIntent(Guid Origin, AgentChatHandleId HandleId, AgentFloatingCloseChoice Choice);

public sealed record AgentActivityState(AgentExecutionOperationId OperationId, string Label, string Tone, string Message, bool HasSequenceGap);
