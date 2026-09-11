using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectIdentityQueryService(
    IDbContextFactory<ProjectsDbContext> factory,
    DbContextOptions<ProjectsDbContext> options,
    CoordinatedDatabaseTransaction transactions) {
    public async Task<bool> ExistsAsync(Guid projectId, CancellationToken cancellationToken = default) {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await ExistsAsync(db, projectId, cancellationToken);
    }

    public async Task<bool> ExistsForMutationAsync(Guid projectId, CancellationToken cancellationToken = default) {
        await using var db = await transactions.CreateEnlistedAsync(options, static value => new ProjectsDbContext(value), cancellationToken);
        return await ExistsAsync(db, projectId, cancellationToken);
    }

    public async Task<Guid?> FindNextIdAfterAsync(Guid afterProjectId, CancellationToken cancellationToken = default) {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Set<Project>().AsNoTracking()
            .Where(project => project.Id.CompareTo(afterProjectId) > 0)
            .OrderBy(project => project.Id)
            .Select(project => (Guid?)project.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static Task<bool> ExistsAsync(ProjectsDbContext db, Guid projectId, CancellationToken cancellationToken)
        => db.Set<Project>().AsNoTracking().AnyAsync(project => project.Id == projectId, cancellationToken);
}
