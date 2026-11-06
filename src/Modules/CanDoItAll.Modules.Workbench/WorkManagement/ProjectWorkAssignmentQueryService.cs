using CanDoItAll.Modules.Projects;
using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectWorkAssignmentQueryService(IDbContextFactory<WorkbenchDbContext> factory) : IProjectWorkAssignmentQueries {
    public async Task<ProjectAssignmentPrimaryObservation> ReadPrimaryObservationAsync(ProjectWriteAdmission project, string nodeKey,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(project);
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var ids = await context.Set<ProjectWorkAssignmentRecord>().AsNoTracking().Where(item =>
            item.ProjectId == project.ProjectId && item.ProjectLifetimeId == project.LifetimeId &&
            item.NodeKey == nodeKey && item.IsPrimary).Select(item => item.Id).ToArrayAsync(cancellationToken);
        return new(project.ProjectId, project.LifetimeId, ProjectPartyAssignmentRole.WorkItemAssignee, nodeKey, ids.Order().ToImmutableArray());
    }

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
