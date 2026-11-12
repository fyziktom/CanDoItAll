namespace CanDoItAll.Modules.Projects;

public enum ProjectDeletionRecoveryPrecondition { AllWriterHostsStopped = 1 }

public enum ProjectDeletionLegacyPrecondition { AllWriterAndFormerSourceOwnerHostsStopped = 1 }

public sealed record ProjectDeletionLegacyPlan(ProjectDeletionPreparationContext Original,
    ProjectDeletionRecoveryDigest Digest, bool Installed, IReadOnlyList<ProjectDeletionRecoveryParticipantEvidence> Participants);

public enum ProjectDeletionRecoveryRefusal {
    MissingCompletion = 1, BindingChanged, PlanChanged, UnsupportedEvidence, ParticipantMismatch,
    ExclusionBusy, ProbeReleaseUnconfirmed, ReadUnavailable, Cancelled
}

public sealed record ProjectDeletionRecoveryPlan(ProjectDeletionCompletionBinding Completion,
    ProjectDeletionRecoveryDigest Digest, DateTimeOffset? ReleasedAtUtc,
    IReadOnlyList<ProjectDeletionRecoveryParticipantEvidence> Participants);

public abstract record ProjectDeletionRecoveryResult {
    private ProjectDeletionRecoveryResult() {
    }

    public sealed record Observed(ProjectDeletionRecoveryPlan Plan) : ProjectDeletionRecoveryResult;
    public sealed record LegacyObserved(ProjectDeletionLegacyPlan Plan) : ProjectDeletionRecoveryResult;
    public sealed record InstalledAwaitingRelease(ProjectDeletionPreparationContext Original,
        ProjectDeletionRecoveryDigest InstallationPlan, bool CleanupUnconfirmed = false,
        bool DiagnosticsUnavailable = false) : ProjectDeletionRecoveryResult;
    public sealed record Released(ProjectDeletionCompletionAddress Address, DateTimeOffset ReleasedAtUtc,
        bool CleanupUnconfirmed = false, bool DiagnosticsUnavailable = false) : ProjectDeletionRecoveryResult;
    public sealed record Refused(ProjectDeletionCompletionAddress Address, ProjectDeletionRecoveryRefusal Reason,
        bool DiagnosticsUnavailable = false) : ProjectDeletionRecoveryResult;
    public sealed record Unconfirmed(ProjectDeletionCompletionAddress Address, bool CleanupUnconfirmed,
        bool DiagnosticsUnavailable = false) : ProjectDeletionRecoveryResult;
}

public interface IProjectDeletionRecoveryEnvironment {
    ProjectDeletionRecoveryDigest ProfileIdentity { get; }
    Task RequireOriginalProfileAsync(CancellationToken cancellationToken);
    Task RequireOriginalSourceAsync(ProjectDeletionSourceRecovery source, CancellationToken cancellationToken);
    ValueTask<IAsyncDisposable> AcquireSourceExclusionAsync(ProjectDeletionSourceRecovery source, CancellationToken cancellationToken);
}

internal sealed class ProjectDeletionRecoveryRefusedException(ProjectDeletionRecoveryRefusal reason)
    : InvalidOperationException($"Deletion recovery refused: {reason}.") {
    internal ProjectDeletionRecoveryRefusal Reason { get; } = reason;
}

internal sealed class ProjectDeletionRecoveryCleanupException(Exception inner)
    : Exception("Deletion recovery could not confirm terminal resource cleanup.", inner);
