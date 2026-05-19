using System.Text.Json.Serialization;

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

public enum CognitiveMemoryModelExecutionRole
{
    SourceIngestion = 0,
    Consolidation = 1,
    EpistemicDrive = 2,
    Probe = 3,
    ProfessorReview = 4
}

public readonly record struct CognitiveMemoryExecutionModelId
{
    [JsonConstructor]
    public CognitiveMemoryExecutionModelId(string value)
    {
        Value = CognitiveMemoryGuard.EnsureText(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
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
    public IReadOnlyList<CognitiveMemoryModelExecutionProfile> ModelExecutionProfiles { get; init; } =
        CognitiveMemoryModelExecutionProfileDefaults.OpenAiProfiles;

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
        UpdatedAtUtc: nowUtc)
    {
        ModelExecutionProfiles = CognitiveMemoryModelExecutionProfileDefaults.OpenAiProfiles
    };
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
    string UpdatedByActorId)
{
    public IReadOnlyList<CognitiveMemoryModelExecutionProfile> ModelExecutionProfiles { get; init; } =
        CognitiveMemoryModelExecutionProfileDefaults.OpenAiProfiles;
}

public sealed record CognitiveMemoryModelExecutionProfile(
    CognitiveMemoryModelExecutionRole Role,
    Guid? ProviderProfileId,
    CognitiveMemoryExecutionModelId ModelId,
    int MaxOutputTokens,
    int TimeoutSeconds,
    bool LocalOnly,
    string Notes = "");

public static class CognitiveMemoryModelExecutionProfileDefaults
{
    public const string OpenAiDefaultModelId = "gpt-5-mini";
    public const string OllamaValidationModelId = "gptoss20b64k";
    public const int DefaultOpenAiMaxOutputTokens = 4096;
    public const int DefaultOllamaMaxOutputTokens = 8192;
    public const int DefaultTimeoutSeconds = 120;

    public static readonly IReadOnlyList<CognitiveMemoryModelExecutionProfile> OpenAiProfiles =
    [
        CreateOpenAi(CognitiveMemoryModelExecutionRole.SourceIngestion),
        CreateOpenAi(CognitiveMemoryModelExecutionRole.Consolidation),
        CreateOpenAi(CognitiveMemoryModelExecutionRole.EpistemicDrive),
        CreateOpenAi(CognitiveMemoryModelExecutionRole.Probe),
        CreateOpenAi(CognitiveMemoryModelExecutionRole.ProfessorReview)
    ];

    public static CognitiveMemoryModelExecutionProfile CreateOpenAi(CognitiveMemoryModelExecutionRole role)
        => new(
            role,
            ProviderProfileId: null,
            new CognitiveMemoryExecutionModelId(OpenAiDefaultModelId),
            DefaultOpenAiMaxOutputTokens,
            DefaultTimeoutSeconds,
            LocalOnly: false,
            Notes: "Default OpenAI cognitive-memory execution profile.");

    public static CognitiveMemoryModelExecutionProfile CreateOllama(CognitiveMemoryModelExecutionRole role)
        => new(
            role,
            ProviderProfileId: null,
            new CognitiveMemoryExecutionModelId(OllamaValidationModelId),
            DefaultOllamaMaxOutputTokens,
            DefaultTimeoutSeconds,
            LocalOnly: true,
            Notes: "Local Ollama validation profile.");
}

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
