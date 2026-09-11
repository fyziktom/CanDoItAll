using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public sealed record ProjectAccessLifetimeFact(Guid ProjectId, Guid LifetimeId, bool LegacyAgentAccessBindingEligible);
public sealed record ProjectCreationGrantFact(Guid ProjectId, Guid LifetimeId, Guid RequesterId);
public sealed record ProjectAccessBindingFacts(Guid DatabaseProfileId, IReadOnlyList<ProjectAccessLifetimeFact> Projects,
    IReadOnlyList<ProjectCreationGrantFact> Reservations);

public sealed partial class ProjectWriteAdmissionService {
    public async Task<ProjectAccessBindingFacts> ListAccessBindingFactsAsync(IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(projectIds);
        if (projectIds.Any(id => id == Guid.Empty)) {
            throw new ArgumentException("Project binding facts require nonempty project identifiers.", nameof(projectIds));
        }
        var ids = projectIds.Distinct().ToArray();
        if (ids.Length == 0) {
            return new(DatabaseProfileId, [], []);
        }
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var projects = await context.Set<Project>().AsNoTracking().Where(project => ids.Contains(project.Id) &&
            !context.Set<ProjectRetirementRecord>().Any(retirement => retirement.LifetimeId == project.LifetimeId))
            .Select(project => new ProjectAccessLifetimeFact(project.Id, project.LifetimeId, project.LegacyAgentAccessBindingEligible))
            .ToArrayAsync(cancellationToken);
        var reservations = await context.Set<ProjectCreationReservationRecord>().AsNoTracking().Where(record =>
            ids.Contains(record.ProjectId) && record.State == ProjectCreationReservationState.Reserved && record.ImportedHistory == null &&
            (record.ParentProjectId == null || context.Set<Project>().Any(parent => parent.Id == record.ParentProjectId &&
                parent.LifetimeId == record.ParentLifetimeId)))
            .Select(record => new { record.DatabaseProfileId, record.ProjectId, record.LifetimeId, record.RequesterId })
            .ToArrayAsync(cancellationToken);
        if (reservations.Any(record => record.DatabaseProfileId != DatabaseProfileId)) {
            throw new InvalidOperationException("A project reservation belongs to another database profile.");
        }
        return new(DatabaseProfileId, projects, reservations.Select(record =>
            new ProjectCreationGrantFact(record.ProjectId, record.LifetimeId, record.RequesterId)).ToArray());
    }
}
