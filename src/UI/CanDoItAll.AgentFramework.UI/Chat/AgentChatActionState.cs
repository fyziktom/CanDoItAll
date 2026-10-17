using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.AgentFramework.UI.Chat;

public sealed record AgentChatActionOrigin(long Generation, Guid? AgentId, Guid? SessionId, Guid? RunId);

public sealed record AgentChatActionState(AgentChatActionOrigin Origin, string? StartRejection,
    bool CanRecover, bool RecoveryBlocked, bool HasPendingApprovals, bool Busy,
    bool FocusedFloating, bool NewThreadBlocked, bool CanOpenRuntime) {
    public AgentChatEffectRecovery? EffectRecovery { get; init; }
    public bool EffectLeaseReleaseUnconfirmed { get; init; }
    public bool EffectActivityFinalizationFailed { get; init; }
    public AgentChatHistoricalReceiptState? HistoricalReceipts { get; init; }
}

public enum AgentChatAction { RecoverOriginalRun, CancelPendingRun, NewThread, OpenRuntime, ReviewSavedReceipts, ReadNextSavedReceipts }

public sealed record AgentChatActionIntent(AgentChatActionOrigin Origin, AgentChatAction Action) {
    public AgentHistoricalReceiptReviewRequest? HistoricalRequest { get; init; }
}

public sealed record AgentImageSelection(long Generation, InputFileChangeEventArgs Files);
