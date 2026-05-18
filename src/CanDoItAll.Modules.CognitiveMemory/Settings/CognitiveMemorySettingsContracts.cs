namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryAutomationScheduleMode
{
    ManualOnly = 0,
    Nightly = 1,
    IdleTimeout = 2,
    ScheduledMoments = 3
}

public enum CognitiveMemoryExternalSourceKind
{
    UploadedFile = 0,
    WebsiteLink = 1
}

public enum CognitiveMemoryExternalSourceIngestionStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3
}

public enum CognitiveMemoryModelAccessMode
{
    AnyEnabledProvider = 0,
    LocalProvidersOnly = 1,
    SelectedProvidersOnly = 2,
    Disabled = 3
}

public sealed record CognitiveMemoryAutomationSettings(
    CognitiveMemoryAutomationScheduleMode ScheduleMode,
    string NightlyLocalTime,
    int IdleMinutes,
    IReadOnlyList<string> ScheduledLocalTimes,
    bool AutoIngestProjectStructure,
    bool AutoIngestProcessRuntime,
    bool AutoConsolidateAfterIngestion,
    CognitiveMemoryModelAccessMode ModelAccessMode,
    Guid? DefaultProviderProfileId,
    Guid? DefaultAgentId,
    IReadOnlyList<Guid> AllowedProviderProfileIds,
    string UpdatedByActorId,
    DateTimeOffset UpdatedAtUtc)
{
    public static CognitiveMemoryAutomationSettings Defaults(DateTimeOffset nowUtc) => new(
        CognitiveMemoryAutomationScheduleMode.ManualOnly,
        "02:00",
        30,
        [],
        AutoIngestProjectStructure: true,
        AutoIngestProcessRuntime: true,
        AutoConsolidateAfterIngestion: true,
        CognitiveMemoryModelAccessMode.AnyEnabledProvider,
        DefaultProviderProfileId: null,
        DefaultAgentId: null,
        AllowedProviderProfileIds: [],
        UpdatedByActorId: "system",
        UpdatedAtUtc: nowUtc);
}

public sealed record CognitiveMemoryAutomationSettingsUpdate(
    CognitiveMemoryAutomationScheduleMode ScheduleMode,
    string NightlyLocalTime,
    int IdleMinutes,
    IReadOnlyList<string> ScheduledLocalTimes,
    bool AutoIngestProjectStructure,
    bool AutoIngestProcessRuntime,
    bool AutoConsolidateAfterIngestion,
    CognitiveMemoryModelAccessMode ModelAccessMode,
    Guid? DefaultProviderProfileId,
    Guid? DefaultAgentId,
    IReadOnlyList<Guid> AllowedProviderProfileIds,
    string UpdatedByActorId);

public sealed record CognitiveMemoryExternalSourceIngestRequest(
    CognitiveMemoryExternalSourceKind SourceKind,
    Guid? ProjectId,
    string Title,
    string Locator,
    string ContentText,
    string ContentType,
    long ContentLength,
    string ActorId,
    string? IdempotencyKey = null);

public sealed record CognitiveMemoryExternalSourceIngestResult(
    Guid OperationId,
    CognitiveMemoryExternalSourceKind SourceKind,
    CognitiveMemoryExternalSourceIngestionStatus Status,
    int ProgressPercent,
    string StatusMessage,
    Guid? ProjectId,
    Guid? SourceManifestId,
    Guid? SourceItemId,
    Guid? EvidenceAnchorId,
    string? FailureMessage);

public interface ICognitiveMemoryAutomationSettingsService
{
    ValueTask<CognitiveMemoryAutomationSettings> GetAsync(CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryAutomationSettings> SaveAsync(
        CognitiveMemoryAutomationSettingsUpdate update,
        CancellationToken cancellationToken = default);
}

public interface ICognitiveMemoryExternalSourceIngestionService
{
    ValueTask<CognitiveMemoryExternalSourceIngestResult> IngestFileAsync(
        Guid? projectId,
        string fileName,
        string contentType,
        Stream content,
        long contentLength,
        string actorId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryExternalSourceIngestResult> IngestWebsiteAsync(
        Guid? projectId,
        Uri uri,
        string actorId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryExternalSourceIngestResult> IngestAsync(
        CognitiveMemoryExternalSourceIngestRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<CognitiveMemoryExternalSourceIngestResult?> GetAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);
}
