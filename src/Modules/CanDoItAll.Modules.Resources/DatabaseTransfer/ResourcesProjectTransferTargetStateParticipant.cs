using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Resources;

internal sealed class ResourcesProjectTransferTargetStateParticipant(ProjectTransferTargetInspectionRunner inspections)
    : IProjectTransferTargetStateParticipant {
    public ProjectTransferTargetStateArea Area =>
        ProjectTransferTargetStateArea.Resources;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } =
    [
        typeof(ProjectResource)
    ];

    public Task<IReadOnlyList<ProjectTransferTargetStateResidue>> FindResiduesAsync(
        ProjectTransferTargetInspection request, CancellationToken cancellationToken)
        => inspections.ReadOwnerAsync<ResourcesDbContext, IReadOnlyList<ProjectTransferTargetStateResidue>>(
            request, static options => new(options), ReadResiduesAsync, cancellationToken);

    private static async Task<IReadOnlyList<ProjectTransferTargetStateResidue>> ReadResiduesAsync(
        ResourcesDbContext dbContext, CancellationToken cancellationToken)
        => await dbContext.Set<ProjectResource>()
            .AsNoTracking()
            .AnyAsync(cancellationToken)
                ? [new("project resources")]
                : [];
}
