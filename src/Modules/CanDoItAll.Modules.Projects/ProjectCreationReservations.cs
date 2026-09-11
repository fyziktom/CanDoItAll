using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Projects;

public sealed record ProjectCreationReservation(Guid Id, Guid DatabaseProfileId, Guid ProjectId, Guid LifetimeId,
    Guid RequesterId, Guid? ParentProjectId, Guid? ParentLifetimeId);

public enum ProjectCreationReservationState {
    Reserved = 1,
    Consumed = 2,
    Cancelled = 3
}

public sealed class ProjectCreationReservationRecord {
    public Guid Id { get; set; }
    public Guid DatabaseProfileId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid LifetimeId { get; set; }
    public Guid RequesterId { get; set; }
    public Guid? ParentProjectId { get; set; }
    public Guid? ParentLifetimeId { get; set; }
    public ProjectCreationReservationState State { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
}

internal sealed class ProjectCreationReservationRecordConfiguration : IEntityTypeConfiguration<ProjectCreationReservationRecord> {
    public void Configure(EntityTypeBuilder<ProjectCreationReservationRecord> builder) {
        builder.ToTable("Projects_ProjectCreationReservations", table => table.HasCheckConstraint(
            "CK_Projects_CreationReservation_Parent",
            "(\"ParentProjectId\" IS NULL AND \"ParentLifetimeId\" IS NULL) OR (\"ParentProjectId\" IS NOT NULL AND \"ParentLifetimeId\" IS NOT NULL)"));
        builder.HasKey(record => record.Id);
        builder.Property(record => record.State).HasConversion<int>();
        builder.HasIndex(record => record.LifetimeId).IsUnique();
        builder.HasIndex(record => record.ProjectId).IsUnique()
            .HasFilter($"\"State\" = {(int)ProjectCreationReservationState.Reserved}");
        builder.HasIndex(record => new { record.ParentProjectId, record.State });
    }
}

public sealed partial class ProjectWriteAdmissionService {
    public async Task<ProjectCreationReservation> ReserveCreationAsync(Guid projectId, Guid requesterId, Guid operationId,
        Guid? parentProjectId = null, CancellationToken cancellationToken = default) {
        if (projectId == Guid.Empty || requesterId == Guid.Empty || operationId == Guid.Empty || parentProjectId == Guid.Empty || parentProjectId == projectId) {
            throw new ArgumentException("A reservation requires nonempty project, requester and operation identifiers and a different parent project.");
        }
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutation = await SerializableMutationScope.BeginAsync(context,
            ReservationScopeKeys(projectId, parentProjectId), cancellationToken);
        var existing = await context.Set<ProjectCreationReservationRecord>().SingleOrDefaultAsync(record => record.Id == operationId, cancellationToken);
        if (existing is not null) {
            if (existing.DatabaseProfileId != DatabaseProfileId || existing.ProjectId != projectId || existing.RequesterId != requesterId ||
                existing.ParentProjectId != parentProjectId || existing.State is not (ProjectCreationReservationState.Reserved or ProjectCreationReservationState.Consumed)) {
                throw new InvalidOperationException("The creation operation is already bound to a different or cancelled project reservation.");
            }
            return ToReservation(existing);
        }
        if (await context.Set<Project>().AnyAsync(project => project.Id == projectId, cancellationToken) ||
            await context.Set<ProjectCreationReservationRecord>().AnyAsync(record => record.ProjectId == projectId &&
                record.State == ProjectCreationReservationState.Reserved, cancellationToken)) {
            throw new InvalidOperationException("The project id is already live or reserved by another creation operation.");
        }
        Guid? parentLifetimeId = null;
        if (parentProjectId.HasValue) {
            parentLifetimeId = await context.Set<Project>().Where(project => project.Id == parentProjectId.Value)
                .Select(project => (Guid?)project.LifetimeId).SingleOrDefaultAsync(cancellationToken);
            if (parentLifetimeId is null) {
                throw new InvalidOperationException("The parent project no longer exists.");
            }
        }
        var record = new ProjectCreationReservationRecord {
            Id = operationId, DatabaseProfileId = DatabaseProfileId, ProjectId = projectId, LifetimeId = Guid.NewGuid(),
            RequesterId = requesterId, ParentProjectId = parentProjectId, ParentLifetimeId = parentLifetimeId,
            State = ProjectCreationReservationState.Reserved, CreatedAtUtc = DateTimeOffset.UtcNow
        };
        context.Add(record);
        await context.SaveChangesAsync(cancellationToken);
        await mutation.CommitAsync(cancellationToken);
        return ToReservation(record);
    }

    public async Task RequireCreationGrantAsync(ProjectCreationReservation reservation, CancellationToken cancellationToken = default) {
        ValidateReservation(reservation);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await context.Set<ProjectCreationReservationRecord>().AsNoTracking()
            .SingleOrDefaultAsync(record => record.Id == reservation.Id, cancellationToken);
        if (record is null || !Matches(record, reservation) || record.State is not (ProjectCreationReservationState.Reserved or ProjectCreationReservationState.Consumed) ||
            record.State == ProjectCreationReservationState.Consumed && !await context.Set<Project>().AnyAsync(
                project => project.Id == reservation.ProjectId && project.LifetimeId == reservation.LifetimeId, cancellationToken)) {
            throw new InvalidOperationException("The exact project creation reservation no longer admits an access grant.");
        }
        if (record.State == ProjectCreationReservationState.Reserved && record.ParentProjectId is { } parentId &&
            !await context.Set<Project>().AnyAsync(project => project.Id == parentId && project.LifetimeId == record.ParentLifetimeId, cancellationToken)) {
            throw new InvalidOperationException("The parent lifetime no longer admits the reserved child project.");
        }
    }

    public async Task CancelCreationAsync(ProjectCreationReservation reservation, CancellationToken cancellationToken = default) {
        ValidateReservation(reservation);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutation = await SerializableMutationScope.BeginAsync(context,
            ReservationScopeKeys(reservation.ProjectId, reservation.ParentProjectId), cancellationToken);
        var record = await context.Set<ProjectCreationReservationRecord>().SingleOrDefaultAsync(record => record.Id == reservation.Id, cancellationToken);
        if (record is null || !Matches(record, reservation)) {
            throw new InvalidOperationException("The exact project creation reservation was not found.");
        }
        if (record.State == ProjectCreationReservationState.Cancelled) {
            return;
        }
        if (record.State is not (ProjectCreationReservationState.Reserved or ProjectCreationReservationState.Consumed)) {
            throw new InvalidOperationException("The creation reservation has an unknown state.");
        }
        if (await context.Set<Project>().AnyAsync(project => project.Id == reservation.ProjectId && project.LifetimeId == reservation.LifetimeId, cancellationToken)) {
            throw new InvalidOperationException("A live project lifetime cannot have its creation grant cancelled as an uncommitted operation.");
        }
        record.State = ProjectCreationReservationState.Cancelled;
        record.CancelledAtUtc = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        await mutation.CommitAsync(cancellationToken);
    }

    public async Task<ProjectCreationReservation?> CaptureReservedCreationForMutationAsync(Guid projectId, CancellationToken cancellationToken = default) {
        await using var context = await coordinatedTransaction.CreateEnlistedAsync(contextOptions,
            static options => new ProjectsDbContext(options), cancellationToken);
        var record = await context.Set<ProjectCreationReservationRecord>().AsNoTracking().SingleOrDefaultAsync(record =>
            record.ProjectId == projectId && record.State == ProjectCreationReservationState.Reserved, cancellationToken);
        if (record is not null && record.DatabaseProfileId != DatabaseProfileId) {
            throw new InvalidOperationException("The stored creation reservation belongs to another database profile.");
        }
        return record is null ? null : ToReservation(record);
    }

    internal async Task<bool> TryConsumeCreationAsync(ProjectsDbContext context, ProjectCreationReservation reservation,
        Guid? parentProjectId, CancellationToken cancellationToken) {
        ValidateReservation(reservation);
        var record = await context.Set<ProjectCreationReservationRecord>().SingleOrDefaultAsync(record => record.Id == reservation.Id, cancellationToken);
        if (record is null || !Matches(record, reservation) || record.State != ProjectCreationReservationState.Reserved ||
            record.ParentProjectId != parentProjectId) {
            return false;
        }
        if (record.ParentProjectId is { } parentId && !await context.Set<Project>().AnyAsync(project =>
            project.Id == parentId && project.LifetimeId == record.ParentLifetimeId, cancellationToken)) {
            return false;
        }
        record.State = ProjectCreationReservationState.Consumed;
        record.ConsumedAtUtc = DateTimeOffset.UtcNow;
        return true;
    }

    internal static async Task CancelCreationForDeletionAsync(ProjectsDbContext context, Guid projectId, CancellationToken cancellationToken) {
        var reservations = await context.Set<ProjectCreationReservationRecord>().Where(record =>
            (record.ProjectId == projectId || record.ParentProjectId == projectId) &&
            record.State == ProjectCreationReservationState.Reserved).ToArrayAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var reservation in reservations) {
            reservation.State = ProjectCreationReservationState.Cancelled;
            reservation.CancelledAtUtc = now;
        }
    }

    private void ValidateReservation(ProjectCreationReservation reservation) {
        ArgumentNullException.ThrowIfNull(reservation);
        if (reservation.DatabaseProfileId != DatabaseProfileId || reservation.Id == Guid.Empty || reservation.ProjectId == Guid.Empty ||
            reservation.LifetimeId == Guid.Empty || reservation.RequesterId == Guid.Empty ||
            reservation.ParentProjectId.HasValue != reservation.ParentLifetimeId.HasValue ||
            reservation.ParentProjectId == Guid.Empty || reservation.ParentLifetimeId == Guid.Empty) {
            throw new InvalidOperationException("The creation reservation does not identify an exact lifetime in this database profile.");
        }
    }

    private static bool Matches(ProjectCreationReservationRecord record, ProjectCreationReservation reservation) =>
        record.DatabaseProfileId == reservation.DatabaseProfileId && record.ProjectId == reservation.ProjectId &&
        record.LifetimeId == reservation.LifetimeId && record.RequesterId == reservation.RequesterId &&
        record.ParentProjectId == reservation.ParentProjectId && record.ParentLifetimeId == reservation.ParentLifetimeId;

    private static ProjectCreationReservation ToReservation(ProjectCreationReservationRecord record) => new(record.Id, record.DatabaseProfileId,
        record.ProjectId, record.LifetimeId, record.RequesterId, record.ParentProjectId, record.ParentLifetimeId);

    private static string[] ReservationScopeKeys(Guid projectId, Guid? parentProjectId) =>
        parentProjectId.HasValue
            ? new[] { ProjectMutationScopeKeys.ForProject(projectId), ProjectMutationScopeKeys.ForProject(parentProjectId.Value), ProjectMutationScopeKeys.Hierarchy }
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            : [ProjectMutationScopeKeys.ForProject(projectId)];
}
