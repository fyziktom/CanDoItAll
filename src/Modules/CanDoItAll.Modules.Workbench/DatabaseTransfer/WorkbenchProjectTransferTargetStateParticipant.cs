using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed class WorkbenchProjectTransferTargetStateParticipant
    : IProjectTransferTargetStateParticipant
{
    public ProjectTransferTargetStateArea Area =>
        ProjectTransferTargetStateArea.Workbench;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } =
    [
        typeof(ProjectCrossModuleMutationRecord),
        typeof(ProjectNodeBindingRecord),
        typeof(ProjectNodeLifecycleEventRecord),
        typeof(ProjectNodeReferenceRecord),
        typeof(ProjectObjectLinkRecord),
        typeof(ProjectObjectRecord),
        typeof(ProjectStructureOperationAnalyticsRecord),
        typeof(ProjectStructureProjectionLayoutRecord),
        typeof(ProjectStructureLeaseRecord),
        typeof(ProjectWorkbenchViewStateRecord)
    ];

    public async Task<IReadOnlyList<ProjectTransferTargetStateResidue>>
        FindResiduesAsync(
            AppDbContext dbContext,
            CancellationToken cancellationToken)
    {
        var hasTransferResidue =
            await dbContext.Set<ProjectCrossModuleMutationRecord>().AsNoTracking().AnyAsync(cancellationToken) ||
            await dbContext.Set<ProjectNodeBindingRecord>().AsNoTracking().AnyAsync(cancellationToken) ||
            await dbContext.Set<ProjectNodeLifecycleEventRecord>().AsNoTracking().AnyAsync(cancellationToken) ||
            await dbContext.Set<ProjectNodeReferenceRecord>().AsNoTracking().AnyAsync(cancellationToken) ||
            await dbContext.Set<ProjectObjectLinkRecord>().AsNoTracking().AnyAsync(cancellationToken) ||
            await dbContext.Set<ProjectObjectRecord>().AsNoTracking().AnyAsync(cancellationToken) ||
            await dbContext.Set<ProjectStructureProjectionLayoutRecord>().AsNoTracking().AnyAsync(cancellationToken) ||
            await dbContext.Set<ProjectWorkbenchViewStateRecord>().AsNoTracking().AnyAsync(cancellationToken);
        var residues = new List<ProjectTransferTargetStateResidue>();
        if (hasTransferResidue)
        {
            residues.Add(new("project workbench records"));
        }

        if (await dbContext.Set<ProjectStructureOperationAnalyticsRecord>()
                .AsNoTracking()
                .AnyAsync(
                    item =>
                        item.ProjectId.HasValue ||
                        item.ScopeKind == ProjectStructureLeaseScopeKind.Project ||
                        item.ScopeKind == ProjectStructureLeaseScopeKind.ProjectNode,
                    cancellationToken))
        {
            residues.Add(new("project structure operation analytics"));
        }

        if (await dbContext.Set<ProjectStructureLeaseRecord>()
                .AsNoTracking()
                .AnyAsync(
                    item =>
                        item.ScopeKind == ProjectStructureLeaseScopeKind.Project ||
                        item.ScopeKind == ProjectStructureLeaseScopeKind.ProjectNode,
                    cancellationToken))
        {
            residues.Add(new("project structure leases"));
        }

        return residues;
    }
}
