using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Persistence;

public sealed class MemoryProviderProfileEntity : IHasConcurrencyToken
{
    public string InstanceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int DriverKind { get; set; }
    public bool IsEnabled { get; set; }
    public int HealthState { get; set; }
    public int WorkspaceScope { get; set; }
    public string SelectionTagsJson { get; set; } = "[]";
    public int FallbackBehavior { get; set; }
    public string ManifestJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid ConcurrencyToken { get; set; }

    public static MemoryProviderProfileEntity FromProfile(
        MemoryProviderProfile profile,
        DateTimeOffset nowUtc)
    {
        return new MemoryProviderProfileEntity
        {
            InstanceId = profile.InstanceId.Value,
            DisplayName = profile.DisplayName,
            DriverKind = (int)profile.DriverKind,
            IsEnabled = profile.IsEnabled,
            HealthState = (int)profile.HealthState,
            WorkspaceScope = (int)profile.WorkspaceScope,
            SelectionTagsJson = MemoryPersistenceJson.Serialize(profile.SelectionTags),
            FallbackBehavior = (int)profile.DefaultPolicy.FallbackBehavior,
            ManifestJson = MemoryPersistenceJson.Serialize(profile.Manifest),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void UpdateFrom(
        MemoryProviderProfile profile,
        DateTimeOffset nowUtc)
    {
        DisplayName = profile.DisplayName;
        DriverKind = (int)profile.DriverKind;
        IsEnabled = profile.IsEnabled;
        HealthState = (int)profile.HealthState;
        WorkspaceScope = (int)profile.WorkspaceScope;
        SelectionTagsJson = MemoryPersistenceJson.Serialize(profile.SelectionTags);
        FallbackBehavior = (int)profile.DefaultPolicy.FallbackBehavior;
        ManifestJson = MemoryPersistenceJson.Serialize(profile.Manifest);
        UpdatedAtUtc = nowUtc;
    }

    public MemoryProviderProfile ToProfile()
    {
        return new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse(InstanceId),
            DisplayName,
            (MemoryProviderDriverKind)DriverKind,
            IsEnabled,
            (MemoryProviderHealthState)HealthState,
            (MemoryProviderWorkspaceScope)WorkspaceScope,
            MemoryPersistenceJson.Deserialize<IReadOnlyList<string>>(SelectionTagsJson),
            new MemoryProviderProfilePolicy((MemoryProviderFallbackBehavior)FallbackBehavior),
            MemoryPersistenceJson.Deserialize<MemoryProviderManifest>(ManifestJson));
    }
}

public sealed class MemoryOperationLedgerEntity
{
    public Guid RecordId { get; set; }
    public Guid OperationId { get; set; }
    public string ProviderInstanceId { get; set; } = string.Empty;
    public string CapabilityId { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset ForgetAtUtc { get; set; }
    public string RecordJson { get; set; } = "{}";

    public static MemoryOperationLedgerEntity FromRecord(MemoryOperationRecord record)
    {
        return new MemoryOperationLedgerEntity
        {
            RecordId = record.RecordId.Value,
            OperationId = record.OperationId.Value,
            ProviderInstanceId = record.ProviderInstanceId.Value,
            CapabilityId = record.RequestedCapability.Value,
            Status = (int)record.Status,
            CreatedAtUtc = record.CreatedAtUtc,
            UpdatedAtUtc = record.UpdatedAtUtc,
            CompletedAtUtc = record.CompletedAtUtc,
            ExpiresAtUtc = record.Retention.ExpiresAtUtc,
            ForgetAtUtc = record.Retention.ForgetAtUtc,
            RecordJson = MemoryPersistenceJson.Serialize(record)
        };
    }

    public void UpdateRecord(MemoryOperationRecord record)
    {
        Status = (int)record.Status;
        UpdatedAtUtc = record.UpdatedAtUtc;
        CompletedAtUtc = record.CompletedAtUtc;
        ExpiresAtUtc = record.Retention.ExpiresAtUtc;
        ForgetAtUtc = record.Retention.ForgetAtUtc;
        RecordJson = MemoryPersistenceJson.Serialize(record);
    }

    public MemoryOperationRecord ToRecord() =>
        MemoryPersistenceJson.Deserialize<MemoryOperationRecord>(RecordJson);
}

public sealed class MemoryFeedbackLedgerEntity
{
    public Guid FeedbackRecordId { get; set; }
    public string ProviderInstanceId { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset ForgetAtUtc { get; set; }
    public string RecordJson { get; set; } = "{}";

    public static MemoryFeedbackLedgerEntity FromRecord(MemoryFeedbackRecord record) =>
        new()
        {
            FeedbackRecordId = record.FeedbackRecordId.Value,
            ProviderInstanceId = record.ProviderInstanceId.Value,
            Status = (int)record.Status,
            CreatedAtUtc = record.CreatedAtUtc,
            UpdatedAtUtc = record.UpdatedAtUtc,
            ExpiresAtUtc = record.Retention.ExpiresAtUtc,
            ForgetAtUtc = record.Retention.ForgetAtUtc,
            RecordJson = MemoryPersistenceJson.Serialize(record)
        };

    public void UpdateRecord(MemoryFeedbackRecord record)
    {
        Status = (int)record.Status;
        UpdatedAtUtc = record.UpdatedAtUtc;
        ExpiresAtUtc = record.Retention.ExpiresAtUtc;
        ForgetAtUtc = record.Retention.ForgetAtUtc;
        RecordJson = MemoryPersistenceJson.Serialize(record);
    }

    public MemoryFeedbackRecord ToRecord() =>
        MemoryPersistenceJson.Deserialize<MemoryFeedbackRecord>(RecordJson);
}
