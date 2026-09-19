using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

/// <summary>
/// Continuation state of a prepared process launch, as a JSON integer: 0 Prepared, 1 Accepted, 2 Continuing, 3 Started,
/// 4 ReconciliationRequired, 5 Failed.
/// </summary>
public enum ProcessLaunchContinuationState {
    Prepared,
    Accepted,
    Continuing,
    Started,
    ReconciliationRequired,
    Failed
}

/// <summary>
/// Delivery state of the link from a launched run back to its source, as a JSON integer: 0 NotRequested, 1 Pending, 2
/// Delivered, 3 Removed, 4 Conflict.
/// </summary>
public enum ProcessLaunchLinkDeliveryState {
    NotRequested,
    Pending,
    Delivered,
    Removed,
    Conflict
}

public enum ProcessLaunchLinkConflictReason {
    SourceBindingChanged,
    ProjectRetired,
    AuthorityDenied
}

public sealed record ProcessLaunchLinkTarget(
    Guid ProjectId,
    string SourceNodeKey,
    string SourceBindingFingerprint);

public sealed record ProcessPreparedLaunch(
    ProcessLaunchAdmissionId AdmissionId,
    ProcessLaunchIntentId? CallerIntentId,
    string RequestFingerprint,
    ProcessLaunchAuthority? Authority,
    ProcessLaunchRequest Request,
    ProcessRuntimeCommitRequest InitialCommit,
    ProcessLaunchPlanView Review,
    ProcessLaunchLinkTarget? LinkTarget,
    DateTimeOffset PreparedAtUtc) {
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProcessLaunchToolSource? ToolSource { get; init; }
}

public sealed record ProcessPreparedLaunchSnapshot(
    ProcessPreparedLaunch Preparation,
    string PreparationFingerprint,
    long AdmissionSequence,
    ProcessLaunchContinuationState State,
    DateTimeOffset? AcceptedAtUtc,
    bool? Execute,
    ProcessLaunchLinkDeliveryState LinkDeliveryState,
    Guid? DeliveredLinkId,
    string? PublicFailure) {
    public ProcessLaunchLinkConflictReason? LinkConflictReason { get; init; }
}

public sealed record ProcessLaunchContinuationClaim(
    ProcessPreparedLaunchSnapshot Snapshot,
    Guid Owner,
    long Generation);

public sealed record ProcessLaunchLinkReceipt(
    ProcessLaunchAdmissionId AdmissionId,
    ProcessLaunchLinkDeliveryState State,
    Guid? LinkId) {
    public ProcessLaunchLinkConflictReason? ConflictReason { get; init; }

    [JsonIgnore]
    public Exception? ObservationException { get; init; }
}

public interface IProcessPreparedLaunchStore {
    Task<ProcessPreparedLaunchSnapshot?> FindByIntentAsync(ProcessLaunchIntentId intentId, CancellationToken cancellationToken = default);
    Task<ProcessPreparedLaunchSnapshot?> GetAsync(ProcessLaunchAdmissionId admissionId, CancellationToken cancellationToken = default);
    Task<ProcessPreparedLaunchSnapshot?> FindByRunAsync(ProcessRunId runId, CancellationToken cancellationToken = default);
    Task<ProcessPreparedLaunchSnapshot> PrepareAsync(ProcessPreparedLaunch preparation, CancellationToken cancellationToken = default);
    Task<ProcessLaunchContinuationClaim?> ClaimContinuationAsync(ProcessLaunchAdmissionId admissionId, CancellationToken cancellationToken = default);
    Task<bool> RenewContinuationAsync(ProcessLaunchContinuationClaim claim, CancellationToken cancellationToken = default);
    Task CompleteContinuationAsync(ProcessLaunchContinuationClaim claim, ProcessLaunchContinuationState state,
        string? publicFailure, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcessLaunchAdmissionId>> ListPendingContinuationsAsync(int take, CancellationToken cancellationToken = default);
}

public interface IProcessLaunchLinkReceiptStore {
    Task<ProcessPreparedLaunchSnapshot> RequireForDeliveryAsync(ProcessLaunchAdmissionId admissionId,
        string preparationFingerprint, CancellationToken cancellationToken = default);
    Task<ProcessLaunchLinkReceipt> StageDeliveredAsync(ProcessLaunchAdmissionId admissionId, string preparationFingerprint,
        Guid linkId, CancellationToken cancellationToken = default);
    Task<ProcessLaunchLinkReceipt> StageConflictAsync(ProcessLaunchAdmissionId admissionId, string preparationFingerprint,
        ProcessLaunchLinkConflictReason reason, CancellationToken cancellationToken = default);
    Task<ProcessLaunchLinkReceipt> StageRemovedAsync(ProcessLaunchAdmissionId admissionId, string preparationFingerprint,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessLaunchIntentConflictException(ProcessLaunchIntentId? intentId, string message)
    : InvalidOperationException(message) {
    public ProcessLaunchIntentId? IntentId { get; } = intentId;
}

/// <summary>Observed state of a prepared process launch.</summary>
/// <param name="AdmissionId">Launch admission identifier, as an object whose <c>value</c> is a GUID.</param>
/// <param name="AcceptedRunId">
/// Process run the launch created, as an object whose <c>value</c> is a GUID; null until a run was accepted.
/// </param>
/// <param name="ContinuationState">
/// Continuation state, as a JSON integer: 0 Prepared, 1 Accepted, 2 Continuing, 3 Started, 4 ReconciliationRequired,
/// 5 Failed.
/// </param>
/// <param name="LinkDeliveryState">
/// Delivery state of the link back to the source, as a JSON integer: 0 NotRequested, 1 Pending, 2 Delivered, 3
/// Removed, 4 Conflict.
/// </param>
/// <param name="PublicFailure">Failure message that is safe to show; null unless the launch failed.</param>
public sealed record ProcessLaunchObservation(
    ProcessLaunchAdmissionId AdmissionId,
    ProcessRunId? AcceptedRunId,
    ProcessLaunchContinuationState ContinuationState,
    ProcessLaunchLinkDeliveryState LinkDeliveryState,
    string? PublicFailure) {
    /// <summary>
    /// Runtime status of the accepted run, as a JSON integer: 0 Created, 1 Active, 2 Waiting, 3 Blocked, 4 Completed,
    /// 5 Failed, 6 CancelRequested, 7 Cancelled, 8 Escalated, 9 WaitingForUser; null when it was not read.
    /// </summary>
    public ProcessRuntimeStatus? RuntimeStatus { get; init; }

    [JsonIgnore]
    public Exception? ObservationException { get; init; }
}

public interface IProcessLaunchAuthorityPolicy {
    Task<IProcessLaunchAuthorityLease> AcquireAsync(ProcessLaunchAuthority saved, ProcessLaunchAuthority currentCaller,
        CancellationToken cancellationToken = default);
    Task RequireCurrentAsync(ProcessLaunchAuthority authority, CancellationToken cancellationToken = default);
}

public interface IProcessLaunchAuthorityLease : IAsyncDisposable {
    Task RequireForMutationAsync(ProcessLaunchLinkTarget? linkTarget, CancellationToken cancellationToken = default);
}
