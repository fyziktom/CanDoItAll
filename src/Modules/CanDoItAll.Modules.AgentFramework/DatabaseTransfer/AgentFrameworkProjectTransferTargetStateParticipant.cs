using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class AgentFrameworkProjectTransferTargetStateParticipant
    : IProjectTransferTargetStateParticipant
{
    public ProjectTransferTargetStateArea Area =>
        ProjectTransferTargetStateArea.AgentFramework;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } =
    [
        typeof(WorkflowLaunchIdempotencyRecordEntity),
        typeof(WorkflowRunRecordEntity),
        typeof(WorkflowUsageObservationRecordEntity),
        typeof(AgentProjectStructureAccessRevocationRecord),
        typeof(AgentHistoryLocator)
    ];

    public async Task<IReadOnlyList<ProjectTransferTargetStateResidue>>
    FindResiduesAsync(
            AppDbContext dbContext,
            CancellationToken cancellationToken)
    {
        var residues = new List<ProjectTransferTargetStateResidue>();
        if (await dbContext.Set<WorkflowRunRecordEntity>()
                .AsNoTracking()
                .AnyAsync(
                    item =>
                        item.OriginProjectId.HasValue ||
                        item.OriginKind ==
                            WorkflowLaunchOriginKind.ProjectStructureNode,
                    cancellationToken))
        {
            residues.Add(new("agent workflow runs linked to projects"));
        }

        if (await dbContext.Set<WorkflowLaunchIdempotencyRecordEntity>()
                .AsNoTracking()
                .AnyAsync(
                    item => item.OriginKind ==
                        WorkflowLaunchOriginKind.ProjectStructureNode,
                    cancellationToken))
        {
            residues.Add(new("project structure workflow launch claims"));
        }

        if (await dbContext.Set<WorkflowUsageObservationRecordEntity>()
                .AsNoTracking()
                .AnyAsync(
                    item => item.OriginKind ==
                        WorkflowLaunchOriginKind.ProjectStructureNode,
                    cancellationToken))
        {
            residues.Add(new("project structure workflow usage observations"));
        }

        if (await dbContext.Set<AgentProjectStructureAccessRevocationRecord>()
                .AsNoTracking()
                .AnyAsync(cancellationToken))
        {
            residues.Add(new("project access revocation recoveries"));
        }

        if (await dbContext.Set<AgentHistoryLocator>()
                .AsNoTracking()
                .AnyAsync(item => item.ProjectId.HasValue, cancellationToken))
        {
            residues.Add(new("project-linked canonical agent history"));
        }

        return residues;
    }
}
