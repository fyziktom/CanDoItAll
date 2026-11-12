using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectIdentityQueryService(
    IDbContextFactory<ProjectsDbContext> factory,
    DbContextOptions<ProjectsDbContext> options,
    CoordinatedDatabaseTransaction transactions) {
    public async Task<Result<ProjectExternalIdentityResolution>> ResolveExternalIdentityAsync(string externalNamespace,
        string externalKey, Guid? expectedLifetimeId = null, CancellationToken cancellationToken = default) {
        var normalized = ProjectExternalIdentity.Normalize(externalNamespace, externalKey);
        if (normalized.IsFailure) {
            return Result<ProjectExternalIdentityResolution>.Failure(normalized.Errors);
        }
        if (expectedLifetimeId == Guid.Empty) {
            return Result<ProjectExternalIdentityResolution>.Failure(Error.Validation(
                "Expected lifetime must be a nonempty identifier.", ProjectErrorCodes.ExternalIdentityInvalid));
        }
        var identity = normalized.Value!;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var matches = await db.Set<Project>().AsNoTracking()
            .Where(project => project.ExternalNamespace == identity.Namespace && project.ExternalKey == identity.Key)
            .Select(project => new ProjectExternalIdentityResolution(project.Id, project.LifetimeId,
                project.ExternalNamespace!, project.ExternalKey!))
            .Take(2).ToArrayAsync(cancellationToken);
        if (matches.Length == 0) {
            return Result<ProjectExternalIdentityResolution>.Failure(Error.Failure(
                "No project has this external identity.", ProjectErrorCodes.NotFound));
        }
        if (matches.Length != 1) {
            return Result<ProjectExternalIdentityResolution>.Failure(Error.Failure(
                "Multiple projects have this external identity. Repair the conflicting bindings before continuing.",
                ProjectErrorCodes.ExternalIdentityConflict));
        }
        var match = matches[0];
        if (expectedLifetimeId.HasValue && expectedLifetimeId.Value != match.LifetimeId) {
            return Result<ProjectExternalIdentityResolution>.Failure(Error.Failure(
                "The external identity now belongs to a different project lifetime. Review the current binding before continuing.",
                ProjectErrorCodes.LifetimeChanged));
        }
        return Result<ProjectExternalIdentityResolution>.Success(match);
    }

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
