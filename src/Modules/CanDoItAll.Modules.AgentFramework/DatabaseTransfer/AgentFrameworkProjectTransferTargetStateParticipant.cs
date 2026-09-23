using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class AgentFrameworkProjectTransferTargetStateParticipant(ProjectTransferTargetInspectionRunner inspections)
    : IProjectTransferTargetStateParticipant {
    public ProjectTransferTargetStateArea Area => ProjectTransferTargetStateArea.AgentFramework;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } = [
        typeof(WorkflowLaunchIdempotencyRecordEntity), typeof(WorkflowRunRecordEntity), typeof(WorkflowUsageObservationRecordEntity),
        typeof(WorkflowStructureOutputRecord), typeof(AgentProjectStructureAccessRevocationRecord), typeof(AgentHistoryLocator)
    ];

    public async Task<IReadOnlyList<ProjectTransferTargetStateResidue>> FindResiduesAsync(
        ProjectTransferTargetInspection request, CancellationToken cancellationToken) {
        var residues = new List<ProjectTransferTargetStateResidue>();
        residues.AddRange(await inspections.ReadOwnerAsync<WorkflowDbContext, IReadOnlyList<ProjectTransferTargetStateResidue>>(
            request, static options => new(options), ReadWorkflowResiduesAsync, cancellationToken));
        if (await inspections.ReadOwnerAsync<AgentProjectAccessDbContext, bool>(request, static options => new(options),
                static (db, token) => db.Set<AgentProjectStructureAccessRevocationRecord>().AsNoTracking().AnyAsync(token), cancellationToken)) {
            residues.Add(new("project access revocation recoveries"));
        }
        if (await inspections.ReadOwnerAsync<AgentHistoryDbContext, bool>(request, static options => new(options),
                static (db, token) => db.Set<AgentHistoryLocator>().AsNoTracking().AnyAsync(item => item.ProjectId.HasValue, token), cancellationToken)) {
            residues.Add(new("project-linked canonical agent history"));
        }
        return residues;
    }

    private static async Task<IReadOnlyList<ProjectTransferTargetStateResidue>> ReadWorkflowResiduesAsync(
        WorkflowDbContext db, CancellationToken cancellationToken) {
        var residues = new List<ProjectTransferTargetStateResidue>();
        if (await db.Set<WorkflowRunRecordEntity>().AsNoTracking().AnyAsync(item =>
                item.OriginProjectId.HasValue || item.OriginKind == WorkflowLaunchOriginKind.ProjectStructureNode, cancellationToken)) {
            residues.Add(new("agent workflow runs linked to projects"));
        }
        if (await db.Set<WorkflowLaunchIdempotencyRecordEntity>().AsNoTracking().AnyAsync(item =>
                item.OriginKind == WorkflowLaunchOriginKind.ProjectStructureNode, cancellationToken)) {
            residues.Add(new("project structure workflow launch claims"));
        }
        if (await db.Set<WorkflowUsageObservationRecordEntity>().AsNoTracking().AnyAsync(item =>
                item.OriginKind == WorkflowLaunchOriginKind.ProjectStructureNode, cancellationToken)) {
            residues.Add(new("project structure workflow usage observations"));
        }
        if (await db.Set<WorkflowStructureOutputRecord>().AsNoTracking().AnyAsync(cancellationToken)) {
            residues.Add(new("retained workflow structure output manifests"));
        }
        return residues;
    }
}
