namespace CanDoItAll.Modules.Projects;

public enum ProjectLifecycleUnavailableReason { ReadUnavailable, MissingMetadata, UnsupportedProtocol, InconsistentMetadata, UnsupportedResult, InvalidStoredResult, AccessDenied }
public enum ProjectLifecycleContextChange { Profile, Dataset, Caller }

public abstract record ProjectLifecycleLookup {
    private ProjectLifecycleLookup() {
    }

    public sealed record Found(ProjectLifecycleAccepted Accepted) : ProjectLifecycleLookup;
    public sealed record NotObserved(ProjectLifecycleOperation Original, ProjectOperationObservationState State) : ProjectLifecycleLookup;
    public sealed record ClosedAbsent(ProjectLifecycleOperation Original, DateTimeOffset ClosedAtUtc,
        DateTimeOffset ObservedAtUtc, DateTimeOffset ReplayUntilUtc) : ProjectLifecycleLookup;
    public sealed record Expired(ProjectLifecycleOperation Original) : ProjectLifecycleLookup;
    public sealed record UnknownFutureEpoch(ProjectLifecycleOperation Original) : ProjectLifecycleLookup;
    public sealed record ContextChanged(ProjectLifecycleOperation Original, ProjectLifecycleContextChange Change) : ProjectLifecycleLookup;
    public sealed record Unavailable(ProjectLifecycleOperation Original, ProjectLifecycleUnavailableReason Reason) : ProjectLifecycleLookup;
}
