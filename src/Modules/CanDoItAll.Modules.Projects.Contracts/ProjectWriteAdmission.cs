namespace CanDoItAll.Modules.Projects;

public sealed record ProjectWriteAdmission {
    public ProjectWriteAdmission(Guid databaseProfileId, Guid projectId, Guid lifetimeId) {
        if (databaseProfileId == Guid.Empty || projectId == Guid.Empty || lifetimeId == Guid.Empty) {
            throw new ArgumentException("A project admission requires nonempty database profile, project and lifetime identifiers.");
        }
        DatabaseProfileId = databaseProfileId;
        ProjectId = projectId;
        LifetimeId = lifetimeId;
    }

    public Guid DatabaseProfileId { get; }
    public Guid ProjectId { get; }
    public Guid LifetimeId { get; }
}

public sealed class ProjectWriteAdmissionRejectedException(ProjectWriteAdmission admission)
    : InvalidOperationException($"Project '{admission.ProjectId:D}' in database profile '{admission.DatabaseProfileId:D}' no longer admits writes for lifetime '{admission.LifetimeId:D}'.") {
    public ProjectWriteAdmission Admission { get; } = admission;
}
