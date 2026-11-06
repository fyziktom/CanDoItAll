using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Collaboration;

public sealed partial class CollaborationService
{
    public async Task<CollaborationWorkspaceModel> GetWorkspaceAsync(
        Guid? selectedThreadId = null,
        CancellationToken cancellationToken = default,
        CollaborationListWindow window = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(window.Offset);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var inbox = dbContext.Set<CollaborationInboxItemRecord>().AsNoTracking();
        var threads = dbContext.Set<CollaborationThreadRecord>().AsNoTracking();
        var messages = dbContext.Set<CollaborationMessageRecord>().AsNoTracking();
        var filtered = window.UnreadOnly ? inbox.Where(item => item.IsUnread) : inbox;
        var inboxSummaries = await ReadInboxAsync(filtered, window.OffsetFor(CollaborationSection.Inbox), cancellationToken);
        var escalations = await ReadInboxAsync(filtered.Where(item => item.ItemKind == CollaborationInboxItemKind.Escalation),
            window.OffsetFor(CollaborationSection.Escalations), cancellationToken);
        var threadSummaries = await threads
            .OrderByDescending(item => item.LastActivityAtUtc)
            .ThenBy(item => item.Id)
            .Skip(window.OffsetFor(CollaborationSection.Threads))
            .Take(CollaborationListWindow.Size)
            .Select(thread => new CollaborationThreadSummary(
                thread.Id,
                thread.Subject,
                thread.ContextKind,
                thread.ContextLabel,
                inbox.Where(item => item.ThreadId == thread.Id).Select(item => (CollaborationInboxItemKind?)item.ItemKind).FirstOrDefault()
                    ?? thread.PrimaryItemKind,
                thread.State,
                thread.LastActivityAtUtc,
                messages.Count(message => message.ThreadId == thread.Id)))
            .ToArrayAsync(cancellationToken);
        var counts = await inbox.GroupBy(_ => 1).Select(group => new {
            Total = group.Count(),
            UnreadMessages = group.Sum(item => item.UnreadCount),
            UnreadItems = group.Count(item => item.IsUnread),
            Escalations = group.Count(item => item.ItemKind == CollaborationInboxItemKind.Escalation),
            UnreadEscalations = group.Count(item => item.ItemKind == CollaborationInboxItemKind.Escalation && item.IsUnread)
        }).SingleOrDefaultAsync(cancellationToken);
        var threadCount = await threads.CountAsync(cancellationToken);
        var effectiveThreadId = selectedThreadId ?? (window.Section switch {
            CollaborationSection.Threads => threadSummaries.FirstOrDefault()?.ThreadId,
            CollaborationSection.Escalations => escalations.FirstOrDefault()?.ThreadId,
            _ => inboxSummaries.FirstOrDefault()?.ThreadId ?? (window.UnreadOnly ? null : threadSummaries.FirstOrDefault()?.ThreadId)
        });
        CollaborationThreadDetailModel? selectedThread = null;
        if (effectiveThreadId is { } id) {
            var selected = await (from thread in threads
                                  join item in inbox on thread.Id equals item.ThreadId
                                  where thread.Id == id
                                  select new { Thread = thread, Item = item }).SingleOrDefaultAsync(cancellationToken);
            if (selected is not null) {
                var transcript = await messages.Where(message => message.ThreadId == id).OrderBy(message => message.CreatedAtUtc)
                    .ThenBy(message => message.Id).ToArrayAsync(cancellationToken);
                var participants = await dbContext.Set<CollaborationParticipantRecord>().AsNoTracking()
                    .Where(participant => participant.ThreadId == id).ToArrayAsync(cancellationToken);
                selectedThread = BuildThreadDetail(selected.Thread, selected.Item, transcript, participants);
            }
        }

        return new CollaborationWorkspaceModel(inboxSummaries, threadSummaries, escalations, selectedThread,
            new(counts?.UnreadMessages ?? 0, counts?.UnreadEscalations ?? 0)) {
            Totals = new(counts?.Total ?? 0, threadCount, counts?.Escalations ?? 0, counts?.UnreadItems ?? 0, counts?.UnreadEscalations ?? 0)
        };
    }

    private static Task<CollaborationInboxItemSummary[]> ReadInboxAsync(IQueryable<CollaborationInboxItemRecord> source,
        int offset, CancellationToken cancellationToken) => source.OrderByDescending(item => item.UpdatedAtUtc).ThenBy(item => item.Id)
            .Skip(offset).Take(CollaborationListWindow.Size)
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
            .ToArrayAsync(cancellationToken);

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
