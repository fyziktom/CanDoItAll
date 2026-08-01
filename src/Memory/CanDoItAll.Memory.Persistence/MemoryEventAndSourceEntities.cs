using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Persistence;

public sealed class MemoryEventInboxLedgerEntity
{
    public Guid InboxRecordId { get; set; }
    public string ProviderInstanceId { get; set; } = string.Empty;
    public string DedupeKey { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset ForgetAtUtc { get; set; }
    public string RecordJson { get; set; } = "{}";

    public static MemoryEventInboxLedgerEntity FromRecord(MemoryEventInboxRecord record) =>
        new()
        {
            InboxRecordId = record.InboxRecordId.Value,
            ProviderInstanceId = record.ProviderInstanceId.Value,
            DedupeKey = record.DedupeKey.Value,
            Status = (int)record.Status,
            ReceivedAtUtc = record.ReceivedAtUtc,
            UpdatedAtUtc = record.UpdatedAtUtc,
            ExpiresAtUtc = record.Retention.ExpiresAtUtc,
            ForgetAtUtc = record.Retention.ForgetAtUtc,
            RecordJson = MemoryPersistenceJson.Serialize(record)
        };

    public void UpdateRecord(MemoryEventInboxRecord record)
    {
        Status = (int)record.Status;
        UpdatedAtUtc = record.UpdatedAtUtc;
        ExpiresAtUtc = record.Retention.ExpiresAtUtc;
        ForgetAtUtc = record.Retention.ForgetAtUtc;
        RecordJson = MemoryPersistenceJson.Serialize(record);
    }

    public MemoryEventInboxRecord ToRecord() =>
        MemoryPersistenceJson.Deserialize<MemoryEventInboxRecord>(RecordJson);
}

public sealed class MemoryEventOutboxLedgerEntity
{
    public Guid OutboxRecordId { get; set; }
    public string ProviderInstanceId { get; set; } = string.Empty;
    public string DedupeKey { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string PayloadKind { get; set; } = string.Empty;
    public string RecordJson { get; set; } = "{}";

    public static MemoryEventOutboxLedgerEntity FromRecord(MemoryEventOutboxRecord record) =>
        new()
        {
            OutboxRecordId = record.OutboxRecordId.Value,
            ProviderInstanceId = record.ProviderInstanceId.Value,
            DedupeKey = record.DedupeKey.Value,
            Status = (int)record.Status,
            CreatedAtUtc = record.CreatedAtUtc,
            UpdatedAtUtc = record.UpdatedAtUtc,
            PayloadKind = record.PayloadKind,
            RecordJson = MemoryPersistenceJson.Serialize(record)
        };

    public void UpdateRecord(MemoryEventOutboxRecord record)
    {
        Status = (int)record.Status;
        UpdatedAtUtc = record.UpdatedAtUtc;
        PayloadKind = record.PayloadKind;
        RecordJson = MemoryPersistenceJson.Serialize(record);
    }

    public MemoryEventOutboxRecord ToRecord() =>
        MemoryPersistenceJson.Deserialize<MemoryEventOutboxRecord>(RecordJson);
}

public sealed class MemorySourceRequestLedgerEntity
{
    public Guid JobId { get; set; }
    public string ProviderInstanceId { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string RecordJson { get; set; } = "{}";

    public static MemorySourceRequestLedgerEntity FromRecord(MemorySourceIngestionJobRecord record) =>
        new()
        {
            JobId = record.JobId,
            ProviderInstanceId = record.ProviderInstanceId.Value,
            Status = (int)record.Status,
            CreatedAtUtc = record.CreatedAtUtc,
            UpdatedAtUtc = record.UpdatedAtUtc,
            RecordJson = MemoryPersistenceJson.Serialize(record)
        };

    public MemorySourceIngestionJobRecord ToRecord() =>
        MemoryPersistenceJson.Deserialize<MemorySourceIngestionJobRecord>(RecordJson);
}
