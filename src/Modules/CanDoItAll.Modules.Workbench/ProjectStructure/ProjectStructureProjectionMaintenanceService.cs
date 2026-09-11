using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStructureProjectionRepairResult(
    int RemovedSystemManagedNodeCount,
    int RemovedSystemManagedLinkCount,
    int RemovedOrphanLayoutCount) {
    public int TotalRemovedCount => RemovedSystemManagedNodeCount + RemovedSystemManagedLinkCount + RemovedOrphanLayoutCount;
}

public sealed class ProjectStructureProjectionMaintenanceService(
    IDbContextFactory<WorkbenchDbContext> dbContextFactory,
    IEnumerable<IProjectStructureProjectionContributor> projectionContributors,
    IClock clock,
    ProjectWriteAdmissionService projectAdmissions,
    ProjectStructureMutationScopeFactory mutationScopes,
    CoordinatedDatabaseTransaction coordinatedTransaction) {
    private readonly IReadOnlyList<IProjectStructureProjectionContributor> _projectionContributors = projectionContributors.ToList();

    public async Task<ProjectStructureProjectionRepairResult> RepairAsync(
        Guid projectId,
        CancellationToken cancellationToken = default) {
        var admission = await projectAdmissions.CaptureAsync(projectId, cancellationToken)
            ?? throw new ProjectStructureAgentException(404, "ProjectNotFound",
                $"Project '{projectId:D}' was not found.");
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutationScope = await mutationScopes.BeginAsync(dbContext,
            ProjectStructureSerializableMutationScope.ForProject(projectId), cancellationToken, [admission]);
        var validProjectionNodeKeys = await LoadProjectionNodeKeysAsync(dbContext, projectId, cancellationToken);

        var staleNodes = await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId && item.IsSystemManaged)
            .ToListAsync(cancellationToken);
        var staleLinks = await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(item => item.ProjectId == projectId && item.IsSystemManaged)
            .ToListAsync(cancellationToken);
        var orphanLayouts = await dbContext.Set<ProjectStructureProjectionLayoutRecord>()
            .Where(item => item.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        orphanLayouts = orphanLayouts
            .Where(item => !validProjectionNodeKeys.Contains(item.NodeKey))
            .ToList();

        if (staleLinks.Count == 0 && staleNodes.Count == 0 && orphanLayouts.Count == 0) {
            return new ProjectStructureProjectionRepairResult(0, 0, 0);
        }

        if (staleLinks.Count > 0) {
            dbContext.RemoveRange(staleLinks);
        }

        if (staleNodes.Count > 0) {
            dbContext.RemoveRange(staleNodes);
        }

        if (orphanLayouts.Count > 0) {
            dbContext.RemoveRange(orphanLayouts);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await mutationScope.CommitAsync(cancellationToken);

        return new ProjectStructureProjectionRepairResult(
            staleNodes.Count,
            staleLinks.Count,
            orphanLayouts.Count);
    }

    private async Task<HashSet<string>> LoadProjectionNodeKeysAsync(
        WorkbenchDbContext dbContext,
        Guid projectId,
        CancellationToken cancellationToken) {
        using var coordinatedRead = dbContext.Database.IsRelational() && dbContext.Database.CurrentTransaction is not null
            ? coordinatedTransaction.Enter(dbContext)
            : null;
        var context = new ProjectStructureProjectionContext(
            projectId,
            clock.GetUtcNow(),
            new Dictionary<string, ProjectStructureProjectionLayoutRecord>(StringComparer.Ordinal),
            canonicalLinks: await dbContext.Set<ProjectObjectLinkRecord>().AsNoTracking()
                .Where(link => link.ProjectId == projectId && !link.IsSystemManaged).ToListAsync(cancellationToken)) {
            RequiresCoordinatedOwnerReads = coordinatedRead is not null
        };

        foreach (var contributor in _projectionContributors) {
            await contributor.ContributeAsync(context, cancellationToken);
        }

        return context.Nodes
            .Select(item => item.NodeKey)
            .ToHashSet(StringComparer.Ordinal);
    }
}
