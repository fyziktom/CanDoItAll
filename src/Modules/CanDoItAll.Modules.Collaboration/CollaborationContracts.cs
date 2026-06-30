using System.ComponentModel.DataAnnotations;

namespace CanDoItAll.Modules.Collaboration;

public sealed class CollaborationThreadEditorModel
{
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

public sealed class CollaborationReplyEditorModel
{
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
    bool MarkAsUnread = true);

public sealed record CollaborationMessageWriteRequest(
    Guid ThreadId,
    string AuthorKey,
    string AuthorName,
    CollaborationMessageAuthorKind AuthorKind,
    string MessageBody,
    CollaborationMessageKind MessageKind,
    bool MarkAsUnread = true);

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

public sealed record CollaborationWorkspaceModel(
    IReadOnlyList<CollaborationInboxItemSummary> InboxItems,
    IReadOnlyList<CollaborationThreadSummary> Threads,
    IReadOnlyList<CollaborationInboxItemSummary> Escalations,
    CollaborationThreadDetailModel? SelectedThread,
    CollaborationShellState ShellState);
