using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public enum ProcessLaunchContinuationState {
    Prepared,
    Accepted,
    Continuing,
    Started,
    ReconciliationRequired,
    Failed
}

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
    DateTimeOffset PreparedAtUtc);

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

public sealed record ProcessLaunchObservation(
    ProcessLaunchAdmissionId AdmissionId,
    ProcessRunId? AcceptedRunId,
    ProcessLaunchContinuationState ContinuationState,
    ProcessLaunchLinkDeliveryState LinkDeliveryState,
    string? PublicFailure) {
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
