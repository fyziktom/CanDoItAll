using System.ComponentModel.DataAnnotations;

namespace CanDoItAll.Modules.Collaboration;

public sealed class CollaborationThreadEditorModel {
    [Required]
    [MaxLength(240)]
    public string Subject { get; set; } = string.Empty;

    public CollaborationContextKind ContextKind { get; set; } = CollaborationContextKind.Manual;

    [MaxLength(200)]
    public string ContextLabel { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ContextRoute { get; set; }

    public CollaborationInboxItemKind ItemKind { get; set; } = CollaborationInboxItemKind.Notification;

    [Required]
    public string MessageBody { get; set; } = string.Empty;
}

public sealed class CollaborationReplyEditorModel {
    public CollaborationMessageKind MessageKind { get; set; } = CollaborationMessageKind.Standard;

    [Required]
    public string MessageBody { get; set; } = string.Empty;
}

public sealed record CollaborationThreadCreateRequest(
    string Subject,
    CollaborationContextKind ContextKind,
    Guid? ContextId,
    Guid? ProjectId,
    string ContextLabel,
    string? ContextRoute,
    CollaborationInboxItemKind ItemKind,
    string ParticipantKey,
    string ParticipantName,
    CollaborationParticipantKind ParticipantKind,
    string MessageBody,
    CollaborationMessageKind MessageKind,
    bool MarkAsUnread = true) {
    public Guid? SubmissionId { get; init; }
}

public sealed record CollaborationMessageWriteRequest(
    Guid ThreadId,
    string AuthorKey,
    string AuthorName,
    CollaborationMessageAuthorKind AuthorKind,
    string MessageBody,
    CollaborationMessageKind MessageKind,
    bool MarkAsUnread = true) {
    public Guid? SubmissionId { get; init; }
    public CollaborationReadObservation? ReadObservation { get; init; }
}

public readonly record struct CollaborationReadObservation(Guid ThreadId, int MessageCount) {
    public static CollaborationReadObservation From(CollaborationThreadDetailModel thread) => new(thread.ThreadId, thread.Messages.Count);
}

public static class CollaborationWriteConflict {
    public const string ReadObservationChanged = "collaboration.read-observation-changed";
}

public sealed record CollaborationAutomationSignalRequest(
    string SourceKey,
    string SourceLabel,
    string Subject,
    string MessageBody,
    CollaborationInboxItemKind ItemKind,
    Guid? ProjectId = null,
    CollaborationContextKind ContextKind = CollaborationContextKind.AutomationSignal,
    Guid? ContextId = null,
    string? ContextLabel = null,
    string? ContextRoute = null);

public sealed record CollaborationInboxItemSummary(
    Guid ItemId,
    Guid ThreadId,
    CollaborationInboxItemKind ItemKind,
    string Title,
    string PreviewText,
    string Route,
    bool IsUnread,
    int UnreadCount,
    DateTimeOffset UpdatedAtUtc);

public sealed record CollaborationThreadSummary(
    Guid ThreadId,
    string Subject,
    CollaborationContextKind ContextKind,
    string ContextLabel,
    CollaborationInboxItemKind ItemKind,
    CollaborationThreadState State,
    DateTimeOffset LastActivityAtUtc,
    int MessageCount);

public sealed record CollaborationParticipantSummary(
    Guid ParticipantId,
    string ParticipantKey,
    string DisplayName,
    CollaborationParticipantKind ParticipantKind,
    string RoleLabel);

public sealed record CollaborationMessageSummary(
    Guid MessageId,
    CollaborationMessageKind MessageKind,
    CollaborationMessageAuthorKind AuthorKind,
    string AuthorName,
    string Body,
    bool RaisesEscalation,
    DateTimeOffset CreatedAtUtc);

public sealed record CollaborationThreadDetailModel(
    Guid ThreadId,
    string Subject,
    CollaborationContextKind ContextKind,
    Guid? ContextId,
    Guid? ProjectId,
    string ContextLabel,
    string? ContextRoute,
    CollaborationInboxItemKind ItemKind,
    CollaborationThreadState State,
    bool IsUnread,
    int UnreadCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastActivityAtUtc,
    IReadOnlyList<CollaborationParticipantSummary> Participants,
    IReadOnlyList<CollaborationMessageSummary> Messages);

public sealed record CollaborationShellState(
    int UnreadCount,
    int EscalationCount);

public enum CollaborationSection { Inbox, Threads, Escalations }

public readonly record struct CollaborationListWindow(CollaborationSection Section, bool UnreadOnly, int Offset) {
    public const int Size = 50;
    public int OffsetFor(CollaborationSection section) => Section == section ? Offset : 0;
}

public sealed record CollaborationListTotals(int Inbox, int Threads, int Escalations, int UnreadInbox, int UnreadEscalations) {
    public int Visible(CollaborationSection section, bool unreadOnly) => section switch {
        CollaborationSection.Threads => Threads,
        CollaborationSection.Escalations => unreadOnly ? UnreadEscalations : Escalations,
        _ => unreadOnly ? UnreadInbox : Inbox
    };
}

public sealed record CollaborationWorkspaceModel(
    IReadOnlyList<CollaborationInboxItemSummary> InboxItems,
    IReadOnlyList<CollaborationThreadSummary> Threads,
    IReadOnlyList<CollaborationInboxItemSummary> Escalations,
    CollaborationThreadDetailModel? SelectedThread,
    CollaborationShellState ShellState) {
    public CollaborationListTotals Totals { get; init; } = new(InboxItems.Count, Threads.Count, Escalations.Count,
        InboxItems.Count(item => item.IsUnread), Escalations.Count(item => item.IsUnread));
}
