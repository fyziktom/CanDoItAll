using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.AgentFramework.UI.Chat;

public sealed record AgentChatActionOrigin(long Generation, Guid? AgentId, Guid? SessionId, Guid? RunId);

public sealed record AgentChatActionState(AgentChatActionOrigin Origin, string? StartRejection,
    bool CanRecover, bool RecoveryBlocked, bool HasPendingApprovals, bool Busy,
    bool FocusedFloating, bool NewThreadBlocked, bool CanOpenRuntime);

public enum AgentChatAction { RecoverOriginalRun, CancelPendingRun, NewThread, OpenRuntime }

public sealed record AgentChatActionIntent(AgentChatActionOrigin Origin, AgentChatAction Action);

public sealed record AgentImageSelection(long Generation, InputFileChangeEventArgs Files);
