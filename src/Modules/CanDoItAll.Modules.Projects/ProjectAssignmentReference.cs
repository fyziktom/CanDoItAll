namespace CanDoItAll.Modules.Projects;

public sealed record ProjectAssignmentReference {
    public ProjectAssignmentReference(Guid databaseProfileId, Guid projectId, Guid? lifetimeId) {
        if (databaseProfileId == Guid.Empty || projectId == Guid.Empty || lifetimeId == Guid.Empty) {
            throw new ArgumentException("An assignment reference requires a database profile, project and a nonempty lifetime when bound.");
        }
        DatabaseProfileId = databaseProfileId;
        ProjectId = projectId;
        LifetimeId = lifetimeId;
    }

    public Guid DatabaseProfileId { get; }
    public Guid ProjectId { get; }
    public Guid? LifetimeId { get; }

    public static ProjectAssignmentReference From(ProjectWriteAdmission admission) {
        ArgumentNullException.ThrowIfNull(admission);
        return new(admission.DatabaseProfileId, admission.ProjectId, admission.LifetimeId);
    }

    public void RequireProfile(Guid databaseProfileId, Guid projectId) {
        if (DatabaseProfileId != databaseProfileId || ProjectId != projectId) {
            throw new InvalidOperationException("The assignment reference does not match this database profile and project.");
        }
    }

    public ProjectWriteAdmission RequireBoundAdmission() => LifetimeId is { } lifetimeId
        ? new(DatabaseProfileId, ProjectId, lifetimeId)
        : throw new InvalidOperationException("Historical assignment cleanup has no captured project lifetime and requires reconciliation.");
}

public static class ProjectAssignmentAdmission {
    public const string RefreshRequiredErrorCode = "projects.assignment-lifetime-refresh-required";

    public static ProjectWriteAdmission Require(Guid projectId, ProjectWriteAdmission? expected) {
        if (expected is null || expected.ProjectId != projectId) {
            throw new InvalidOperationException("The assignment mutation requires the project lifetime captured by its caller.");
        }
        return expected;
    }

    public static ProjectPartyAssignmentUpsertRequest[] Snapshot(Guid projectId,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> requests, ProjectWriteAdmission expected) {
        ArgumentNullException.ThrowIfNull(requests);
        Require(projectId, expected);
        return requests.Select(request => {
            var snapshot = request.Snapshot();
            if (snapshot.ExpectedProjectAdmission is not null && snapshot.ExpectedProjectAdmission != expected) {
                throw new InvalidOperationException("The assignment batch contains a different captured project lifetime.");
            }
            snapshot.ExpectedProjectAdmission = expected;
            return snapshot;
        }).ToArray();
    }
}
