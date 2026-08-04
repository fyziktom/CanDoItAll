using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

internal sealed class ProjectsProjectTransferTargetStateParticipant
    : IProjectTransferTargetStateParticipant
{
    public ProjectTransferTargetStateArea Area =>
        ProjectTransferTargetStateArea.Projects;

    public IReadOnlyCollection<Type> EntityTypesToLock { get; } =
    [
        typeof(Project),
        typeof(ProjectHierarchyLink),
        typeof(ProjectOptionSelection),
        typeof(ProjectPhase)
    ];

    public async Task<IReadOnlyList<ProjectTransferTargetStateResidue>>
        FindResiduesAsync(
            AppDbContext dbContext,
            CancellationToken cancellationToken)
    {
        var hasResidue =
            await dbContext.Set<Project>().AsNoTracking().AnyAsync(cancellationToken) ||
            await dbContext.Set<ProjectHierarchyLink>().AsNoTracking().AnyAsync(cancellationToken) ||
            await dbContext.Set<ProjectOptionSelection>().AsNoTracking().AnyAsync(cancellationToken) ||
            await dbContext.Set<ProjectPhase>().AsNoTracking().AnyAsync(cancellationToken);
        return hasResidue
            ? [new("project core records")]
            : [];
    }
}
