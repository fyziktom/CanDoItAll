using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public sealed record ProjectDeletionCommitTarget {
    internal ProjectDeletionCommitTarget(ProjectWriteAdmission project, ProjectCreationReservation? cancelledCreation) {
        ArgumentNullException.ThrowIfNull(project);
        if (cancelledCreation is not null && (cancelledCreation.DatabaseProfileId != project.DatabaseProfileId ||
                cancelledCreation.ProjectId != project.ProjectId || cancelledCreation.LifetimeId != project.LifetimeId)) {
            throw new ArgumentException("The cancelled creation must identify the exact retired project lifetime.", nameof(cancelledCreation));
        }
        Project = project;
        CancelledCreation = cancelledCreation;
    }

    public ProjectWriteAdmission Project { get; }
    public ProjectCreationReservation? CancelledCreation { get; }

    public void RequireOriginalTarget(ProjectMutationTarget target) {
        ArgumentNullException.ThrowIfNull(target);
        if (target.ExistingProjects.IsDefault) {
            throw new InvalidOperationException("Final deletion authority requires the original project admissions.");
        }
        var matches = target.Purpose switch {
            ProjectMutationPurpose.ExistingWrite => CancelledCreation is null && target.Reservation is null &&
                target.ExistingProjects.Contains(Project),
            ProjectMutationPurpose.CreateRoot when target.Reservation is null => CancelledCreation is null,
            ProjectMutationPurpose.CreateRoot or ProjectMutationPurpose.CreateChild => CancelledCreation is not null &&
                target.Reservation == CancelledCreation &&
                (target.Purpose == ProjectMutationPurpose.CreateChild) == CancelledCreation.ParentProjectId.HasValue,
            _ => false
        };
        if (target.ProjectId != Project.ProjectId || !matches ||
                target.ExistingProjects.Any(item => item.DatabaseProfileId != Project.DatabaseProfileId ||
                    item.ProjectId == Project.ProjectId && item != Project) ||
                CancelledCreation?.ParentProjectId is { } parent && !target.ExistingProjects.Contains(
                    new(Project.DatabaseProfileId, parent, CancelledCreation.ParentLifetimeId!.Value))) {
            throw new InvalidOperationException("Final deletion authority must retain its exact originally admitted target.");
        }
    }
}

public sealed partial class ProjectWriteAdmissionService {
    internal async Task RequireRetiredDeletionAsync(ProjectsDbContext context, ProjectDeletionCommitTarget target,
        CancellationToken cancellationToken) {
        var project = target.Project;
        if (project.DatabaseProfileId != DatabaseProfileId || !context.Database.IsNpgsql() ||
                context.Database.CurrentTransaction is null || context.ChangeTracker.HasChanges()) {
            throw new InvalidOperationException("Final deletion admission requires the saved original PostgreSQL owner transaction.");
        }
        if (await context.Set<Project>().AsNoTracking().AnyAsync(item => item.Id == project.ProjectId, cancellationToken) ||
                !await context.Set<ProjectRetirementRecord>().AsNoTracking().AnyAsync(item =>
                    item.ProjectId == project.ProjectId && item.LifetimeId == project.LifetimeId && item.ImportedHistory == null,
                    cancellationToken)) {
            throw new ProjectWriteAdmissionRejectedException(project);
        }
        if (target.CancelledCreation is not { } reservation) {
            return;
        }
        ValidateReservation(reservation);
        var record = await context.Set<ProjectCreationReservationRecord>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == reservation.Id, cancellationToken);
        if (record is null || !Matches(record, reservation) || record.State != ProjectCreationReservationState.Cancelled ||
                record.ConsumedAtUtc is null || record.CancelledAtUtc is null) {
            throw new InvalidOperationException("The original consumed creation must be cancelled by this exact compensating deletion.");
        }
    }
}
