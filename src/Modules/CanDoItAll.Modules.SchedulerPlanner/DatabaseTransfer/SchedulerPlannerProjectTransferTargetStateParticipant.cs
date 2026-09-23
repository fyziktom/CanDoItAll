using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.SchedulerPlanner;

internal sealed class SchedulerPlannerProjectTransferTargetStateParticipant(ProjectTransferTargetInspectionRunner inspections)
    : IProjectTransferTargetStateParticipant {
    public ProjectTransferTargetStateArea Area =>
        ProjectTransferTargetStateArea.SchedulerPlanner;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } =
    [
        typeof(SchedulerPlan),
        typeof(SchedulerPlanRun),
        typeof(SchedulerFireAdmissionRecord)
    ];

    public Task<IReadOnlyList<ProjectTransferTargetStateResidue>> FindResiduesAsync(
        ProjectTransferTargetInspection request, CancellationToken cancellationToken)
        => inspections.ReadOwnerAsync<SchedulerPlannerDbContext, IReadOnlyList<ProjectTransferTargetStateResidue>>(
            request, static options => new(options), ReadResiduesAsync, cancellationToken);

    private static async Task<IReadOnlyList<ProjectTransferTargetStateResidue>> ReadResiduesAsync(
        SchedulerPlannerDbContext dbContext, CancellationToken cancellationToken) {
        var hasResidue =
            await dbContext.Set<SchedulerPlan>()
                .AsNoTracking()
                .AnyAsync(cancellationToken) ||
            await dbContext.Set<SchedulerFireAdmissionRecord>()
                .AsNoTracking()
                .AnyAsync(cancellationToken) ||
            await dbContext.Set<SchedulerPlanRun>()
                .AsNoTracking()
                .AnyAsync(cancellationToken);
        var retainedAdmissions = await dbContext.Set<SchedulerFireAdmissionRecord>().AsNoTracking().AnyAsync(cancellationToken);
        var residues = new List<ProjectTransferTargetStateResidue>();
        if (retainedAdmissions) {
            residues.Add(new("retained scheduler fire admissions"));
        }
        if (hasResidue) {
            residues.Add(new("scheduler plans or runs with unclassifiable project input"));
        }
        return residues;
    }
}
