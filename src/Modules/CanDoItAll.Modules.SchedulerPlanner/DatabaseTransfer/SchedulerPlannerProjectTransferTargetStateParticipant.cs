using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.SchedulerPlanner;

internal sealed class SchedulerPlannerProjectTransferTargetStateParticipant
    : IProjectTransferTargetStateParticipant
{
    public ProjectTransferTargetStateArea Area =>
        ProjectTransferTargetStateArea.SchedulerPlanner;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } =
    [
        typeof(SchedulerPlan),
        typeof(SchedulerPlanRun)
    ];

    public async Task<IReadOnlyList<ProjectTransferTargetStateResidue>>
        FindResiduesAsync(
            AppDbContext dbContext,
            CancellationToken cancellationToken)
    {
        var hasResidue =
            await dbContext.Set<SchedulerPlan>()
                .AsNoTracking()
                .AnyAsync(cancellationToken) ||
            await dbContext.Set<SchedulerPlanRun>()
                .AsNoTracking()
                .AnyAsync(cancellationToken);
        return hasResidue
            ? [new("scheduler plans or runs with unclassifiable project input")]
            : [];
    }
}
