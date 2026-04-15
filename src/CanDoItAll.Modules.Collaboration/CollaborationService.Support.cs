using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Collaboration;

public sealed partial class CollaborationService
{
    private static List<Error> ValidateCreateRequest(CollaborationThreadCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<Error>();
        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            errors.Add(Error.Validation("Thread subject is required.", "collaboration.subject-required"));
        }

        if (string.IsNullOrWhiteSpace(request.MessageBody))
        {
            errors.Add(Error.Validation("The first collaboration message is required.", "collaboration.message-required"));
        }

        if (string.IsNullOrWhiteSpace(request.ParticipantKey) || string.IsNullOrWhiteSpace(request.ParticipantName))
        {
            errors.Add(Error.Validation("A participant identity is required.", "collaboration.participant-required"));
        }

        return errors;
    }

    private static List<Error> ValidateMessageRequest(CollaborationMessageWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<Error>();
        if (request.ThreadId == Guid.Empty)
        {
            errors.Add(Error.Validation("A thread id is required.", "collaboration.thread-required"));
        }

        if (string.IsNullOrWhiteSpace(request.AuthorKey) || string.IsNullOrWhiteSpace(request.AuthorName))
        {
            errors.Add(Error.Validation("The message author is required.", "collaboration.author-required"));
        }

        if (string.IsNullOrWhiteSpace(request.MessageBody))
        {
            errors.Add(Error.Validation("The message body is required.", "collaboration.message-required"));
        }

        return errors;
    }

    private static CollaborationThreadDetailModel? BuildThreadDetail(
        Guid threadId,
        IReadOnlyList<CollaborationThreadRecord> threads,
        IReadOnlyDictionary<Guid, CollaborationInboxItemRecord> inboxByThreadId,
        IReadOnlyList<CollaborationMessageRecord> messages,
        IReadOnlyList<CollaborationParticipantRecord> participants)
    {
        var thread = threads.FirstOrDefault(item => item.Id == threadId);
        if (thread is null || !inboxByThreadId.TryGetValue(threadId, out var inboxItem))
        {
            return null;
        }

        var participantSummaries = participants
            .Where(item => item.ThreadId == threadId)
            .Select(item => new CollaborationParticipantSummary(
                item.Id,
                item.ParticipantKey,
                item.DisplayName,
                item.ParticipantKind,
                item.RoleLabel))
            .ToArray();
        var messageSummaries = messages
            .Where(item => item.ThreadId == threadId)
            .Select(item => new CollaborationMessageSummary(
                item.Id,
                item.Kind,
                item.AuthorKind,
                item.AuthorName,
                item.Body,
                item.RaisesEscalation,
                item.CreatedAtUtc))
            .ToArray();

        return new CollaborationThreadDetailModel(
            thread.Id,
            thread.Subject,
            thread.ContextKind,
            thread.ContextId,
            thread.ProjectId,
            thread.ContextLabel,
            thread.ContextRoute,
            inboxItem.ItemKind,
            thread.State,
            inboxItem.IsUnread,
            inboxItem.UnreadCount,
            thread.CreatedAtUtc,
            thread.LastActivityAtUtc,
            participantSummaries,
            messageSummaries);
    }

    private async Task RecordActivitySafeAsync(ActivityWriteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await activityStream.RecordAsync(request, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Collaboration activity write failed for {Action} and artifact {ArtifactId}.",
                request.Action,
                request.ArtifactId);
        }
    }

    private void NotifyChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string BuildPreviewText(string body)
    {
        var normalized = body.Trim().ReplaceLineEndings(" ");
        return normalized.Length <= 160
            ? normalized
            : normalized[..157] + "...";
    }

    private static string BuildThreadRoute(Guid threadId)
    {
        return $"/collaboration?threadId={threadId:D}";
    }

    private static string NormalizeContextLabel(CollaborationContextKind contextKind, string contextLabel)
    {
        var normalized = NormalizeOptionalValue(contextLabel);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return contextKind switch
        {
            CollaborationContextKind.ProcessRun => "Process run",
            CollaborationContextKind.ProcessLaunch => "Process launch",
            CollaborationContextKind.AutomationSignal => "Automation signal",
            _ => "Manual thread"
        };
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static CollaborationMessageAuthorKind ResolveAuthorKind(CollaborationParticipantKind participantKind)
    {
        return participantKind switch
        {
            CollaborationParticipantKind.Agent => CollaborationMessageAuthorKind.Agent,
            CollaborationParticipantKind.Role => CollaborationMessageAuthorKind.Role,
            CollaborationParticipantKind.System => CollaborationMessageAuthorKind.System,
            _ => CollaborationMessageAuthorKind.User
        };
    }

    private static CollaborationParticipantKind ResolveParticipantKind(CollaborationMessageAuthorKind authorKind)
    {
        return authorKind switch
        {
            CollaborationMessageAuthorKind.Agent => CollaborationParticipantKind.Agent,
            CollaborationMessageAuthorKind.Role => CollaborationParticipantKind.Role,
            CollaborationMessageAuthorKind.System => CollaborationParticipantKind.System,
            _ => CollaborationParticipantKind.User
        };
    }

    public static CollaborationThreadCreateRequest CreateManualThreadRequest(CollaborationThreadEditorModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);

        return new CollaborationThreadCreateRequest(
            editor.Subject,
            editor.ContextKind,
            ContextId: null,
            ProjectId: null,
            editor.ContextLabel,
            editor.ContextRoute,
            editor.ItemKind,
            LocalOperatorKey,
            LocalOperatorName,
            CollaborationParticipantKind.User,
            editor.MessageBody,
            editor.ItemKind == CollaborationInboxItemKind.Escalation
                ? CollaborationMessageKind.Escalation
                : CollaborationMessageKind.Standard,
            MarkAsUnread: true);
    }

    public static CollaborationMessageWriteRequest CreateLocalReplyRequest(Guid threadId, CollaborationReplyEditorModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);

        return new CollaborationMessageWriteRequest(
            threadId,
            LocalOperatorKey,
            LocalOperatorName,
            CollaborationMessageAuthorKind.User,
            editor.MessageBody,
            editor.MessageKind,
            MarkAsUnread: false);
    }
}
