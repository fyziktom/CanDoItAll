using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Collaboration;

public sealed partial class CollaborationService
{
    public async Task<Result<Guid>> CreateThreadAsync(
        CollaborationThreadCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return Result<Guid>.Failure(errors);
        }

        var id = request.SubmissionId is { } submitted && submitted != Guid.Empty ? submitted : Guid.NewGuid();
        var ownsCommit = dbContext.Database.CurrentTransaction is null;
        await using var mutation = await EnterThreadMutationAsync(dbContext, id, cancellationToken);
        if (request.SubmissionId.HasValue && await dbContext.Set<CollaborationThreadRecord>()
            .AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken) is { } existing) {
            var same = existing.Subject == request.Subject.Trim() && existing.ContextKind == request.ContextKind &&
                existing.ContextId == request.ContextId && existing.ProjectId == request.ProjectId &&
                await dbContext.Set<CollaborationMessageRecord>().AnyAsync(message => message.Id == id && message.ThreadId == id &&
                    message.AuthorKey == request.ParticipantKey.Trim() && message.Body == request.MessageBody.Trim() && message.Kind == request.MessageKind, cancellationToken);
            return same ? Result<Guid>.Success(id) : Result<Guid>.Failure(SubmissionChanged());
        }

        var now = clock.GetUtcNow();
        logger.LogInformation(
            "Creating collaboration thread for context {ContextKind}/{ContextId}. Subject={Subject}.",
            request.ContextKind,
            request.ContextId,
            request.Subject);
        var thread = new CollaborationThreadRecord
        {
            Id = id,
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
            Id = id,
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

        await dbContext.Set<CollaborationThreadRecord>().AddAsync(thread, cancellationToken);
        await dbContext.Set<CollaborationParticipantRecord>().AddAsync(participant, cancellationToken);
        await dbContext.Set<CollaborationMessageRecord>().AddAsync(message, cancellationToken);
        await dbContext.Set<CollaborationInboxItemRecord>().AddAsync(inboxItem, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitThreadMutationAsync(mutation, cancellationToken);
        logger.LogInformation(
            "Created collaboration thread {ThreadId}. Recording activity mirror next.",
            thread.Id);

        if (ownsCommit)
        {
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
        }
        else
        {
            logger.LogInformation(
                "Skipped collaboration activity mirror for thread {ThreadId} because an ambient transaction is active.",
                thread.Id);
        }
        logger.LogInformation(
            "Finished collaboration thread creation for {ThreadId}.",
            thread.Id);

        NotifyChanged();
        return Result<Guid>.Success(thread.Id);
    }

    public async Task<Result> AppendMessageAsync(
        CollaborationMessageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var errors = ValidateMessageRequest(request);
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }

        var ownsCommit = dbContext.Database.CurrentTransaction is null;
        await using var mutation = await EnterThreadMutationAsync(dbContext, request.ThreadId, cancellationToken);
        var messageId = request.SubmissionId is { } submitted && submitted != Guid.Empty ? submitted : Guid.NewGuid();
        if (request.SubmissionId.HasValue && await dbContext.Set<CollaborationMessageRecord>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == messageId, cancellationToken) is { } existing) {
            return existing.ThreadId == request.ThreadId && existing.AuthorKey == request.AuthorKey.Trim() &&
                existing.AuthorKind == request.AuthorKind && existing.Body == request.MessageBody.Trim() && existing.Kind == request.MessageKind
                ? Result.Success() : Result.Failure(SubmissionChanged());
        }

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
        logger.LogInformation(
            "Appending collaboration message to thread {ThreadId}. Kind={MessageKind}. Author={AuthorName}.",
            request.ThreadId,
            request.MessageKind,
            normalizedAuthorName);

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
                Id = messageId,
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
        await CommitThreadMutationAsync(mutation, cancellationToken);
        logger.LogInformation(
            "Appended collaboration message to thread {ThreadId}. Recording activity mirror next.",
            request.ThreadId);

        if (ownsCommit)
        {
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
                    IdempotencyKey: $"collaboration-message:{messageId:D}"),
                cancellationToken);
        }
        else
        {
            logger.LogInformation(
                "Skipped collaboration activity mirror for thread {ThreadId} message {MessageKind} because an ambient transaction is active.",
                request.ThreadId,
                request.MessageKind);
        }
        logger.LogInformation(
            "Finished collaboration message append for thread {ThreadId}.",
            request.ThreadId);

        NotifyChanged();
        return Result.Success();
    }

    public async Task<Result> MarkThreadAsReadAsync(Guid threadId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutation = await EnterThreadMutationAsync(dbContext, threadId, cancellationToken);
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
        await CommitThreadMutationAsync(mutation, cancellationToken);

        NotifyChanged();
        return Result.Success();
    }

    private static async Task<SerializableMutationScope?> EnterThreadMutationAsync(CollaborationDbContext database, Guid threadId, CancellationToken token) {
        var key = $"collaboration-thread:{threadId:D}";
        if (database.Database.CurrentTransaction is null) {
            return await SerializableMutationScope.BeginAsync(database, key, token);
        }
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(database, [key], token);
        return null;
    }

    private static async Task CommitThreadMutationAsync(SerializableMutationScope? mutation, CancellationToken token) {
        if (mutation is not null) {
            await mutation.CommitAsync(token);
            await mutation.DisposeAsync();
        }
    }

    private static Error SubmissionChanged()
        => Error.Failure("This submission identifier is already associated with a different message or thread.", "collaboration.submission-conflict");

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
}
