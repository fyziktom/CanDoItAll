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
        var messages = await dbContext.Set<CollaborationMessageRecord>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var participants = await dbContext.Set<CollaborationParticipantRecord>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        inboxItems = inboxItems
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();

        var inboxByThreadId = inboxItems
            .GroupBy(item => item.ThreadId)
            .ToDictionary(group => group.Key, group => group.First());
        var messageCounts = messages
            .GroupBy(item => item.ThreadId)
            .ToDictionary(group => group.Key, group => group.Count());

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
}
