using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectsProfileTransferStore(DatabaseTransferOperationRunner operations) {
    public async Task<ProjectsProfileTransferCounts> CountAsync(DatabaseTransferProfileSession session, CancellationToken cancellationToken = default) {
        await using var context = await CreateAsync(session, cancellationToken);
        return new(
            await context.Set<Project>().CountAsync(cancellationToken),
            await context.Set<ProjectPhase>().CountAsync(cancellationToken),
            await context.Set<ProjectOptionSelection>().CountAsync(cancellationToken),
            await context.Set<ProjectHierarchyLink>().CountAsync(cancellationToken),
            await context.Set<ProjectRetirementRecord>().CountAsync(cancellationToken),
            await context.Set<ProjectCreationReservationRecord>().CountAsync(cancellationToken));
    }

    public async Task<ProjectsProfileTransferData> LoadAsync(DatabaseTransferProfileSession session, CancellationToken cancellationToken = default) {
        await using var context = await CreateAsync(session, cancellationToken);
        return new(
            await context.Set<Project>().AsNoTracking().Select(row => new ProjectTransferProject {
                LifetimeId = row.LifetimeId, LegacyAgentAccessBindingEligible = row.LegacyAgentAccessBindingEligible, Id = row.Id, Name = row.Name, Slug = row.Slug, Description = row.Description, Objective = row.Objective, Status = row.Status, CurrentPhase = row.CurrentPhase, TargetDateUtc = row.TargetDateUtc, CreatedAtUtc = row.CreatedAtUtc, UpdatedAtUtc = row.UpdatedAtUtc
            }).ToArrayAsync(cancellationToken),
            await context.Set<ProjectPhase>().AsNoTracking().Select(row => new ProjectTransferPhase {
                Id = row.Id, ProjectId = row.ProjectId, Name = row.Name, Goal = row.Goal, Status = row.Status, OrderIndex = row.OrderIndex, StartDateUtc = row.StartDateUtc, EndDateUtc = row.EndDateUtc
            }).ToArrayAsync(cancellationToken),
            await context.Set<ProjectOptionSelection>().AsNoTracking().Select(row => new ProjectTransferOption {
                Id = row.Id, ProjectId = row.ProjectId, Category = row.Category, OptionName = row.OptionName, Notes = row.Notes
            }).ToArrayAsync(cancellationToken),
            await context.Set<ProjectHierarchyLink>().AsNoTracking().Select(row => new ProjectTransferHierarchy {
                Id = row.Id, ParentProjectId = row.ParentProjectId, ChildProjectId = row.ChildProjectId, CreatedAtUtc = row.CreatedAtUtc
            }).ToArrayAsync(cancellationToken),
            await context.Set<ProjectRetirementRecord>().AsNoTracking().Select(row => new ProjectTransferRetirement {
                LifetimeId = row.LifetimeId, ProjectId = row.ProjectId, RetiredAtUtc = row.RetiredAtUtc, ImportedHistory = row.ImportedHistory
            }).ToArrayAsync(cancellationToken),
            await context.Set<ProjectCreationReservationRecord>().AsNoTracking().Select(row => new ProjectTransferReservation {
                Id = row.Id, DatabaseProfileId = row.DatabaseProfileId, ProjectId = row.ProjectId, LifetimeId = row.LifetimeId, RequesterId = row.RequesterId, ParentProjectId = row.ParentProjectId, ParentLifetimeId = row.ParentLifetimeId, State = row.State, CreatedAtUtc = row.CreatedAtUtc, ConsumedAtUtc = row.ConsumedAtUtc, CancelledAtUtc = row.CancelledAtUtc, ImportedHistory = row.ImportedHistory
            }).ToArrayAsync(cancellationToken));
    }

    public async Task ClearAsync(DatabaseTransferProfileSession session, CancellationToken cancellationToken = default) {
        RequireMutation(session);
        await using var context = await CreateAsync(session, cancellationToken);
        if (await context.Set<ProjectRetirementRecord>().AnyAsync(cancellationToken) ||
            await context.Set<ProjectCreationReservationRecord>().AnyAsync(cancellationToken)) {
            throw new InvalidOperationException("Project transfer cannot replace retained project lifetime evidence.");
        }
        context.RemoveRange(await context.Set<ProjectHierarchyLink>().ToArrayAsync(cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
        context.RemoveRange(await context.Set<ProjectOptionSelection>().ToArrayAsync(cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
        context.RemoveRange(await context.Set<ProjectPhase>().ToArrayAsync(cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
        context.RemoveRange(await context.Set<Project>().ToArrayAsync(cancellationToken));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAsync(DatabaseTransferProfileSession session, ProjectsProfileTransferData data, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(data);
        RequireMutation(session);
        if (data.Retirements.Any(row => row.ImportedHistory is null) || data.CreationReservations.Any(row => row.ImportedHistory is null)) {
            throw new InvalidDataException("Imported lifetime evidence requires an explicit history disposition.");
        }
        await using var context = await CreateAsync(session, cancellationToken);
        context.AddRange(data.Projects.Select(row => new Project {
            Id = row.Id, Name = row.Name, Slug = row.Slug, Description = row.Description, Objective = row.Objective, Status = row.Status, CurrentPhase = row.CurrentPhase, TargetDateUtc = row.TargetDateUtc, CreatedAtUtc = row.CreatedAtUtc, UpdatedAtUtc = row.UpdatedAtUtc
        }));
        await context.SaveChangesAsync(cancellationToken);
        context.AddRange(data.Phases.Select(row => new ProjectPhase {
            Id = row.Id, ProjectId = row.ProjectId, Name = row.Name, Goal = row.Goal, Status = row.Status, OrderIndex = row.OrderIndex, StartDateUtc = row.StartDateUtc, EndDateUtc = row.EndDateUtc
        }));
        await context.SaveChangesAsync(cancellationToken);
        context.AddRange(data.Options.Select(row => new ProjectOptionSelection {
            Id = row.Id, ProjectId = row.ProjectId, Category = row.Category, OptionName = row.OptionName, Notes = row.Notes
        }));
        await context.SaveChangesAsync(cancellationToken);
        context.AddRange(data.HierarchyLinks.Select(row => new ProjectHierarchyLink {
            Id = row.Id, ParentProjectId = row.ParentProjectId, ChildProjectId = row.ChildProjectId, CreatedAtUtc = row.CreatedAtUtc
        }));
        await context.SaveChangesAsync(cancellationToken);
        context.AddRange(data.Retirements.Select(row => new ProjectRetirementRecord {
            LifetimeId = row.LifetimeId, ProjectId = row.ProjectId, RetiredAtUtc = row.RetiredAtUtc, ImportedHistory = row.ImportedHistory
        }));
        await context.SaveChangesAsync(cancellationToken);
        context.AddRange(data.CreationReservations.Select(row => new ProjectCreationReservationRecord {
            Id = row.Id, DatabaseProfileId = row.DatabaseProfileId, ProjectId = row.ProjectId, LifetimeId = row.LifetimeId, RequesterId = row.RequesterId, ParentProjectId = row.ParentProjectId, ParentLifetimeId = row.ParentLifetimeId, State = row.State, CreatedAtUtc = row.CreatedAtUtc, ConsumedAtUtc = row.ConsumedAtUtc, CancelledAtUtc = row.CancelledAtUtc, ImportedHistory = row.ImportedHistory
        }));
        await context.SaveChangesAsync(cancellationToken);
    }

    private Task<ProjectsDbContext> CreateAsync(DatabaseTransferProfileSession session, CancellationToken cancellationToken)
        => operations.CreateOwnerAsync<ProjectsDbContext>(session, static options => new ProjectsDbContext(options), cancellationToken);

    private static void RequireMutation(DatabaseTransferProfileSession session) {
        if (session.Mode != DatabaseTransferProfileMode.Serializable) {
            throw new InvalidOperationException("Projects import requires the exact locked Serializable target session.");
        }
    }
}
