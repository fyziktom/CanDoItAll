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
        return await CreateThreadAsync(dbContext, request, cancellationToken);
    }

    public async Task<Result<Guid>> CreateThreadAsync(
        AppDbContext dbContext,
        CollaborationThreadCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return Result<Guid>.Failure(errors);
        }

        var now = clock.GetUtcNow();
        logger.LogInformation(
            "Creating collaboration thread for context {ContextKind}/{ContextId}. Subject={Subject}.",
            request.ContextKind,
            request.ContextId,
            request.Subject);
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

        await dbContext.Set<CollaborationThreadRecord>().AddAsync(thread, cancellationToken);
        await dbContext.Set<CollaborationParticipantRecord>().AddAsync(participant, cancellationToken);
        await dbContext.Set<CollaborationMessageRecord>().AddAsync(message, cancellationToken);
        await dbContext.Set<CollaborationInboxItemRecord>().AddAsync(inboxItem, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Created collaboration thread {ThreadId}. Recording activity mirror next.",
            thread.Id);

        if (dbContext.Database.CurrentTransaction is null)
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
        return await AppendMessageAsync(dbContext, request, cancellationToken);
    }

    public async Task<Result> AppendMessageAsync(
        AppDbContext dbContext,
        CollaborationMessageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var errors = ValidateMessageRequest(request);
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
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
        logger.LogInformation(
            "Appended collaboration message to thread {ThreadId}. Recording activity mirror next.",
            request.ThreadId);

        if (dbContext.Database.CurrentTransaction is null)
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
                    IdempotencyKey: $"collaboration-message:{thread.Id}:{now.ToUnixTimeMilliseconds()}"),
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
}
