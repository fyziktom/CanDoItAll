using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public sealed record ProjectLifetimeObservation(ProjectWriteAdmission Admission, bool HasRetiredLifetime);

public sealed partial class ProjectWriteAdmissionService {
    public async Task<ProjectLifetimeObservation?> CaptureObservationAsync(Guid projectId,
        CancellationToken cancellationToken = default) {
        if (projectId == Guid.Empty) {
            throw new ArgumentException("A project is required.", nameof(projectId));
        }
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<Project>().AsNoTracking().Where(project => project.Id == projectId)
            .Select(project => new ProjectLifetimeObservation(new(DatabaseProfileId, project.Id, project.LifetimeId),
                context.Set<ProjectRetirementRecord>().Any(retirement => retirement.ProjectId == project.Id)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task RequireManyUnderMutationGatesAsync(IReadOnlyCollection<ProjectWriteAdmission> admissions,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(admissions);
        if (admissions.Count == 0) {
            return;
        }
        var expected = admissions.ToDictionary(project => project.ProjectId);
        if (expected.Count > ProjectRecordQueryLimits.MaximumReferenceCount) {
            throw new ArgumentException("The project admission set exceeds the supported bound.", nameof(admissions));
        }
        if (expected.Values.FirstOrDefault(project => project.DatabaseProfileId != DatabaseProfileId) is { } foreign) {
            throw new ProjectWriteAdmissionRejectedException(foreign);
        }
        await using var context = await coordinatedTransaction.CreateEnlistedAsync(
            contextOptions, static options => new ProjectsDbContext(options), cancellationToken);
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(context,
            expected.Keys.Order().Select(ProjectMutationScopeKeys.ForProject).ToArray(), cancellationToken);
        var active = await ReadActiveLifetimesAsync(context, expected.Keys.ToArray(), cancellationToken);
        foreach (var project in expected.Values) {
            if (!active.TryGetValue(project.ProjectId, out var lifetime) || lifetime != project.LifetimeId) {
                throw new ProjectWriteAdmissionRejectedException(project);
            }
        }
    }

    public async Task<IReadOnlyList<ProjectWriteAdmission>> CaptureManyAsync(IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(projectIds);
        if (projectIds.Count > ProjectRecordQueryLimits.MaximumReferenceCount || projectIds.Contains(Guid.Empty)) {
            throw new ArgumentException("The requested project admission set has invalid identifiers or exceeds the supported bound.", nameof(projectIds));
        }
        if (projectIds.Count == 0) {
            return [];
        }
        var ids = projectIds.Distinct().ToArray();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<Project>().AsNoTracking().Where(project => ids.Contains(project.Id) &&
                !context.Set<ProjectRetirementRecord>().Any(retired => retired.LifetimeId == project.LifetimeId))
            .OrderBy(project => project.Id)
            .Select(project => new ProjectWriteAdmission(DatabaseProfileId, project.Id, project.LifetimeId))
            .ToListAsync(cancellationToken);
    }
}
