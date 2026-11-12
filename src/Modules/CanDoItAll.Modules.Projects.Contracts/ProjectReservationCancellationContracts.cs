namespace CanDoItAll.Modules.Projects;

public sealed record ProjectReservationCancellationTarget {
    public ProjectReservationCancellationTarget(ProjectWriteAdmission project, Guid reservationId, Guid requesterId,
        ProjectWriteAdmission? parent = null) {
        ArgumentNullException.ThrowIfNull(project);
        if (reservationId == Guid.Empty || requesterId == Guid.Empty || parent is not null &&
                (parent.DatabaseProfileId != project.DatabaseProfileId || parent.ProjectId == project.ProjectId)) {
            throw new ArgumentException("Reservation cancellation requires the exact original reservation and distinct parent lifetime.");
        }
        Project = project;
        ReservationId = reservationId;
        RequesterId = requesterId;
        Parent = parent;
    }

    public ProjectWriteAdmission Project { get; }
    public Guid ReservationId { get; }
    public Guid RequesterId { get; }
    public ProjectWriteAdmission? Parent { get; }
}

public sealed record ProjectReservationCancellationPreparationContext {
    public ProjectReservationCancellationPreparationContext(ProjectDeletionPreparationContext completion,
        ProjectReservationCancellationTarget target) {
        ArgumentNullException.ThrowIfNull(completion);
        ArgumentNullException.ThrowIfNull(target);
        if (completion.Project != target.Project) {
            throw new ArgumentException("Reservation cancellation must retain the same original completion and reserved lifetime.");
        }
        Completion = completion;
        Target = target;
    }

    public ProjectDeletionPreparationContext Completion { get; }
    public ProjectReservationCancellationTarget Target { get; }
}
