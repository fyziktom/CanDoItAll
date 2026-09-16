using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public sealed record ProjectWriteSelection(Guid Id, string Name, ProjectWriteAdmission Admission);

public sealed class ProjectWriteSelectionQuery(IDbContextFactory<ProjectsDbContext> factory, ICanonicalRuntimeDatabase canonical) {
    public async Task<IReadOnlyList<ProjectWriteSelection>> ListAsync(int? maximumItems = null, CancellationToken cancellationToken = default) {
        if (maximumItems is < 1 or > ProjectRecordQueryLimits.MaximumReferenceCount) {
            throw new ArgumentOutOfRangeException(nameof(maximumItems));
        }
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var query = context.Set<Project>().AsNoTracking().OrderBy(project => project.Name).ThenBy(project => project.Id)
            .Select(project => new { project.Id, project.Name, project.LifetimeId });
        var items = maximumItems.HasValue
            ? await query.Take(maximumItems.Value).ToArrayAsync(cancellationToken)
            : await query.ToArrayAsync(cancellationToken);
        return items.Select(project => new ProjectWriteSelection(project.Id, project.Name,
            new ProjectWriteAdmission(canonical.Profile.Profile.Id, project.Id, project.LifetimeId))).ToArray();
    }
}
