using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryAutomationSettingsRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string SettingsKey { get; set; } = CognitiveMemoryAutomationSettingsKeys.Default;

    public CognitiveMemoryAutomationScheduleMode ScheduleMode { get; set; } = CognitiveMemoryAutomationScheduleMode.ManualOnly;

    public string NightlyLocalTime { get; set; } = "02:00";

    public int IdleMinutes { get; set; } = 30;

    public string ScheduledLocalTimes { get; set; } = string.Empty;

    public bool AutoIngestProjectStructure { get; set; } = true;

    public bool AutoIngestProcessRuntime { get; set; } = true;

    public bool AutoConsolidateAfterIngestion { get; set; } = true;

    public string UpdatedByActorId { get; set; } = "system";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public sealed class CognitiveMemoryExternalSourceIngestionRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public CognitiveMemoryExternalSourceKind SourceKind { get; set; } = CognitiveMemoryExternalSourceKind.UploadedFile;

    public CognitiveMemoryExternalSourceIngestionStatus Status { get; set; } = CognitiveMemoryExternalSourceIngestionStatus.Pending;

    public string Title { get; set; } = string.Empty;

    public string Locator { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long ContentLength { get; set; }

    public int ProgressPercent { get; set; }

    public string StatusMessage { get; set; } = string.Empty;

    public Guid? SourceManifestId { get; set; }

    public Guid? SourceItemId { get; set; }

    public Guid? EvidenceAnchorId { get; set; }

    public string FailureMessage { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}

public static class CognitiveMemoryAutomationSettingsKeys
{
    public const string Default = "default";
}
