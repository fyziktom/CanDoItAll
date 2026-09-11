using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectWorkAssignmentQueryService(IDbContextFactory<WorkbenchDbContext> factory) : IProjectWorkAssignmentQueries {
    public async Task<IReadOnlyList<ProjectWorkAssignmentFact>> ListForProjectsAsync(
        IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(projectIds);
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return (await context.Set<ProjectWorkAssignmentRecord>().AsNoTracking()
            .Where(item => projectIds.Contains(item.ProjectId)).ToListAsync(cancellationToken))
            .Select(item => item.ToFact()).ToArray();
    }

    public async Task<IReadOnlyList<ProjectWorkAssignmentFact>> ListForPartiesAsync(
        IReadOnlyCollection<Guid> partyIds, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(partyIds);
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return (await context.Set<ProjectWorkAssignmentRecord>().AsNoTracking()
            .Where(item => partyIds.Contains(item.PartyId)).ToListAsync(cancellationToken))
            .Select(item => item.ToFact()).ToArray();
    }

    public async Task<ProjectWorkAssignmentFact?> GetAsync(Guid assignmentId, CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return (await context.Set<ProjectWorkAssignmentRecord>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == assignmentId, cancellationToken))?.ToFact();
    }
}
