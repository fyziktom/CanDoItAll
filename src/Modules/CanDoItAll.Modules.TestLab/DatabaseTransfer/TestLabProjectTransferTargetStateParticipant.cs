using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.TestLab;

internal sealed class TestLabProjectTransferTargetStateParticipant
    : IProjectTransferTargetStateParticipant
{
    public ProjectTransferTargetStateArea Area =>
        ProjectTransferTargetStateArea.TestLab;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } =
    [
        typeof(TestPlan)
    ];

    public async Task<IReadOnlyList<ProjectTransferTargetStateResidue>>
        FindResiduesAsync(
            AppDbContext dbContext,
            CancellationToken cancellationToken)
        => await dbContext.Set<TestPlan>()
            .AsNoTracking()
            .AnyAsync(item => item.ProjectId.HasValue, cancellationToken)
                ? [new("test plans linked to projects")]
                : [];
}
