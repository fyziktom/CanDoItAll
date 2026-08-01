namespace CanDoItAll.Memory.Application;

public static class MemoryLedgerPersistenceContract
{
    public const string OperationRecords = "MemoryOperationRecords";
    public const string ContextDeliveryRecords = "MemoryContextDeliveryRecords";
    public const string FeedbackRecords = "MemoryFeedbackRecords";
    public const string EventInboxRecords = "MemoryEventInboxRecords";
    public const string EventOutboxRecords = "MemoryEventOutboxRecords";

    public static readonly IReadOnlyList<string> RequiredRecordSets =
    [
        OperationRecords,
        ContextDeliveryRecords,
        FeedbackRecords,
        EventInboxRecords,
        EventOutboxRecords
    ];
}
