using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects.Pages.Components;

public sealed record ProjectLifecyclePendingScope(Guid DatabaseProfileId, DatasetNamespaceId Namespace, ProjectCallerBinding CallerBinding) {
    public bool Contains(ProjectLifecycleOperation operation) => operation.DatabaseProfileId == DatabaseProfileId &&
        operation.Namespace == Namespace && operation.CallerBinding == CallerBinding;
}

public sealed record ProjectLifecyclePendingRecord(ProjectLifecycleFrozenSubmission Frozen, ProjectPendingRevision Revision,
    ProjectPendingStage Stage, Guid? AttemptId, bool HadUnconfirmedDispatch, ProjectLifecycleReceiptPayload? Accepted,
    ProjectLifecycleAbsencePayload? Absence) {
    public ProjectLifecycleOperation Operation => Frozen.Submission.Operation;
}

public sealed record ProjectLifecyclePendingChange(ProjectPendingStoreStatus Status, ProjectLifecyclePendingRecord? Record);
public sealed record ProjectLifecyclePendingMetadata {
    public ProjectLifecyclePendingMetadata(ProjectLifecycleOperation operation, ProjectPendingRevision revision,
        ProjectPendingStage stage, ProjectLifecycleCommandKind kind, IReadOnlyList<Guid> targetProjectIds, bool hadUnconfirmedDispatch) {
        ArgumentNullException.ThrowIfNull(targetProjectIds);
        Operation = operation;
        Revision = revision;
        Stage = stage;
        Kind = kind;
        TargetProjectIds = Array.AsReadOnly(targetProjectIds.ToArray());
        HadUnconfirmedDispatch = hadUnconfirmedDispatch;
    }

    public ProjectLifecycleOperation Operation { get; }
    public ProjectPendingRevision Revision { get; }
    public ProjectPendingStage Stage { get; }
    public ProjectLifecycleCommandKind Kind { get; }
    public IReadOnlyList<Guid> TargetProjectIds { get; }
    public bool HadUnconfirmedDispatch { get; }
}

public sealed record ProjectLifecyclePendingPage {
    public ProjectLifecyclePendingPage(IReadOnlyList<ProjectLifecyclePendingMetadata> items, ProjectLifecycleOperation? next) {
        ArgumentNullException.ThrowIfNull(items);
        Items = Array.AsReadOnly(items.ToArray());
        Next = next;
    }

    public IReadOnlyList<ProjectLifecyclePendingMetadata> Items { get; }
    public ProjectLifecycleOperation? Next { get; }
}

public sealed record ProjectLifecyclePendingStoreLimits {
    public const int MaximumSubmissionBytes = 4096;
    public const int MaximumEvidenceBytes = ProjectLifecycleAccepted.MaximumPayloadBytes;

    public ProjectLifecyclePendingStoreLimits(int maximumRecordCount, long maximumTotalBytes, int maximumPageSize) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecordCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPageSize);
        if (maximumTotalBytes <= 0 || maximumTotalBytes > ProjectPendingRevision.Maximum) {
            throw new ArgumentOutOfRangeException(nameof(maximumTotalBytes));
        }
        MaximumRecordCount = maximumRecordCount;
        MaximumTotalBytes = maximumTotalBytes;
        MaximumPageSize = maximumPageSize;
    }

    public int MaximumRecordCount { get; }
    public long MaximumTotalBytes { get; }
    public int MaximumPageSize { get; }
}

public static class ProjectLifecyclePendingTargets {
    public static IReadOnlyList<Guid> Capture(ProjectLifecycleCommand command) {
        Guid[] targets = command switch {
            ProjectLifecycleCommand.Delete value => [value.Expected.Project.ProjectId],
            ProjectLifecycleCommand.EnsureLink value => [value.Parent.ProjectId, value.Child.ProjectId],
            ProjectLifecycleCommand.RemoveLink value => [value.Expected.Parent.ProjectId, value.Expected.Child.ProjectId],
            ProjectLifecycleCommand.ReconnectLink value => [value.Expected.Parent.ProjectId, value.Expected.Child.ProjectId, value.NewParent.ProjectId],
            _ => throw new ArgumentException("A pending lifecycle operation requires a supported command.", nameof(command))
        };
        return Array.AsReadOnly(targets.OrderBy(value => value.ToString("D"), StringComparer.Ordinal).ToArray());
    }
}

public interface IProjectLifecyclePendingStore {
    ValueTask InitializeAsync(CancellationToken cancellationToken = default);
    ValueTask<ProjectLifecyclePendingChange> PrepareAsync(ProjectLifecycleFrozenSubmission frozen, CancellationToken cancellationToken = default);
    ValueTask<ProjectLifecyclePendingChange> BeginDispatchAsync(ProjectLifecyclePendingRecord original, Guid attemptId,
        bool explicitRetry, CancellationToken cancellationToken = default);
    ValueTask<ProjectLifecyclePendingChange> ObserveAcceptedAsync(ProjectLifecycleFrozenSubmission original,
        ProjectLifecycleAccepted accepted, CancellationToken cancellationToken = default);
    ValueTask<ProjectLifecyclePendingChange> ObserveAttemptAsync(ProjectLifecycleOperation original, Guid attemptId,
        ProjectPendingAttemptOutcome outcome, CancellationToken cancellationToken = default);
    ValueTask<ProjectLifecyclePendingChange> ObserveClosedAbsentAsync(ProjectLifecycleFrozenSubmission original,
        ProjectLifecycleLookup.ClosedAbsent absence, CancellationToken cancellationToken = default);
    ValueTask<ProjectLifecyclePendingRecord?> ReadAsync(ProjectLifecycleOperation original, CancellationToken cancellationToken = default);
    ValueTask<ProjectLifecyclePendingPage> ListAsync(ProjectLifecyclePendingScope scope, int take,
        ProjectLifecycleOperation? after = null, CancellationToken cancellationToken = default);
    ValueTask<ProjectLifecyclePendingChange> CancelPreparedAsync(ProjectLifecyclePendingRecord original, CancellationToken cancellationToken = default);
    ValueTask<ProjectLifecyclePendingChange> ClearResolvedAsync(ProjectLifecyclePendingRecord original, CancellationToken cancellationToken = default);
}
