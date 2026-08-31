using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentHistoryTransferParticipant : IHistoryTransferParticipant {
    public HistorySourceKind Kind => HistorySourceKind.AgentConversation;

    public async Task ValidateTargetAsync(DatabaseTransferContext context, CancellationToken cancellationToken) {
        if (await context.TargetDbContext.Set<AgentHistoryLocator>().AnyAsync(cancellationToken)) {
            throw new InvalidOperationException("History transfer cannot replace retained canonical file locators.");
        }
    }

    public async Task<int> CopyAsync(DatabaseTransferContext context, CancellationToken cancellationToken) {
        Guid? partitionCursor = null;
        var evidenceCursor = Guid.Empty;
        var count = 0;
        while (true) {
            var query = context.SourceDbContext.Set<AgentHistoryLocator>().AsNoTracking();
            if (partitionCursor is { } partition) {
                query = query.Where(row => row.PartitionId.CompareTo(partition) > 0 ||
                    row.PartitionId == partition && row.EvidenceId.CompareTo(evidenceCursor) > 0);
            }
            var page = await query.OrderBy(row => row.PartitionId).ThenBy(row => row.EvidenceId)
                .Take(500).ToArrayAsync(cancellationToken);
            if (page.Length == 0) {
                return count;
            }
            var projects = page.Where(row => !row.IsDeleted && row.ProjectId.HasValue)
                .Select(row => row.ProjectId!.Value).Distinct().ToArray();
            var existing = await context.TargetDbContext.Set<Project>().AsNoTracking()
                .Where(row => projects.Contains(row.Id)).Select(row => row.Id).ToArrayAsync(cancellationToken);
            if (existing.Length != projects.Length) {
                throw new InvalidOperationException("Transfer the owning projects before their canonical file history.");
            }
            context.TargetDbContext.AddRange(page);
            await context.TargetDbContext.SaveChangesAsync(cancellationToken);
            context.TargetDbContext.ChangeTracker.Clear();
            partitionCursor = page[^1].PartitionId;
            evidenceCursor = page[^1].EvidenceId;
            count = checked(count + page.Length);
        }
    }
}
