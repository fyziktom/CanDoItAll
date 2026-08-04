using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Collaboration;

internal sealed class CollaborationProjectTransferTargetStateParticipant
    : IProjectTransferTargetStateParticipant
{
    public ProjectTransferTargetStateArea Area =>
        ProjectTransferTargetStateArea.Collaboration;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } =
    [
        typeof(CollaborationThreadRecord)
    ];

    public async Task<IReadOnlyList<ProjectTransferTargetStateResidue>>
        FindResiduesAsync(
            AppDbContext dbContext,
            CancellationToken cancellationToken)
        => await dbContext.Set<CollaborationThreadRecord>()
            .AsNoTracking()
            .AnyAsync(item => item.ProjectId.HasValue, cancellationToken)
                ? [new("collaboration threads linked to projects")]
                : [];
}
