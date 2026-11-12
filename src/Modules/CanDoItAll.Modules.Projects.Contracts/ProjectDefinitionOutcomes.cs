using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

public sealed record ProjectDefinitionAccepted<TAcknowledgement> where TAcknowledgement : class {
    public ProjectDefinitionAccepted(ProjectDefinitionOperation operation, ProjectDefinitionResultVersion version,
        ProjectDefinitionCommandKind kind, DateTimeOffset acceptedAtUtc, DateTimeOffset replayUntilUtc,
        TAcknowledgement acknowledgement) {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(acknowledgement);
        if (acceptedAtUtc.Offset != TimeSpan.Zero || replayUntilUtc.Offset != TimeSpan.Zero || replayUntilUtc <= acceptedAtUtc) {
            throw new ArgumentException("An accepted result requires ordered UTC acceptance and replay timestamps.");
        }
        var frozen = acknowledgement switch {
            ProjectEditorAcknowledgement full when kind is ProjectDefinitionCommandKind.Create or ProjectDefinitionCommandKind.Replace
                => (object)(full with {
                    Phases = ProjectDefinitionCollections.Snapshot(full.Phases, nameof(full.Phases)),
                    Options = ProjectDefinitionCollections.Snapshot(full.Options, nameof(full.Options))
                }),
            ProjectDefinitionScalarsAcknowledgement scalar when kind == ProjectDefinitionCommandKind.UpdateScalars => scalar,
            _ => throw new ArgumentException("The acknowledgement must match its definition command kind.", nameof(acknowledgement))
        };
        Operation = operation;
        Version = version;
        Kind = kind;
        AcceptedAtUtc = acceptedAtUtc;
        ReplayUntilUtc = replayUntilUtc;
        Acknowledgement = (TAcknowledgement)frozen;
    }

    public ProjectDefinitionOperation Operation { get; }
    public ProjectDefinitionResultVersion Version { get; }
    public ProjectDefinitionCommandKind Kind { get; }
    public DateTimeOffset AcceptedAtUtc { get; }
    public DateTimeOffset ReplayUntilUtc { get; }
    public TAcknowledgement Acknowledgement { get; }
}

public enum ProjectDefinitionRecoveryReason { CommitUnconfirmed = 1, RecoveryUnavailable = 2 }
public enum ProjectOperationObservationState { Open = 1, Draining = 2 }
public enum ProjectDefinitionDetailUnavailableReason { AccessDenied = 1, TargetUnavailable = 2, UnsupportedVersion = 3, InvalidStoredResult = 4 }
public enum ProjectDefinitionInvalidContextReason { UnknownFutureEpoch = 1 }

public abstract record ProjectDefinitionOutcome<TAcknowledgement> where TAcknowledgement : class {
    private ProjectDefinitionOutcome() {
    }

    public sealed record Accepted(ProjectDefinitionAccepted<TAcknowledgement> Result, bool Replayed)
        : ProjectDefinitionOutcome<TAcknowledgement>;

    public sealed record Rejected : ProjectDefinitionOutcome<TAcknowledgement> {
        public Rejected(ProjectDefinitionOperation? operation, IReadOnlyList<Error> errors) {
            Operation = operation;
            Errors = ProjectDefinitionCollections.Snapshot(errors, nameof(errors));
            if (Errors.Count == 0) {
                throw new ArgumentException("A rejected attempt requires an error.", nameof(errors));
            }
        }

        public ProjectDefinitionOperation? Operation { get; }
        public IReadOnlyList<Error> Errors { get; }
    }

    public sealed record Unconfirmed(ProjectDefinitionOperation Operation, ProjectDefinitionRecoveryReason Reason)
        : ProjectDefinitionOutcome<TAcknowledgement>;
}

public abstract record ProjectDefinitionLookup {
    private ProjectDefinitionLookup() {
    }

    public sealed record FoundFull(ProjectDefinitionAccepted<ProjectEditorAcknowledgement> Result) : ProjectDefinitionLookup;
    public sealed record FoundScalars(ProjectDefinitionAccepted<ProjectDefinitionScalarsAcknowledgement> Result) : ProjectDefinitionLookup;
    public sealed record NotObserved(ProjectDefinitionOperation Operation, ProjectOperationObservationState State) : ProjectDefinitionLookup;
    public sealed record ClosedAbsent(ProjectDefinitionOperation Operation, DateTimeOffset ClosedAtUtc,
        DateTimeOffset ObservedAtUtc, DateTimeOffset RetainedUntilUtc) : ProjectDefinitionLookup;
    public sealed record Expired(ProjectDefinitionOperation Operation) : ProjectDefinitionLookup;
    public sealed record DatasetChanged(ProjectDefinitionOperation Operation) : ProjectDefinitionLookup;
    public sealed record CallerContextChanged(ProjectDefinitionOperation Operation) : ProjectDefinitionLookup;
    public sealed record InvalidContext(ProjectDefinitionOperation Operation, ProjectDefinitionInvalidContextReason Reason) : ProjectDefinitionLookup;
    public sealed record DetailUnavailable(ProjectDefinitionOperation Operation, ProjectDefinitionDetailUnavailableReason Reason)
        : ProjectDefinitionLookup;
}
