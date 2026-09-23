using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentHistoryTransferParticipant(DatabaseTransferOwnerSessionRunner sessions,
    IProjectTransferReferenceQuery projects) : IHistoryTransferParticipant {
    public HistorySourceKind Kind => HistorySourceKind.AgentConversation;

    public async Task ValidateTargetAsync(DatabaseTransferOwnerRequest transfer, CancellationToken cancellationToken) {
        await using var target = await sessions.CreateTargetAsync<AgentHistoryDbContext>(transfer, static options => new AgentHistoryDbContext(options), cancellationToken);
        if (await target.Set<AgentHistoryLocator>().AnyAsync(cancellationToken)) {
            throw new InvalidOperationException("History transfer cannot replace retained canonical file locators.");
        }
    }

    public async Task<int> CopyAsync(DatabaseTransferOwnerRequest transfer, CancellationToken cancellationToken) {
        await using var source = await sessions.CreateSourceAsync<AgentHistoryDbContext>(transfer, static options => new AgentHistoryDbContext(options), cancellationToken);
        await using var target = await sessions.CreateTargetAsync<AgentHistoryDbContext>(transfer, static options => new AgentHistoryDbContext(options), cancellationToken);
        Guid? partitionCursor = null;
        var evidenceCursor = Guid.Empty;
        var count = 0;
        while (true) {
            var query = source.Set<AgentHistoryLocator>().AsNoTracking();
            if (partitionCursor is { } partition) {
                query = query.Where(row => row.PartitionId.CompareTo(partition) > 0 ||
                    row.PartitionId == partition && row.EvidenceId.CompareTo(evidenceCursor) > 0);
            }
            var page = await query.OrderBy(row => row.PartitionId).ThenBy(row => row.EvidenceId)
                .Take(500).ToArrayAsync(cancellationToken);
            if (page.Length == 0) {
                return count;
            }
            var projectIds = page.Where(row => !row.IsDeleted && row.ProjectId.HasValue)
                .Select(row => row.ProjectId!.Value).Distinct().ToArray();
            if (!await projects.TargetContainsAllAsync(transfer, projectIds, cancellationToken)) {
                throw new InvalidOperationException("Transfer the owning projects before their canonical file history.");
            }
            target.AddRange(page);
            await target.SaveChangesAsync(cancellationToken);
            target.ChangeTracker.Clear();
            partitionCursor = page[^1].PartitionId;
            evidenceCursor = page[^1].EvidenceId;
            count = checked(count + page.Length);
        }
    }
}
