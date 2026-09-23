using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal sealed class WorkbenchProjectTransferTargetStateParticipant(ProjectTransferTargetInspectionRunner inspections)
    : IProjectTransferTargetStateParticipant {
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
        typeof(ProjectWorkbenchViewStateRecord),
        typeof(ProjectWorkflowContributionRecord),
        typeof(ProjectWorkflowAdmissionRecord),
        typeof(ProjectWorkAssignmentRecord),
        typeof(ProjectWorkAssignmentHistoryRecord),
        typeof(ProjectProcessAssetContributionRecord)
    ];

    public Task<IReadOnlyList<ProjectTransferTargetStateResidue>> FindResiduesAsync(
        ProjectTransferTargetInspection request, CancellationToken cancellationToken)
        => inspections.ReadOwnerAsync<WorkbenchDbContext, IReadOnlyList<ProjectTransferTargetStateResidue>>(
            request, static options => new(options), ReadResiduesAsync, cancellationToken);

    private static async Task<IReadOnlyList<ProjectTransferTargetStateResidue>> ReadResiduesAsync(
        WorkbenchDbContext dbContext, CancellationToken cancellationToken) {
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
        if (hasTransferResidue) {
            residues.Add(new("project workbench records"));
        }

        if (await dbContext.Set<ProjectStructureOperationAnalyticsRecord>()
                .AsNoTracking()
                .AnyAsync(
                    item =>
                        item.ProjectId.HasValue ||
                        item.ScopeKind == ProjectStructureLeaseScopeKind.Project ||
                        item.ScopeKind == ProjectStructureLeaseScopeKind.ProjectNode,
                    cancellationToken)) {
            residues.Add(new("project structure operation analytics"));
        }

        if (await dbContext.Set<ProjectStructureLeaseRecord>()
                .AsNoTracking()
                .AnyAsync(
                    item =>
                        item.ScopeKind == ProjectStructureLeaseScopeKind.Project ||
                        item.ScopeKind == ProjectStructureLeaseScopeKind.ProjectNode,
                    cancellationToken)) {
            residues.Add(new("project structure leases"));
        }

        if (await dbContext.Set<ProjectWorkflowContributionRecord>().AsNoTracking().AnyAsync(cancellationToken)) {
            residues.Add(new("retained workflow contribution receipts"));
        }
        if (await dbContext.Set<ProjectWorkflowAdmissionRecord>().AsNoTracking().AnyAsync(cancellationToken)) {
            residues.Add(new("retained workflow delivery admissions"));
        }
        if (await dbContext.Set<ProjectWorkAssignmentRecord>().AsNoTracking().AnyAsync(cancellationToken)) {
            residues.Add(new("work-item assignments"));
        }
        if (await dbContext.Set<ProjectWorkAssignmentHistoryRecord>().AsNoTracking().AnyAsync(cancellationToken)) {
            residues.Add(new("retained imported Work assignment history"));
        }
        if (await dbContext.Set<ProjectProcessAssetContributionRecord>().AsNoTracking().AnyAsync(cancellationToken)) {
            residues.Add(new("retained Process asset contributions"));
        }
        return residues;
    }
}
