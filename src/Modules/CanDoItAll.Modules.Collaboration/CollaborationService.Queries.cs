using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Collaboration;

public sealed partial class CollaborationService
{
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
        var messageCounts = await dbContext.Set<CollaborationMessageRecord>()
            .AsNoTracking()
            .GroupBy(message => message.ThreadId)
            .Select(group => new { ThreadId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ThreadId, item => item.Count, cancellationToken);

        inboxItems = inboxItems
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();

        var inboxByThreadId = inboxItems
            .GroupBy(item => item.ThreadId)
            .ToDictionary(group => group.Key, group => group.First());
        var threadSummaries = threads
            .OrderByDescending(item => item.LastActivityAtUtc)
            .Select(thread => new CollaborationThreadSummary(
                thread.Id,
                thread.Subject,
                thread.ContextKind,
                thread.ContextLabel,
                inboxByThreadId.GetValueOrDefault(thread.Id)?.ItemKind ?? thread.PrimaryItemKind,
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
        CollaborationThreadDetailModel? selectedThread = null;
        if (effectiveThreadId is { } id && threads.Any(thread => thread.Id == id)) {
            var messages = await dbContext.Set<CollaborationMessageRecord>().AsNoTracking()
                .Where(message => message.ThreadId == id).ToListAsync(cancellationToken);
            var participants = await dbContext.Set<CollaborationParticipantRecord>().AsNoTracking()
                .Where(participant => participant.ThreadId == id).ToListAsync(cancellationToken);
            selectedThread = BuildThreadDetail(id, threads, inboxByThreadId, messages, participants);
        }

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
        return await dbContext.Set<CollaborationInboxItemRecord>()
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new CollaborationShellState(group.Sum(item => item.UnreadCount),
                group.Count(item => item.ItemKind == CollaborationInboxItemKind.Escalation && item.IsUnread)))
            .SingleOrDefaultAsync(cancellationToken) ?? new(0, 0);
    }
}
