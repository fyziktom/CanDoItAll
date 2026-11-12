using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects.Pages.Components;

public enum ProjectPendingStage { Prepared, Dispatching, Unconfirmed, Accepted, Rejected, ClosedAbsent }
public enum ProjectPendingStoreStatus { Applied, Unchanged, Changed, Missing }
public enum ProjectPendingAttemptOutcome { Rejected, Unconfirmed }
public enum ProjectPendingProblem { None, StorageFailed, RecordChanged, OutcomeUnconfirmed, ContextChanged, RecoveryUnavailable, ReconciliationFailed }

public sealed record ProjectPendingRevision {
    public const long Maximum = 9_007_199_254_740_991;

    public ProjectPendingRevision(long value) {
        if (value <= 0 || value > Maximum) {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
        Value = value;
    }

    public long Value { get; }
}

public sealed record ProjectPendingScope(DatasetNamespaceId Namespace, ProjectCallerBinding CallerBinding) {
    public bool Contains(ProjectDefinitionOperation operation) => operation.Namespace == Namespace && operation.CallerBinding == CallerBinding;
}

public sealed record ProjectPendingSubmission(
    ProjectDefinitionFrozenSubmission Frozen,
    ProjectPendingRevision Revision,
    ProjectPendingStage Stage,
    Guid? AttemptId,
    bool HadUnconfirmedDispatch,
    bool HadStarterPlan,
    ProjectDefinitionReceiptPayload? Accepted,
    ProjectDefinitionLookup.ClosedAbsent? Absence) {
    public ProjectDefinitionOperation Operation => Frozen.Submission.Operation;
}

public sealed record ProjectPendingSubmissionMetadata(
    ProjectDefinitionOperation Operation,
    ProjectPendingRevision Revision,
    ProjectPendingStage Stage,
    Guid? TargetProjectId,
    bool HadUnconfirmedDispatch,
    bool HadStarterPlan);

public sealed record ProjectPendingStoreChange(ProjectPendingStoreStatus Status, ProjectPendingSubmission? Record);

public sealed record ProjectPendingSubmissionPage {
    public ProjectPendingSubmissionPage(IReadOnlyList<ProjectPendingSubmissionMetadata> items, ProjectDefinitionOperation? next) {
        ArgumentNullException.ThrowIfNull(items);
        Items = Array.AsReadOnly(items.ToArray());
        Next = next;
    }

    public IReadOnlyList<ProjectPendingSubmissionMetadata> Items { get; }
    public ProjectDefinitionOperation? Next { get; }
}

public sealed record ProjectPendingStoreLimits {
    public ProjectPendingStoreLimits(int maximumRecordCount, long maximumTotalBytes, int maximumPageSize, ProjectDefinitionLimits definition) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecordCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPageSize);
        if (maximumTotalBytes <= 0 || maximumTotalBytes > ProjectPendingRevision.Maximum) {
            throw new ArgumentOutOfRangeException(nameof(maximumTotalBytes));
        }
        ArgumentNullException.ThrowIfNull(definition);
        if (definition != ProjectDefinitionLimits.VersionOneMaximum) {
            throw new ArgumentException("Browser schema one requires the retained protocol bounds; current admission limits apply separately to new submissions.", nameof(definition));
        }
        MaximumRecordCount = maximumRecordCount;
        MaximumTotalBytes = maximumTotalBytes;
        MaximumPageSize = maximumPageSize;
        Definition = definition;
    }

    public int MaximumRecordCount { get; }
    public long MaximumTotalBytes { get; }
    public int MaximumPageSize { get; }
    public ProjectDefinitionLimits Definition { get; }
}

public interface IProjectPendingSubmissionStore {
    ValueTask InitializeAsync(CancellationToken cancellationToken = default);
    ValueTask<ProjectPendingStoreChange> PrepareAsync(ProjectDefinitionFrozenSubmission frozen, bool hadStarterPlan, CancellationToken cancellationToken = default);
    ValueTask<ProjectPendingStoreChange> BeginDispatchAsync(ProjectPendingSubmission record, Guid attemptId, bool explicitRetry, CancellationToken cancellationToken = default);
    ValueTask<ProjectPendingStoreChange> ObserveAcceptedAsync(ProjectDefinitionFrozenSubmission frozen, ProjectDefinitionAcceptedReceipt receipt, CancellationToken cancellationToken = default);
    ValueTask<ProjectPendingStoreChange> ObserveAttemptAsync(ProjectDefinitionOperation operation, Guid attemptId, ProjectPendingAttemptOutcome outcome, CancellationToken cancellationToken = default);
    ValueTask<ProjectPendingStoreChange> ObserveClosedAbsentAsync(ProjectDefinitionFrozenSubmission frozen, ProjectDefinitionLookup.ClosedAbsent absence, CancellationToken cancellationToken = default);
    ValueTask<ProjectPendingSubmission?> ReadAsync(ProjectDefinitionOperation operation, CancellationToken cancellationToken = default);
    ValueTask<ProjectPendingSubmissionPage> ListAsync(ProjectPendingScope scope, int take, ProjectDefinitionOperation? after = null, CancellationToken cancellationToken = default);
    ValueTask<ProjectPendingStoreChange> CancelPreparedAsync(ProjectPendingSubmission record, CancellationToken cancellationToken = default);
    ValueTask<ProjectPendingStoreChange> ClearResolvedAsync(ProjectPendingSubmission record, CancellationToken cancellationToken = default);
}

public abstract record ProjectPendingDispatchOutcome {
    private ProjectPendingDispatchOutcome() {
    }

    public sealed record Full(ProjectDefinitionOutcome<ProjectEditorAcknowledgement> Outcome) : ProjectPendingDispatchOutcome;
    public sealed record Scalars(ProjectDefinitionOutcome<ProjectDefinitionScalarsAcknowledgement> Outcome) : ProjectPendingDispatchOutcome;
}
