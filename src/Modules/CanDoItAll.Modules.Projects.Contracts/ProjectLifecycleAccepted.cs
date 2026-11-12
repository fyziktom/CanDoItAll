namespace CanDoItAll.Modules.Projects;

public abstract record ProjectLifecycleAcknowledgement {
    private ProjectLifecycleAcknowledgement() {
    }

    public sealed record DeletionCommitted : ProjectLifecycleAcknowledgement {
        public DeletionCommitted(ProjectDeletionCompletionBinding completion) {
            ArgumentNullException.ThrowIfNull(completion);
            Completion = completion;
        }

        public ProjectDeletionCompletionBinding Completion { get; }
    }

    public sealed record LinkEnsured : ProjectLifecycleAcknowledgement {
        public LinkEnsured(Guid edgeId, bool created) {
            RequireEdge(edgeId);
            EdgeId = edgeId;
            Created = created;
        }

        public Guid EdgeId { get; }
        public bool Created { get; }
    }

    public sealed record LinkRemoved : ProjectLifecycleAcknowledgement;

    public sealed record LinkReconnected : ProjectLifecycleAcknowledgement {
        public LinkReconnected(Guid destinationEdgeId, bool created) {
            RequireEdge(destinationEdgeId);
            DestinationEdgeId = destinationEdgeId;
            Created = created;
        }

        public Guid DestinationEdgeId { get; }
        public bool Created { get; }
    }

    private static void RequireEdge(Guid id) {
        if (id == Guid.Empty) {
            throw new ArgumentException("An accepted hierarchy result requires its actual edge identity.", nameof(id));
        }
    }
}

public sealed record ProjectLifecycleAccepted {
    public const int VersionOne = 1;
    public const int MaximumPayloadBytes = 16 * 1024;

    public ProjectLifecycleAccepted(ProjectLifecycleSubmission original, int resultVersion, DateTimeOffset acceptedAtUtc,
        DateTimeOffset replayUntilUtc, ProjectLifecycleAcknowledgement acknowledgement) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(acknowledgement);
        if (resultVersion != VersionOne || original.Interpretation != ProjectLifecycleInterpretation.VersionOne ||
                !FiniteUtc(acceptedAtUtc) || !FiniteUtc(replayUntilUtc) || replayUntilUtc <= acceptedAtUtc) {
            throw new ArgumentException("A lifecycle acknowledgement requires supported versions and ordered finite UTC times.");
        }
        var matches = (original.Command, acknowledgement) switch {
            (ProjectLifecycleCommand.Delete command, ProjectLifecycleAcknowledgement.DeletionCommitted accepted) =>
                accepted.Completion.Original.Project == command.Expected.Project &&
                accepted.Completion.Original.Completion.DatabaseProfileId == original.Operation.DatabaseProfileId &&
                accepted.Completion.Original.Completion.Namespace == original.Operation.Namespace,
            (ProjectLifecycleCommand.EnsureLink, ProjectLifecycleAcknowledgement.LinkEnsured) => true,
            (ProjectLifecycleCommand.RemoveLink, ProjectLifecycleAcknowledgement.LinkRemoved) => true,
            (ProjectLifecycleCommand.ReconnectLink command, ProjectLifecycleAcknowledgement.LinkReconnected accepted) =>
                accepted.DestinationEdgeId != command.Expected.EdgeId,
            _ => false
        };
        if (!matches) {
            throw new ArgumentException("The accepted lifecycle result must match the exact original command and target.");
        }
        Original = original;
        ResultVersion = resultVersion;
        AcceptedAtUtc = acceptedAtUtc;
        ReplayUntilUtc = replayUntilUtc;
        Acknowledgement = acknowledgement;
    }

    public ProjectLifecycleSubmission Original { get; }
    public int ResultVersion { get; }
    public DateTimeOffset AcceptedAtUtc { get; }
    public DateTimeOffset ReplayUntilUtc { get; }
    public ProjectLifecycleAcknowledgement Acknowledgement { get; }

    private static bool FiniteUtc(DateTimeOffset value)
        => value.Offset == TimeSpan.Zero && value != DateTimeOffset.MinValue && value != DateTimeOffset.MaxValue;
}
