using System.Text.Json.Serialization;

namespace CanDoItAll.Infrastructure.Storage;

public readonly record struct StoragePlacementIntentId {
    [JsonConstructor]
    public StoragePlacementIntentId(Guid value) {
        if (value == Guid.Empty) {
            throw new ArgumentException("A stable storage placement requires its preallocated intent.", nameof(value));
        }

        Value = value;
    }
    public Guid Value { get; }
}

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
    Task<StorageObjectReference> PrepareStableTargetAsync(StorageCatalogRecord storage, StoragePlacementIntentId intentId,
        StorageWriteRequest request, CancellationToken cancellationToken);
    Task<StorageWriteResult> WriteStableTargetAsync(StorageCatalogRecord storage, StorageObjectReference target,
        StorageWriteRequest request, CancellationToken cancellationToken);
    Task CompleteStableTargetAsync(StorageCatalogRecord storage, StorageObjectReference target, CancellationToken cancellationToken);
}

public interface IStoragePlacementReceiptObserver {
    Task ObserveAsync(StorageStablePlacementReceipt receipt, CancellationToken cancellationToken);
}
