namespace CanDoItAll.Infrastructure.Storage;

/// <summary>
/// Database runtime context of a storage placement recovery request: the host's active database profile and its
/// generation, as returned by <c>GET /api/storage-placement-recovery/context</c>. Send it unchanged with every recovery
/// request. A context that is no longer current is rejected with HTTP 409 (<c>StaleContext</c>), for example after the
/// host switched its database profile; read the context again.
/// </summary>
/// <param name="DatabaseProfileId">Identifier (GUID) of the host's active database profile; not the empty GUID.</param>
/// <param name="Generation">
/// Generation of the host's database runtime, an integer that changes when the active database changes; not negative.
/// </param>
public sealed record StoragePlacementRecoveryContext(Guid DatabaseProfileId, long Generation);

public enum StoragePlacementRecoveryOperation {
    Read,
    Reconcile,
    VerifyExternalTermination
}

/// <summary>
/// Recovery action the caller may take for a stable storage placement, as a JSON integer: 0 None (no action; the
/// placement's <c>block</c> says why), 1 Reconcile (read back the original target with
/// <c>POST /api/storage-placement-recovery/reconcile</c>) or 2 VerifyExternalTermination (an operator must first attest
/// with <c>POST /api/storage-placement-recovery/verify-external-termination</c> that an unacknowledged FTP upload has
/// stopped).
/// </summary>
public enum StoragePlacementRecoveryAction {
    None,
    Reconcile,
    VerifyExternalTermination
}

/// <summary>
/// Why no recovery action is available for a stable storage placement, as a JSON integer: 0 None (not blocked), 1
/// ReadOnlyAuthority (the caller may read but lacks the scopes to act), 2 MissingOwnerAssociation (no project record
/// owns the placement), 3 InvalidOwnerAssociation (the owning record is inconsistent or ambiguous), 4
/// OriginalProjectLifetimeMissing (the owning workflow record carries no project lifetime), 5
/// OriginalProjectUnavailable (the original project no longer exists in the lifetime it had, or belongs to another
/// database profile), 6 ImportedHistory (imported history, which is not recovered here), 7 OriginalStorageUnavailable
/// (the original storage is missing, disabled, unavailable, read-only or lacks a required capability), 8
/// OriginalTargetConflict (the storage endpoint, configuration or host binding changed; rebind the storage first), 9
/// DispatchNotStarted (the write was never dispatched; there is nothing to read back), 10 RetainedConflict (the target
/// holds different content, which is preserved) or 11 DeletionRequested (the placement was deleted or its deletion
/// was requested).
/// </summary>
public enum StoragePlacementRecoveryBlock {
    None,
    ReadOnlyAuthority,
    MissingOwnerAssociation,
    InvalidOwnerAssociation,
    OriginalProjectLifetimeMissing,
    OriginalProjectUnavailable,
    ImportedHistory,
    OriginalStorageUnavailable,
    OriginalTargetConflict,
    DispatchNotStarted,
    RetainedConflict,
    DeletionRequested
}

/// <summary>
/// Owner of a stable storage placement, the component that planned the write and records its result, as a JSON
/// integer: 0 Unknown (no single owner record), 1 ProcessAsset (an asset produced by a process step's agent run) or 2
/// WorkflowAsset (an asset produced by a workflow run).
/// </summary>
public enum StoragePlacementRecoveryOwner {
    Unknown,
    ProcessAsset,
    WorkflowAsset
}

/// <summary>
/// Identity of a stable storage placement: its intent and the project, storage and provider it was planned for. It
/// never contains paths or storage addresses.
/// </summary>
/// <param name="IntentId">Identifier of the placement intent, as an object whose <c>value</c> is the GUID.</param>
/// <param name="OriginalProjectId">
/// Identifier (GUID) of the project the placement was planned for; null when it was not planned for a project.
/// </param>
/// <param name="OriginalStorageId">
/// Identifier (GUID) of the storage catalog entry the placement was planned for.
/// </param>
/// <param name="OriginalProvider">
/// Storage provider of that entry, as a JSON integer: 0 FileSystem, 1 Ipfs or 2 Ftp.
/// </param>
public sealed record StoragePlacementRecoveryIdentity(StoragePlacementIntentId IntentId,
    Guid? OriginalProjectId, Guid OriginalStorageId, StorageProviderKind OriginalProvider);

public sealed record StoragePlacementRecoveryOwnerObservation(StoragePlacementRecoveryOwner Owner,
    bool NativeReceiptPresent, StoragePlacementRecoveryBlock Block);

/// <summary>
/// Recovery state of one stable storage placement: the state of its write, its receipts, its owner and the action
/// the caller may take. Returned by the storage placement recovery reads and commands; it contains no content,
/// storage addresses or paths.
/// </summary>
/// <param name="Context">The database runtime context the item was read under.</param>
/// <param name="Identity">Identity of the placement.</param>
/// <param name="StorageState">
/// State of the write, as a JSON integer: 0 Prepared (target reserved, write not dispatched), 1 Dispatching (write
/// dispatched, outcome not confirmed), 2 Completed (content verified at the target and storage receipt recorded), 3
/// Uncertain (outcome could not be verified; the target is kept), 4 Conflict (the target holds different content,
/// which is preserved) or 5 Deleted. Only Completed means the content is stored.
/// </param>
/// <param name="CreatedAtUtc">Instant (UTC, with offset) at which the placement was prepared.</param>
/// <param name="UpdatedAtUtc">Instant (UTC, with offset) of the last change to the placement record.</param>
/// <param name="StorageReceiptPresent">True when the storage receipt of the completed write is recorded.</param>
/// <param name="Owner">Owner of the placement, as a JSON integer: 0 Unknown, 1 ProcessAsset or 2 WorkflowAsset.</param>
/// <param name="NativeReceiptPresent">True when the owner recorded its own receipt for the placement.</param>
/// <param name="AvailableAction">
/// Action the caller may take now, as a JSON integer: 0 None, 1 Reconcile or 2 VerifyExternalTermination.
/// </param>
/// <param name="Block">
/// Why no action is available, as a JSON integer: 0 None, 1 ReadOnlyAuthority, 2 MissingOwnerAssociation, 3
/// InvalidOwnerAssociation, 4 OriginalProjectLifetimeMissing, 5 OriginalProjectUnavailable, 6 ImportedHistory, 7
/// OriginalStorageUnavailable, 8 OriginalTargetConflict, 9 DispatchNotStarted, 10 RetainedConflict or 11
/// DeletionRequested; the <c>StoragePlacementRecoveryBlock</c> schema explains each value.
/// </param>
public sealed record StoragePlacementRecoveryItem(StoragePlacementRecoveryContext Context,
    StoragePlacementRecoveryIdentity Identity, StorageStablePlacementState StorageState,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, bool StorageReceiptPresent,
    StoragePlacementRecoveryOwner Owner, bool NativeReceiptPresent, StoragePlacementRecoveryAction AvailableAction,
    StoragePlacementRecoveryBlock Block);

public sealed record StoragePlacementRecoveryQuery(StoragePlacementRecoveryContext Context,
    Guid? ProjectId = null, Guid? StorageId = null, int Take = 32, int Offset = 0);

/// <summary>
/// One page of unresolved stable storage placements, returned by <c>GET /api/storage-placement-recovery/pending</c>.
/// </summary>
/// <param name="Items">The placements of this page, oldest first; at most <c>take</c> items.</param>
/// <param name="NextOffset">
/// Value to send as <c>offset</c> for the next page; null when this is the last page.
/// </param>
public sealed record StoragePlacementRecoveryPage(IReadOnlyList<StoragePlacementRecoveryItem> Items, int? NextOffset);

/// <summary>
/// Request body of the storage placement recovery commands that act on one placement: the database runtime context and
/// the placement intent. Send both members as read from the recovery operations.
/// </summary>
/// <param name="Context">
/// The current database runtime context from <c>GET /api/storage-placement-recovery/context</c>.
/// </param>
/// <param name="IntentId">
/// The placement intent, as an object whose <c>value</c> is the intent GUID, for example
/// <c>{ "value": "3f2504e0-4f89-11d3-9a0c-0305e82c3301" }</c>. Must not be the empty GUID.
/// </param>
public sealed record StoragePlacementRecoveryCommand(StoragePlacementRecoveryContext Context, StoragePlacementIntentId IntentId);

/// <summary>
/// Request body of <c>POST /api/storage-placement-recovery/verify-external-termination</c>: an operator's attestation
/// that the unacknowledged FTP upload of a placement has stopped. The attestation is recorded durably.
/// </summary>
/// <param name="Context">
/// The current database runtime context from <c>GET /api/storage-placement-recovery/context</c>.
/// </param>
/// <param name="IntentId">
/// The placement intent, as an object whose <c>value</c> is the intent GUID. Must not be the empty GUID.
/// </param>
/// <param name="VerifiedExternalDispatchStopped">
/// Must be true: the operator attests having verified outside the host, for example on the FTP server, that no
/// further data can arrive for the upload. False is rejected with HTTP 400 and nothing is recorded.
/// </param>
public sealed record StoragePlacementExternalTerminationVerification(StoragePlacementRecoveryContext Context,
    StoragePlacementIntentId IntentId, bool VerifiedExternalDispatchStopped);

public sealed record StoragePlacementRecoveryAuthorization(string Stamp, bool CanReconcile, bool CanVerifyExternalTermination);

public interface IStoragePlacementRecoveryAccess {
    Task<StoragePlacementRecoveryAuthorization> AuthorizeAsync(StoragePlacementRecoveryOperation operation,
        CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, StoragePlacementRecoveryOwnerObservation>> InspectOwnersAsync(
        StoragePlacementRecoveryAuthorization authorization, IReadOnlyCollection<StoragePlacementRecoveryIdentity> identities,
        CancellationToken cancellationToken);
    Task<StoragePlacementContinuationScan> ListContinuationsAsync(StoragePlacementRecoveryAuthorization authorization,
        Guid? projectId, int take, int offset, CancellationToken cancellationToken);
    Task RequireOriginalOwnerForMutationAsync(StoragePlacementRecoveryAuthorization authorization,
        StoragePlacementRecoveryIdentity identity, CancellationToken cancellationToken);
    Task EnsureCurrentAsync(StoragePlacementRecoveryAuthorization authorization, CancellationToken cancellationToken);
}

/// <summary>
/// Reason a storage placement recovery request failed, as a JSON integer: 0 Denied (HTTP 403), 1 StaleContext (HTTP
/// 409), 2 NotFound (HTTP 404), 3 Blocked (HTTP 409), 4 InvalidRequest (HTTP 400) or 5 Unavailable (HTTP 503; the
/// effect of the request is unknown until the placement is read again).
/// </summary>
public enum StoragePlacementRecoveryFailure {
    Denied,
    StaleContext,
    NotFound,
    Blocked,
    InvalidRequest,
    Unavailable
}

public sealed class StoragePlacementRecoveryException(StoragePlacementRecoveryFailure failure)
    : InvalidOperationException("The Storage recovery operation is unavailable or no longer authorized. Refresh its status before continuing.") {
    public StoragePlacementRecoveryFailure Failure { get; } = failure;
}
