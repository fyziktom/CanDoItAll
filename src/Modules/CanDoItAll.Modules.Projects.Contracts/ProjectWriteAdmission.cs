namespace CanDoItAll.Modules.Projects;

/// <summary>
/// Owner-issued evidence that binds a later project write to the project lifetime the caller read. Read it from the
/// owner (for example <c>expectedProjectAdmission</c> in a Project Structure read) and send it back unchanged; never
/// construct or edit it. It is not a bearer token, lease or version number: a write whose admission names another
/// project, or a lifetime that is no longer the project's current one (for example after the project was retired or
/// deleted), is rejected so that a stale page cannot write into a different project incarnation.
/// </summary>
public sealed record ProjectWriteAdmission {
    /// <summary>Creates an admission; all three identifiers must be nonempty.</summary>
    /// <param name="databaseProfileId">Identifier of the database profile that holds the project.</param>
    /// <param name="projectId">Identifier of the admitted project.</param>
    /// <param name="lifetimeId">Identifier of the admitted project lifetime.</param>
    public ProjectWriteAdmission(Guid databaseProfileId, Guid projectId, Guid lifetimeId) {
        if (databaseProfileId == Guid.Empty || projectId == Guid.Empty || lifetimeId == Guid.Empty) {
            throw new ArgumentException("A project admission requires nonempty database profile, project and lifetime identifiers.");
        }
        DatabaseProfileId = databaseProfileId;
        ProjectId = projectId;
        LifetimeId = lifetimeId;
    }

    /// <summary>Identifier of the database profile (workspace data store) that holds the admitted project.</summary>
    public Guid DatabaseProfileId { get; }

    /// <summary>Identifier of the admitted project; it must equal the project addressed by the write.</summary>
    public Guid ProjectId { get; }

    /// <summary>
    /// Identifier of the project lifetime (incarnation) the caller read. The owner assigns a new lifetime when a project
    /// is created and records its retirement, so this value fences writes prepared against another incarnation.
    /// </summary>
    public Guid LifetimeId { get; }
}

public sealed class ProjectWriteAdmissionRejectedException(ProjectWriteAdmission admission)
    : InvalidOperationException($"Project '{admission.ProjectId:D}' in database profile '{admission.DatabaseProfileId:D}' no longer admits writes for lifetime '{admission.LifetimeId:D}'.") {
    public ProjectWriteAdmission Admission { get; } = admission;
}
