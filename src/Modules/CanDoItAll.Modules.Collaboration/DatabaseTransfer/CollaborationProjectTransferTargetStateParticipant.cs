using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Collaboration;

internal sealed class CollaborationProjectTransferTargetStateParticipant(ProjectTransferTargetInspectionRunner inspections)
    : IProjectTransferTargetStateParticipant {
    public ProjectTransferTargetStateArea Area =>
        ProjectTransferTargetStateArea.Collaboration;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } =
    [
        typeof(CollaborationThreadRecord)
    ];

    public Task<IReadOnlyList<ProjectTransferTargetStateResidue>> FindResiduesAsync(
        ProjectTransferTargetInspection request, CancellationToken cancellationToken)
        => inspections.ReadOwnerAsync<CollaborationDbContext, IReadOnlyList<ProjectTransferTargetStateResidue>>(
            request, static options => new(options), ReadResiduesAsync, cancellationToken);

    private static async Task<IReadOnlyList<ProjectTransferTargetStateResidue>> ReadResiduesAsync(
        CollaborationDbContext dbContext, CancellationToken cancellationToken)
        => await dbContext.Set<CollaborationThreadRecord>()
            .AsNoTracking()
            .AnyAsync(item => item.ProjectId.HasValue, cancellationToken)
                ? [new("collaboration threads linked to projects")]
                : [];
}
