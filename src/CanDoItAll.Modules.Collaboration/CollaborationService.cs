using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Collaboration;

public sealed class CollaborationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    ILogger<CollaborationService> logger)
{
    private const string LocalOperatorKey = "local-user";
    private const string LocalOperatorName = "Local operator";

    public event EventHandler? Changed;

    public async Task<Result<Guid>> CreateThreadAsync(
        CollaborationThreadCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return Result<Guid>.Failure(errors);
        }

        var now = clock.GetUtcNow();
        var thread = new CollaborationThreadRecord
        {
            Subject = request.Subject.Trim(),
            ContextKind = request.ContextKind,
            ContextId = request.ContextId,
            ProjectId = request.ProjectId,
            ContextLabel = NormalizeContextLabel(request.ContextKind, request.ContextLabel),
            ContextRoute = NormalizeOptionalValue(request.ContextRoute),
            PrimaryItemKind = request.ItemKind,
            State = CollaborationThreadState.Open,
            CreatedAtUtc = now,
            LastActivityAtUtc = now
        };
        var participant = new CollaborationParticipantRecord
        {
            ThreadId = thread.Id,
            ParticipantKind = request.ParticipantKind,
            ParticipantKey = request.ParticipantKey.Trim(),
            DisplayName = request.ParticipantName.Trim(),
            RoleLabel = string.Empty,
            AddedAtUtc = now
        };
        var message = new CollaborationMessageRecord
        {
            ThreadId = thread.Id,
            Kind = request.MessageKind,
            AuthorKind = ResolveAuthorKind(request.ParticipantKind),
            AuthorKey = request.ParticipantKey.Trim(),
            AuthorName = request.ParticipantName.Trim(),
            Body = request.MessageBody.Trim(),
            RaisesEscalation = request.ItemKind == CollaborationInboxItemKind.Escalation || request.MessageKind == CollaborationMessageKind.Escalation,
            CreatedAtUtc = now
        };
        var inboxItem = new CollaborationInboxItemRecord
        {
            ThreadId = thread.Id,
            ItemKind = request.ItemKind,
            Title = request.Subject.Trim(),
            PreviewText = BuildPreviewText(request.MessageBody),
            Route = BuildThreadRoute(thread.Id),
            IsUnread = request.MarkAsUnread,
            UnreadCount = request.MarkAsUnread ? 1 : 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Set<CollaborationThreadRecord>().AddAsync(thread, cancellationToken);
        await dbContext.Set<CollaborationParticipantRecord>().AddAsync(participant, cancellationToken);
        await dbContext.Set<CollaborationMessageRecord>().AddAsync(message, cancellationToken);
        await dbContext.Set<CollaborationInboxItemRecord>().AddAsync(inboxItem, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await RecordActivitySafeAsync(
            new ActivityWriteRequest(
                "Collaboration",
                "ThreadCreated",
                thread.Subject,
                $"{thread.PrimaryItemKind} thread created for {thread.ContextKind}.",
                thread.ProjectId,
                ArtifactKind: "CollaborationThread",
                ArtifactId: thread.Id,
                Route: inboxItem.Route,
                Actor: participant.DisplayName,
                IdempotencyKey: $"collaboration-thread-created:{thread.Id}"),
            cancellationToken);

        NotifyChanged();
        return Result<Guid>.Success(thread.Id);
    }

    public async Task<Result> AppendMessageAsync(
        CollaborationMessageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateMessageRequest(request);
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var thread = await dbContext.Set<CollaborationThreadRecord>()
            .SingleOrDefaultAsync(item => item.Id == request.ThreadId, cancellationToken);
        if (thread is null)
        {
            return Result.Failure(Error.Failure("The collaboration thread does not exist.", "collaboration.thread-not-found"));
        }

        var inboxItem = await dbContext.Set<CollaborationInboxItemRecord>()
            .SingleAsync(item => item.ThreadId == request.ThreadId, cancellationToken);
        var now = clock.GetUtcNow();
        var normalizedAuthorKey = request.AuthorKey.Trim();
        var normalizedAuthorName = request.AuthorName.Trim();

        if (!await dbContext.Set<CollaborationParticipantRecord>()
                .AnyAsync(item => item.ThreadId == request.ThreadId && item.ParticipantKey == normalizedAuthorKey, cancellationToken))
        {
            await dbContext.Set<CollaborationParticipantRecord>().AddAsync(
                new CollaborationParticipantRecord
                {
                    ThreadId = request.ThreadId,
                    ParticipantKind = ResolveParticipantKind(request.AuthorKind),
                    ParticipantKey = normalizedAuthorKey,
                    DisplayName = normalizedAuthorName,
                    RoleLabel = string.Empty,
                    AddedAtUtc = now
                },
                cancellationToken);
        }

        await dbContext.Set<CollaborationMessageRecord>().AddAsync(
            new CollaborationMessageRecord
            {
                ThreadId = request.ThreadId,
                Kind = request.MessageKind,
                AuthorKind = request.AuthorKind,
                AuthorKey = normalizedAuthorKey,
                AuthorName = normalizedAuthorName,
                Body = request.MessageBody.Trim(),
                RaisesEscalation = request.MessageKind == CollaborationMessageKind.Escalation,
                CreatedAtUtc = now
            },
            cancellationToken);

        thread.LastActivityAtUtc = now;
        thread.PrimaryItemKind = request.MessageKind == CollaborationMessageKind.Escalation
            ? CollaborationInboxItemKind.Escalation
            : thread.PrimaryItemKind;

        inboxItem.ItemKind = request.MessageKind == CollaborationMessageKind.Escalation
            ? CollaborationInboxItemKind.Escalation
            : inboxItem.ItemKind;
        inboxItem.PreviewText = BuildPreviewText(request.MessageBody);
        inboxItem.UpdatedAtUtc = now;
        inboxItem.IsUnread = request.MarkAsUnread;
        inboxItem.UnreadCount = request.MarkAsUnread
            ? Math.Max(1, inboxItem.UnreadCount + 1)
            : 0;

        await dbContext.SaveChangesAsync(cancellationToken);

        await RecordActivitySafeAsync(
            new ActivityWriteRequest(
                "Collaboration",
                "MessageAdded",
                thread.Subject,
                $"{request.MessageKind} message appended by {normalizedAuthorName}.",
                thread.ProjectId,
                ArtifactKind: "CollaborationThread",
                ArtifactId: thread.Id,
                Route: inboxItem.Route,
                Actor: normalizedAuthorName,
                IdempotencyKey: $"collaboration-message:{thread.Id}:{now.ToUnixTimeMilliseconds()}"),
            cancellationToken);

        NotifyChanged();
        return Result.Success();
    }

    public async Task<Result> MarkThreadAsReadAsync(Guid threadId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var inboxItem = await dbContext.Set<CollaborationInboxItemRecord>()
            .SingleOrDefaultAsync(item => item.ThreadId == threadId, cancellationToken);
        if (inboxItem is null)
        {
            return Result.Failure(Error.Failure("The collaboration thread inbox item does not exist.", "collaboration.inbox-item-not-found"));
        }

        if (!inboxItem.IsUnread && inboxItem.UnreadCount == 0)
        {
            return Result.Success();
        }

        inboxItem.IsUnread = false;
        inboxItem.UnreadCount = 0;
        inboxItem.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        NotifyChanged();
        return Result.Success();
    }

    public Task<Result<Guid>> RecordAutomationSignalAsync(
        CollaborationAutomationSignalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return CreateThreadAsync(
            new CollaborationThreadCreateRequest(
                request.Subject,
                request.ContextKind,
                request.ContextId,
                request.ProjectId,
                request.ContextLabel ?? request.SourceLabel,
                request.ContextRoute,
                request.ItemKind,
                $"automation:{request.SourceKey.Trim()}",
                request.SourceLabel.Trim(),
                CollaborationParticipantKind.System,
                request.MessageBody,
                request.ItemKind == CollaborationInboxItemKind.Escalation
                    ? CollaborationMessageKind.Escalation
                    : CollaborationMessageKind.System,
                MarkAsUnread: true),
            cancellationToken);
    }

    public async Task<CollaborationWorkspaceModel> GetWorkspaceAsync(
        Guid? selectedThreadId = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var inboxItems = await dbContext.Set<CollaborationInboxItemRecord>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var threads = await dbContext.Set<CollaborationThreadRecord>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var messages = await dbContext.Set<CollaborationMessageRecord>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var participants = await dbContext.Set<CollaborationParticipantRecord>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        inboxItems = inboxItems
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
        threads = threads
            .OrderByDescending(item => item.LastActivityAtUtc)
            .ToList();
        messages = messages
            .OrderBy(item => item.CreatedAtUtc)
            .ToList();
        participants = participants
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var inboxByThreadId = inboxItems.ToDictionary(item => item.ThreadId);
        var messageCounts = messages
            .GroupBy(item => item.ThreadId)
            .ToDictionary(group => group.Key, group => group.Count());

        var threadSummaries = threads
            .Select(thread => new CollaborationThreadSummary(
                thread.Id,
                thread.Subject,
                thread.ContextKind,
                thread.ContextLabel,
                thread.PrimaryItemKind,
                thread.State,
                thread.LastActivityAtUtc,
                messageCounts.GetValueOrDefault(thread.Id)))
            .ToArray();

        var inboxSummaries = inboxItems
            .Select(item => new CollaborationInboxItemSummary(
                item.Id,
                item.ThreadId,
                item.ItemKind,
                item.Title,
                item.PreviewText,
                item.Route,
                item.IsUnread,
                item.UnreadCount,
                item.UpdatedAtUtc))
            .ToArray();

        var effectiveThreadId = selectedThreadId
            ?? inboxSummaries.FirstOrDefault()?.ThreadId
            ?? threadSummaries.FirstOrDefault()?.ThreadId;
        var selectedThread = effectiveThreadId.HasValue
            ? BuildThreadDetail(effectiveThreadId.Value, threads, inboxByThreadId, messages, participants)
            : null;

        return new CollaborationWorkspaceModel(
            inboxSummaries,
            threadSummaries,
            inboxSummaries.Where(item => item.ItemKind == CollaborationInboxItemKind.Escalation).ToArray(),
            selectedThread,
            new CollaborationShellState(
                inboxSummaries.Sum(item => item.UnreadCount),
                inboxSummaries.Count(item => item.ItemKind == CollaborationInboxItemKind.Escalation && item.IsUnread)));
    }

    public async Task<CollaborationShellState> GetShellStateAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var inboxItems = await dbContext.Set<CollaborationInboxItemRecord>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new CollaborationShellState(
            inboxItems.Sum(item => item.UnreadCount),
            inboxItems.Count(item => item.ItemKind == CollaborationInboxItemKind.Escalation && item.IsUnread));
    }

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
