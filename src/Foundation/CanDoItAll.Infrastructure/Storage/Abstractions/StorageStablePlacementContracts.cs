using System.Text.Json.Serialization;

namespace CanDoItAll.Infrastructure.Storage;

/// <summary>
/// Identifier of a stable storage placement intent: the identity allocated before one planned write of content to
/// storage, which the host keeps for the placement's whole life. In JSON it is an object with a single <c>value</c>
/// member holding the GUID, for example <c>{ "value": "3f2504e0-4f89-11d3-9a0c-0305e82c3301" }</c>.
/// </summary>
public readonly record struct StoragePlacementIntentId {
    [JsonConstructor]
    public StoragePlacementIntentId(Guid value) {
        if (value == Guid.Empty) {
            throw new ArgumentException("A stable storage placement requires its preallocated intent.", nameof(value));
        }

        Value = value;
    }
    /// <summary>The intent identifier (GUID); never the empty GUID.</summary>
    public Guid Value { get; }
}

/// <summary>
/// State of the write of a stable storage placement, as a JSON integer: 0 Prepared (the target was reserved; the write
/// has not been dispatched), 1 Dispatching (the write was dispatched; its outcome is not confirmed), 2 Completed (the
/// content was verified at the target and the storage receipt recorded), 3 Uncertain (the outcome could not be
/// verified; the target is kept and no second write is made), 4 Conflict (the target holds different content, which is
/// preserved) or 5 Deleted (the placement was deleted and will not be recreated). Only Completed means the content is
/// stored.
/// </summary>
public enum StorageStablePlacementState {
    Prepared,
    Dispatching,
    Completed,
    Uncertain,
    Conflict,
    Deleted
}

public sealed record StorageStablePlacementReceipt(StoragePlacementIntentId IntentId, string RequestFingerprint,
    StorageCatalogPlanningFact Storage, StorageWriteResult WriteResult, string Route, string Location,
    string RelativePath, DateTimeOffset AppliedAtUtc);

public sealed record StorageStablePlacementOutcome(StoragePlacementIntentId IntentId, StorageStablePlacementState State,
    StorageStablePlacementReceipt? Receipt, string Message) {
    [JsonIgnore]
    public Exception? ObservationException { get; init; }
}

public sealed class StorageStablePlacementConflictException(StoragePlacementIntentId intentId)
    : InvalidOperationException("The storage placement intent was prepared for different content or target.") {
    public StoragePlacementIntentId IntentId { get; } = intentId;
}

public sealed class StorageStablePlacementPendingException(StorageStablePlacementOutcome outcome)
    : InvalidOperationException(outcome.Message, outcome.ObservationException) {
    public StorageStablePlacementOutcome Outcome { get; } = outcome;
}

internal interface IStorageStablePlacementDriver {
    bool CanRecoverWithoutWriteAcknowledgement { get; }
    Task<StorageObjectReference> PrepareStableTargetAsync(StorageDriverInput storage, StoragePlacementIntentId intentId,
        StorageWriteRequest request, CancellationToken cancellationToken);
    Task<StorageWriteResult> WriteStableTargetAsync(StorageDriverInput storage, StorageObjectReference target,
        StorageWriteRequest request, CancellationToken cancellationToken);
    Task CompleteStableTargetAsync(StorageDriverInput storage, StorageObjectReference target, CancellationToken cancellationToken);
}

public interface IStoragePlacementReceiptObserver {
    Task ObserveAsync(StorageStablePlacementReceipt receipt, CancellationToken cancellationToken);
}
