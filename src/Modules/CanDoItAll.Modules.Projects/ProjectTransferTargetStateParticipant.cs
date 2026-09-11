using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

internal sealed class ProjectsProjectTransferTargetStateParticipant(ProjectTransferTargetInspectionRunner inspections)
    : IProjectTransferTargetStateParticipant {
    public ProjectTransferTargetStateArea Area =>
        ProjectTransferTargetStateArea.Projects;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } =
    [
        typeof(Project),
        typeof(ProjectHierarchyLink),
        typeof(ProjectOptionSelection),
        typeof(ProjectPhase),
        typeof(ProjectRetirementRecord),
        typeof(ProjectCreationReservationRecord)
    ];

    public Task<IReadOnlyList<ProjectTransferTargetStateResidue>> FindResiduesAsync(
        ProjectTransferTargetInspection request, CancellationToken cancellationToken)
        => inspections.ReadOwnerAsync<ProjectsDbContext, IReadOnlyList<ProjectTransferTargetStateResidue>>(
            request, static options => new(options), ReadResiduesAsync, cancellationToken);

    private static async Task<IReadOnlyList<ProjectTransferTargetStateResidue>> ReadResiduesAsync(
        ProjectsDbContext dbContext, CancellationToken cancellationToken) {
        var hasResidue =
            await dbContext.Set<Project>().AsNoTracking().AnyAsync(cancellationToken) ||
            await dbContext.Set<ProjectHierarchyLink>().AsNoTracking().AnyAsync(cancellationToken) ||
            await dbContext.Set<ProjectOptionSelection>().AsNoTracking().AnyAsync(cancellationToken) ||
            await dbContext.Set<ProjectPhase>().AsNoTracking().AnyAsync(cancellationToken) ||
            await dbContext.Set<ProjectRetirementRecord>().AsNoTracking().AnyAsync(cancellationToken) ||
            await dbContext.Set<ProjectCreationReservationRecord>().AsNoTracking().AnyAsync(cancellationToken);
        return hasResidue
            ? [new("project core records")]
            : [];
    }
}
