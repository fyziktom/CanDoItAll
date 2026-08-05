using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Resources;

internal sealed class ResourcesProjectTransferTargetStateParticipant
    : IProjectTransferTargetStateParticipant
{
    public ProjectTransferTargetStateArea Area =>
        ProjectTransferTargetStateArea.Resources;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } =
    [
        typeof(ProjectResource)
    ];

    public async Task<IReadOnlyList<ProjectTransferTargetStateResidue>>
        FindResiduesAsync(
            AppDbContext dbContext,
            CancellationToken cancellationToken)
        => await dbContext.Set<ProjectResource>()
            .AsNoTracking()
            .AnyAsync(cancellationToken)
                ? [new("project resources")]
                : [];
}
