using CanDoItAll.Editing;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

internal sealed class LlmChatConversationMetadataDraft(LlmChatConversationListItem original) {
    private LlmChatConversationListItem? reviewed;
    private string? undo;
    public LlmChatConversationListItem Original { get; private set; } = original;
    public string Title { get; private set; } = original.Title;
    public long Revision { get; private set; }
    public MergeField<string>? Comparison { get; private set; }
    public bool NeedsReview { get; private set; }
    public bool Unconfirmed { get; private set; }
    public bool CanUndo => undo is not null;
    public bool HasChanges => Title != Original.Title;
    public string Notice { get; private set; } = "";
    public string StateInformation => reviewed is { } current
        ? $"Current saved state: {current.Status}. {(current.ActiveOperationId.HasValue ? "A response operation is active." : "")}".TrimEnd()
        : $"Observed state: {Original.Status}.";

    public void SetTitle(string title) {
        if (Title == title) {
            return;
        }
        Title = title;
        Revision++;
        undo = null;
        if (Comparison is not null) {
            Comparison = null;
            reviewed = null;
            NeedsReview = true;
            Notice = "Your title changed. Compare again before applying.";
        }
    }

    public void ObserveHint() {
        Revision++;
        NeedsReview = true;
        Comparison = null;
        reviewed = null;
        undo = null;
        Notice = "Conversation metadata changed. Compare current information before saving.";
    }

    public void Compare(LlmChatConversationListItem current, long revision) {
        if (revision != Revision || current.ConversationId != Original.ConversationId) {
            return;
        }
        reviewed = current;
        Comparison = new(Original.Title, Title, current.Title, StringComparer.Ordinal);
        NeedsReview = true;
        Notice = Unconfirmed ? "The earlier outcome is unconfirmed. Review current state before starting a new conditional command."
            : "Applying changes only your draft. Save or Archive is a separate command.";
    }

    public bool Apply(bool acknowledgeUnknown) {
        if (Comparison is not { IsResolved: true } comparison || reviewed is not { } current || Unconfirmed && !acknowledgeUnknown) {
            return false;
        }
        undo = Title;
        Title = comparison.Result;
        Original = current;
        Comparison = null;
        reviewed = null;
        NeedsReview = false;
        Unconfirmed = false;
        Revision++;
        Notice = current.Status == LlmChatConversationStatus.Archived
            ? "The conversation is archived. The local title is retained and cannot be saved."
            : "Review applied locally. Save or Archive separately.";
        return true;
    }

    public void Undo() {
        if (undo is not { } previous) {
            return;
        }
        Title = previous;
        undo = null;
        ObserveHint();
        Notice = "The previous local title is restored. Compare again before saving.";
    }

    public void Reject(string message) {
        NeedsReview = true;
        Comparison = null;
        reviewed = null;
        Notice = message;
    }

    public void MarkUnknown() {
        Reject("The command outcome is unconfirmed. Keep this draft and compare current state before any new command.");
        Unconfirmed = true;
    }

    public void Acknowledge(LlmChatConversationListItem accepted) {
        Original = accepted;
        Unconfirmed = false;
        undo = null;
        Comparison = null;
        reviewed = null;
        Notice = NeedsReview ? "The submitted title is saved. A later change needs another comparison; your draft is retained."
            : "The submitted title is saved. Your later title draft is retained.";
    }
}
