namespace CanDoItAll.Modules.Projects;

public interface IProjectDeletionCompletionQuery {
    Task<ProjectDeletionCompletionObservation> ObserveAsync(ProjectDeletionCompletionBinding original,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Exact full-binding deletion release observation is not supported.");

    Task<ProjectDeletionCompletionObservation> ObserveAsync(ProjectDeletionPreparationContext original,
        ProjectDeletionObligation obligation, CancellationToken cancellationToken = default);
}

public enum ProjectDeletionCompletionUnavailableReason {
    OriginalProfileChanged,
    DatasetChanged,
    MissingCompletion,
    MissingNativeRetirement,
    UnsupportedVersion,
    InvalidStoredBinding,
    ObligationMismatch,
    ReadUnavailable
}

public abstract record ProjectDeletionCompletionObservation {
    private ProjectDeletionCompletionObservation() { }

    public sealed record AwaitingOwnerRelease : ProjectDeletionCompletionObservation;
    public sealed record Released(DateTimeOffset ReleasedAtUtc) : ProjectDeletionCompletionObservation;
    public sealed record Unavailable(ProjectDeletionCompletionUnavailableReason Reason) : ProjectDeletionCompletionObservation;
}
