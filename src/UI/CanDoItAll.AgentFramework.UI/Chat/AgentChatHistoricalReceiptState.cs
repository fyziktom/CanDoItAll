using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.UI.Chat;

public sealed record AgentChatHistoricalReceiptState(AgentHistoricalReceiptReviewRequest Target,
    AgentHistoricalReceiptReview? Page, bool Reading, bool CanRead, string? Error) {
    public string Status => Page?.Stop switch {
        AgentHistoricalReceiptReviewStop.ExhaustedSnapshot => "No further receipt candidates remain in this saved snapshot.",
        AgentHistoricalReceiptReviewStop.LookupLimit => "More saved receipts remain. Read the next page explicitly.",
        AgentHistoricalReceiptReviewStop.CooperativeBudget => "The read budget ended before all saved receipts were examined.",
        AgentHistoricalReceiptReviewStop.Cancelled => "Receipt review was interrupted. Unexamined effects remain unresolved.",
        AgentHistoricalReceiptReviewStop.SnapshotChanged => "The saved run changed. Review again from the beginning; pages were not combined.",
        AgentHistoricalReceiptReviewStop.ReadDenied => "Current access does not allow this receipt review.",
        AgentHistoricalReceiptReviewStop.ReadUnavailable => "Current receipt access could not be verified. This does not prove absence.",
        AgentHistoricalReceiptReviewStop.Unsupported => "This saved run does not support historical receipt review.",
        AgentHistoricalReceiptReviewStop.ContextChanged => "The workspace changed. Refresh this view before reviewing receipts again.",
        _ => "Read existing owner receipts for the selected saved run."
    };

    public static string Describe(AgentHistoricalReceiptKnowledge knowledge) => knowledge switch {
        AgentHistoricalReceiptKnowledge.Accepted => "The owner accepted this original effect.",
        AgentHistoricalReceiptKnowledge.NotObserved => "No receipt was observed; the outcome remains unresolved.",
        AgentHistoricalReceiptKnowledge.ClosedAbsent => "The owner proved no acceptance in the closed retained operation window.",
        AgentHistoricalReceiptKnowledge.Expired => "The receipt window expired; absence is not proven.",
        AgentHistoricalReceiptKnowledge.DatasetChanged => "The original dataset is no longer current; the outcome remains unresolved.",
        AgentHistoricalReceiptKnowledge.ReadDenied => "Current access to this receipt was denied.",
        AgentHistoricalReceiptKnowledge.ReadUnavailable => "Current access to this receipt could not be verified.",
        AgentHistoricalReceiptKnowledge.Unsupported => "This effect does not support historical receipt review.",
        AgentHistoricalReceiptKnowledge.AcceptedDetailUnavailable => "Acceptance is known; receipt details are unavailable.",
        AgentHistoricalReceiptKnowledge.AcceptedDetailDenied => "Acceptance is known; current access to receipt details was denied.",
        _ => throw new ArgumentOutOfRangeException(nameof(knowledge))
    };
}
