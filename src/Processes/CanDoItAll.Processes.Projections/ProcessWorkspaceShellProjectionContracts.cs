using System.ComponentModel;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Processes.Projections;

public enum ProcessProjectionComponentState
{
    NotRequested,
    Absent,
    Present
}

public enum ProcessProjectionComponentSource
{
    None,
    RuntimeProjectionStore,
    ReusedRuntimeProjection,
    RunRecordStore,
    RuntimeState,
    UsageTelemetry,
    DefinitionCatalog,
    Request,
    ShellProjection
}

public enum ProcessProjectionComponentAbsenceReason
{
    None,
    LoadOptionDisabled,
    NoSelection,
    NoData,
    SourceUnavailable,
    NotApplicable,
    SupersededByDurableRecord,
    OutsideQueryScope
}

public enum ProcessWorkspaceProvenanceComponent
{
    Selection,
    ShellRefresh,
    DefinitionCatalog,
    LiveRunSummary,
    LiveRuns,
    SelectedRunDetail,
    SelectedRunRecord,
    HistoryPage,
    MetricHistory,
    ActiveAgents,
    UsageTelemetry,
    DerivedProjection
}

public readonly record struct ProcessProjectionContentFingerprint
{
    private const string Prefix = "sha256:";
    private const int Sha256HexLength = 64;

    public ProcessProjectionContentFingerprint(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (!normalized.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ||
            normalized.Length != Prefix.Length + Sha256HexLength ||
            normalized.AsSpan(Prefix.Length).ContainsAnyExcept(
                "0123456789abcdefABCDEF".AsSpan()))
        {
            throw new ArgumentException(
                "Process projection content fingerprint must be a SHA-256 value prefixed with 'sha256:'.",
                nameof(value));
        }

        Value = normalized.ToLowerInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record ProcessRunRecordProjectionRevision
{
    public ProcessRunRecordProjectionRevision(
        long sourceGlobalSequence,
        long sourceRootSequence,
        DateTimeOffset updatedAtUtc)
    {
        if (sourceGlobalSequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceGlobalSequence),
                sourceGlobalSequence,
                "Process run record source global sequence cannot be negative.");
        }

        if (sourceRootSequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceRootSequence),
                sourceRootSequence,
                "Process run record source root sequence cannot be negative.");
        }

        if (updatedAtUtc == default || updatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Process run record revision timestamp must be a non-default UTC value.",
                nameof(updatedAtUtc));
        }

        SourceGlobalSequence = sourceGlobalSequence;
        SourceRootSequence = sourceRootSequence;
        UpdatedAtUtc = updatedAtUtc;
    }

    public long SourceGlobalSequence { get; }

    public long SourceRootSequence { get; }

    public DateTimeOffset UpdatedAtUtc { get; }
}

public sealed record ProcessProjectionComponentProvenance
{
    private ProcessProjectionComponentProvenance(
        ProcessProjectionComponentState state,
        ProcessProjectionComponentSource source,
        ProcessProjectionComponentAbsenceReason absenceReason,
        ProcessProjectionContentFingerprint? contentFingerprint,
        ProcessProjectionFreshness? freshness,
        ProcessRunRecordProjectionRevision? runRecordRevision)
    {
        State = state;
        Source = source;
        AbsenceReason = absenceReason;
        ContentFingerprint = contentFingerprint;
        Freshness = freshness;
        RunRecordRevision = runRecordRevision;
    }

    public ProcessProjectionComponentState State { get; }

    public ProcessProjectionComponentSource Source { get; }

    public ProcessProjectionComponentAbsenceReason AbsenceReason { get; }

    public ProcessProjectionContentFingerprint? ContentFingerprint { get; }

    public ProcessProjectionFreshness? Freshness { get; }

    public ProcessRunRecordProjectionRevision? RunRecordRevision { get; }

    public static ProcessProjectionComponentProvenance Present(
        ProcessProjectionComponentSource source,
        ProcessProjectionContentFingerprint contentFingerprint,
        ProcessProjectionFreshness? freshness = null,
        ProcessRunRecordProjectionRevision? runRecordRevision = null)
    {
        if (!Enum.IsDefined(source) || source == ProcessProjectionComponentSource.None)
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "Present projection provenance requires a source.");
        }

        if (string.IsNullOrWhiteSpace(contentFingerprint.Value))
        {
            throw new ArgumentException("Present projection provenance requires a content fingerprint.", nameof(contentFingerprint));
        }

        if (source == ProcessProjectionComponentSource.RunRecordStore && runRecordRevision is null)
        {
            throw new ArgumentException(
                "Run-record projection provenance requires a durable record revision.",
                nameof(runRecordRevision));
        }

        if (source != ProcessProjectionComponentSource.RunRecordStore && runRecordRevision is not null)
        {
            throw new ArgumentException(
                "Only run-record projection provenance can carry a durable record revision.",
                nameof(runRecordRevision));
        }

        if (freshness is not null && runRecordRevision is not null)
        {
            throw new ArgumentException(
                "Projection freshness and durable record revision are mutually exclusive.",
                nameof(freshness));
        }

        return new ProcessProjectionComponentProvenance(
            ProcessProjectionComponentState.Present,
            source,
            ProcessProjectionComponentAbsenceReason.None,
            contentFingerprint,
            freshness,
            runRecordRevision);
    }

    public static ProcessProjectionComponentProvenance Absent(
        ProcessProjectionComponentSource source,
        ProcessProjectionComponentAbsenceReason reason,
        ProcessProjectionContentFingerprint? contentFingerprint = null)
    {
        if (!Enum.IsDefined(source) || source == ProcessProjectionComponentSource.None)
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "Absent projection provenance requires a source.");
        }

        if (reason is not (
            ProcessProjectionComponentAbsenceReason.NoSelection or
            ProcessProjectionComponentAbsenceReason.NoData or
            ProcessProjectionComponentAbsenceReason.SourceUnavailable))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Absent projection provenance requires an absence reason.");
        }

        if (contentFingerprint is { } fingerprint &&
            string.IsNullOrWhiteSpace(fingerprint.Value))
        {
            throw new ArgumentException(
                "Absent projection provenance carries an invalid content fingerprint.",
                nameof(contentFingerprint));
        }

        if (contentFingerprint is not null &&
            reason != ProcessProjectionComponentAbsenceReason.NoData)
        {
            throw new ArgumentException(
                "Only a no-data result can carry an empty-content fingerprint.",
                nameof(contentFingerprint));
        }

        return new ProcessProjectionComponentProvenance(
            ProcessProjectionComponentState.Absent,
            source,
            reason,
            contentFingerprint,
            freshness: null,
            runRecordRevision: null);
    }

    public static ProcessProjectionComponentProvenance NotRequested(
        ProcessProjectionComponentAbsenceReason reason)
    {
        if (reason is not (
            ProcessProjectionComponentAbsenceReason.LoadOptionDisabled or
            ProcessProjectionComponentAbsenceReason.NotApplicable or
            ProcessProjectionComponentAbsenceReason.SupersededByDurableRecord or
            ProcessProjectionComponentAbsenceReason.OutsideQueryScope))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Not-requested projection provenance requires a request reason.");
        }

        return new ProcessProjectionComponentProvenance(
            ProcessProjectionComponentState.NotRequested,
            ProcessProjectionComponentSource.None,
            reason,
            contentFingerprint: null,
            freshness: null,
            runRecordRevision: null);
    }
}

public sealed record ProcessWorkspaceProvenanceVector
{
    private static readonly ProcessProjectionComponentProvenance OutsideQueryScope =
        ProcessProjectionComponentProvenance.NotRequested(
            ProcessProjectionComponentAbsenceReason.OutsideQueryScope);
    private ProcessProjectionComponentProvenance selection = null!;
    private ProcessProjectionComponentProvenance shellRefresh = null!;
    private ProcessProjectionComponentProvenance definitionCatalog = null!;
    private ProcessProjectionComponentProvenance liveRunSummary = null!;
    private ProcessProjectionComponentProvenance liveRuns = null!;
    private ProcessProjectionComponentProvenance selectedRunDetail = null!;
    private ProcessProjectionComponentProvenance selectedRunRecord = null!;
    private ProcessProjectionComponentProvenance historyPage = null!;
    private ProcessProjectionComponentProvenance metricHistory = null!;
    private ProcessProjectionComponentProvenance activeAgents = null!;
    private ProcessProjectionComponentProvenance usageTelemetry = null!;
    private ProcessProjectionComponentProvenance derivedProjection = null!;

    public ProcessWorkspaceProvenanceVector(
        ProcessProjectionComponentProvenance selection,
        ProcessProjectionComponentProvenance shellRefresh,
        ProcessProjectionComponentProvenance definitionCatalog,
        ProcessProjectionComponentProvenance liveRunSummary,
        ProcessProjectionComponentProvenance liveRuns,
        ProcessProjectionComponentProvenance selectedRunDetail,
        ProcessProjectionComponentProvenance selectedRunRecord,
        ProcessProjectionComponentProvenance historyPage,
        ProcessProjectionComponentProvenance metricHistory,
        ProcessProjectionComponentProvenance activeAgents,
        ProcessProjectionComponentProvenance usageTelemetry,
        ProcessProjectionComponentProvenance derivedProjection)
    {
        Selection = selection;
        ShellRefresh = shellRefresh;
        DefinitionCatalog = definitionCatalog;
        LiveRunSummary = liveRunSummary;
        LiveRuns = liveRuns;
        SelectedRunDetail = selectedRunDetail;
        SelectedRunRecord = selectedRunRecord;
        HistoryPage = historyPage;
        MetricHistory = metricHistory;
        ActiveAgents = activeAgents;
        UsageTelemetry = usageTelemetry;
        DerivedProjection = derivedProjection;
    }

    public ProcessProjectionComponentProvenance Selection
    {
        get => selection;
        init => selection = value ?? throw new ArgumentNullException(nameof(Selection));
    }

    public ProcessProjectionComponentProvenance ShellRefresh
    {
        get => shellRefresh;
        init => shellRefresh = value ?? throw new ArgumentNullException(nameof(ShellRefresh));
    }

    public ProcessProjectionComponentProvenance DefinitionCatalog
    {
        get => definitionCatalog;
        init => definitionCatalog = value ?? throw new ArgumentNullException(nameof(DefinitionCatalog));
    }

    public ProcessProjectionComponentProvenance LiveRunSummary
    {
        get => liveRunSummary;
        init => liveRunSummary = value ?? throw new ArgumentNullException(nameof(LiveRunSummary));
    }

    public ProcessProjectionComponentProvenance LiveRuns
    {
        get => liveRuns;
        init => liveRuns = value ?? throw new ArgumentNullException(nameof(LiveRuns));
    }

    public ProcessProjectionComponentProvenance SelectedRunDetail
    {
        get => selectedRunDetail;
        init => selectedRunDetail = value ?? throw new ArgumentNullException(nameof(SelectedRunDetail));
    }

    public ProcessProjectionComponentProvenance SelectedRunRecord
    {
        get => selectedRunRecord;
        init => selectedRunRecord = value ?? throw new ArgumentNullException(nameof(SelectedRunRecord));
    }

    public ProcessProjectionComponentProvenance HistoryPage
    {
        get => historyPage;
        init => historyPage = value ?? throw new ArgumentNullException(nameof(HistoryPage));
    }

    public ProcessProjectionComponentProvenance MetricHistory
    {
        get => metricHistory;
        init => metricHistory = value ?? throw new ArgumentNullException(nameof(MetricHistory));
    }

    public ProcessProjectionComponentProvenance ActiveAgents
    {
        get => activeAgents;
        init => activeAgents = value ?? throw new ArgumentNullException(nameof(ActiveAgents));
    }

    public ProcessProjectionComponentProvenance UsageTelemetry
    {
        get => usageTelemetry;
        init => usageTelemetry = value ?? throw new ArgumentNullException(nameof(UsageTelemetry));
    }

    public ProcessProjectionComponentProvenance DerivedProjection
    {
        get => derivedProjection;
        init => derivedProjection = value ?? throw new ArgumentNullException(nameof(DerivedProjection));
    }

    public static ProcessWorkspaceProvenanceVector Empty { get; } = new(
        OutsideQueryScope,
        OutsideQueryScope,
        OutsideQueryScope,
        OutsideQueryScope,
        OutsideQueryScope,
        OutsideQueryScope,
        OutsideQueryScope,
        OutsideQueryScope,
        OutsideQueryScope,
        OutsideQueryScope,
        OutsideQueryScope,
        OutsideQueryScope);

    public static ProcessWorkspaceProvenanceVector RuntimeUnavailable { get; } = Empty with
    {
        LiveRuns = ProcessProjectionComponentProvenance.Absent(
            ProcessProjectionComponentSource.RuntimeProjectionStore,
            ProcessProjectionComponentAbsenceReason.SourceUnavailable),
        SelectedRunDetail = ProcessProjectionComponentProvenance.Absent(
            ProcessProjectionComponentSource.RuntimeProjectionStore,
            ProcessProjectionComponentAbsenceReason.SourceUnavailable),
        SelectedRunRecord = ProcessProjectionComponentProvenance.Absent(
            ProcessProjectionComponentSource.RunRecordStore,
            ProcessProjectionComponentAbsenceReason.SourceUnavailable),
        HistoryPage = ProcessProjectionComponentProvenance.Absent(
            ProcessProjectionComponentSource.RuntimeProjectionStore,
            ProcessProjectionComponentAbsenceReason.SourceUnavailable),
        MetricHistory = ProcessProjectionComponentProvenance.Absent(
            ProcessProjectionComponentSource.RuntimeProjectionStore,
            ProcessProjectionComponentAbsenceReason.SourceUnavailable),
        ActiveAgents = ProcessProjectionComponentProvenance.Absent(
            ProcessProjectionComponentSource.RuntimeState,
            ProcessProjectionComponentAbsenceReason.SourceUnavailable)
    };

    public ProcessProjectionComponentProvenance GetComponent(
        ProcessWorkspaceProvenanceComponent component)
        => component switch
        {
            ProcessWorkspaceProvenanceComponent.Selection => Selection,
            ProcessWorkspaceProvenanceComponent.ShellRefresh => ShellRefresh,
            ProcessWorkspaceProvenanceComponent.DefinitionCatalog => DefinitionCatalog,
            ProcessWorkspaceProvenanceComponent.LiveRunSummary => LiveRunSummary,
            ProcessWorkspaceProvenanceComponent.LiveRuns => LiveRuns,
            ProcessWorkspaceProvenanceComponent.SelectedRunDetail => SelectedRunDetail,
            ProcessWorkspaceProvenanceComponent.SelectedRunRecord => SelectedRunRecord,
            ProcessWorkspaceProvenanceComponent.HistoryPage => HistoryPage,
            ProcessWorkspaceProvenanceComponent.MetricHistory => MetricHistory,
            ProcessWorkspaceProvenanceComponent.ActiveAgents => ActiveAgents,
            ProcessWorkspaceProvenanceComponent.UsageTelemetry => UsageTelemetry,
            ProcessWorkspaceProvenanceComponent.DerivedProjection => DerivedProjection,
            _ => throw new ArgumentOutOfRangeException(
                nameof(component),
                component,
                "The process provenance component is undefined.")
        };
}

public static class ProcessProjectionContentFingerprintFactory
{
    private const string ComponentPropertyName = "component";
    private const string ContentPropertyName = "content";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static ProcessProjectionContentFingerprint Create<T>(
        ProcessWorkspaceProvenanceComponent component,
        T content)
    {
        if (!Enum.IsDefined(component))
        {
            throw new ArgumentOutOfRangeException(
                nameof(component),
                component,
                "The process provenance component is undefined.");
        }

        ArgumentNullException.ThrowIfNull(content);
        using var document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(content, SerializerOptions));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber(ComponentPropertyName, (int)component);
            writer.WritePropertyName(ContentPropertyName);
            WriteCanonical(writer, document.RootElement);
            writer.WriteEndObject();
        }

        return new ProcessProjectionContentFingerprint(
            "sha256:" + Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element
                             .EnumerateObject()
                             .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }

                writer.WriteEndObject();
                return;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }

                writer.WriteEndArray();
                return;
            default:
                element.WriteTo(writer);
                return;
        }
    }
}

public enum ProcessWorkspaceContextField
{
    SurfaceSource,
    SurfaceView,
    SurfaceRoute,
    SurfaceScope,
    SurfaceProjectId,
    ContextAccessState,
    WorkspaceRunView,
    RuntimeHistoryWindow,
    RuntimeStatusFilter,
    FocusedRuntimeEventDialog,
    FocusedRunDetailDialog,
    FocusedRunFilesDialog,
    FocusedAgentDialog,
    ProjectionStatus,
    DefinitionCount,
    LoadedRunCount,
    ActiveRunCount,
    AttentionRunCount,
    FailedRunCount,
    DefinitionIdentity,
    DefinitionName,
    DefinitionStatus,
    DefinitionScope,
    DefinitionCriticality,
    DefinitionOperatingMode,
    DefinitionCompatibilityIssues,
    RunIdentity,
    RunDisplayName,
    RunStatus,
    RunProjectId,
    RunProjectName,
    RunProcessName,
    RunIsSubprocess,
    RunProgress,
    CurrentStep,
    CurrentStepStatus,
    CurrentStepRole,
    EventIdentity,
    EventType,
    EventSensitivity,
    AgentIdentity,
    AgentDisplayName,
    AgentStatus,
    AgentStep,
    AgentRole,
    ManagerScope,
    ManagerSelectedRun,
    ManagerSelectedRunStatus,
    ManagerAttentionSummary,
    ManagerHistoryWindow,
    ManagerLoadedRuns,
    ManagerLoadedRunLatestEvent,
    ManagerUsageScope,
    ManagerUsageCost,
    ManagerUsageInputTokens,
    ManagerUsageCachedInputTokens,
    ManagerUsageOutputTokens,
    ManagerUsageTotalTokens
}

public static class ProcessWorkspaceContextFieldProvenance
{
    public static ProcessWorkspaceProvenanceComponent GetComponent(ProcessWorkspaceContextField field)
        => field switch
        {
            ProcessWorkspaceContextField.SurfaceSource or
            ProcessWorkspaceContextField.SurfaceView or
            ProcessWorkspaceContextField.SurfaceRoute or
            ProcessWorkspaceContextField.SurfaceScope or
            ProcessWorkspaceContextField.SurfaceProjectId or
            ProcessWorkspaceContextField.ContextAccessState or
            ProcessWorkspaceContextField.WorkspaceRunView or
            ProcessWorkspaceContextField.RuntimeStatusFilter or
            ProcessWorkspaceContextField.FocusedRunFilesDialog or
            ProcessWorkspaceContextField.ManagerScope or
            ProcessWorkspaceContextField.ManagerSelectedRun =>
                ProcessWorkspaceProvenanceComponent.Selection,

            ProcessWorkspaceContextField.ProjectionStatus =>
                ProcessWorkspaceProvenanceComponent.ShellRefresh,

            ProcessWorkspaceContextField.DefinitionCount or
            ProcessWorkspaceContextField.DefinitionIdentity or
            ProcessWorkspaceContextField.DefinitionName or
            ProcessWorkspaceContextField.DefinitionStatus or
            ProcessWorkspaceContextField.DefinitionScope or
            ProcessWorkspaceContextField.DefinitionCriticality or
            ProcessWorkspaceContextField.DefinitionOperatingMode or
            ProcessWorkspaceContextField.DefinitionCompatibilityIssues =>
                ProcessWorkspaceProvenanceComponent.DefinitionCatalog,

            ProcessWorkspaceContextField.ActiveRunCount or
            ProcessWorkspaceContextField.AttentionRunCount or
            ProcessWorkspaceContextField.FailedRunCount =>
                ProcessWorkspaceProvenanceComponent.LiveRunSummary,

            ProcessWorkspaceContextField.LoadedRunCount or
            ProcessWorkspaceContextField.RunIdentity or
            ProcessWorkspaceContextField.RunDisplayName or
            ProcessWorkspaceContextField.RunStatus or
            ProcessWorkspaceContextField.RunProjectId or
            ProcessWorkspaceContextField.RunProjectName or
            ProcessWorkspaceContextField.RunProcessName or
            ProcessWorkspaceContextField.RunIsSubprocess or
            ProcessWorkspaceContextField.RunProgress or
            ProcessWorkspaceContextField.CurrentStep or
            ProcessWorkspaceContextField.CurrentStepStatus or
            ProcessWorkspaceContextField.CurrentStepRole or
            ProcessWorkspaceContextField.ManagerLoadedRuns or
            ProcessWorkspaceContextField.ManagerLoadedRunLatestEvent =>
                ProcessWorkspaceProvenanceComponent.LiveRuns,

            ProcessWorkspaceContextField.FocusedRunDetailDialog or
            ProcessWorkspaceContextField.ManagerSelectedRunStatus =>
                ProcessWorkspaceProvenanceComponent.SelectedRunDetail,

            ProcessWorkspaceContextField.RuntimeHistoryWindow or
            ProcessWorkspaceContextField.FocusedRuntimeEventDialog or
            ProcessWorkspaceContextField.EventIdentity or
            ProcessWorkspaceContextField.EventType or
            ProcessWorkspaceContextField.EventSensitivity or
            ProcessWorkspaceContextField.ManagerHistoryWindow =>
                ProcessWorkspaceProvenanceComponent.HistoryPage,

            ProcessWorkspaceContextField.FocusedAgentDialog or
            ProcessWorkspaceContextField.AgentIdentity or
            ProcessWorkspaceContextField.AgentDisplayName or
            ProcessWorkspaceContextField.AgentStatus or
            ProcessWorkspaceContextField.AgentStep or
            ProcessWorkspaceContextField.AgentRole =>
                ProcessWorkspaceProvenanceComponent.ActiveAgents,

            ProcessWorkspaceContextField.ManagerAttentionSummary or
            ProcessWorkspaceContextField.ManagerUsageScope or
            ProcessWorkspaceContextField.ManagerUsageCost or
            ProcessWorkspaceContextField.ManagerUsageInputTokens or
            ProcessWorkspaceContextField.ManagerUsageCachedInputTokens or
            ProcessWorkspaceContextField.ManagerUsageOutputTokens or
            ProcessWorkspaceContextField.ManagerUsageTotalTokens =>
                ProcessWorkspaceProvenanceComponent.DerivedProjection,

            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "The process context field is undefined.")
        };

    public static ProcessProjectionComponentProvenance GetProvenance(
        ProcessWorkspaceProvenanceVector vector,
        ProcessWorkspaceContextField field)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return vector.GetComponent(GetComponent(field));
    }
}

public enum ProcessWorkspaceScopeKind
{
    Global,
    Project
}

public enum ProcessWorkspaceTabKey
{
    Definitions,
    LaunchPlans,
    LiveRuns,
    History
}

public enum ProcessWorkspaceCommandKind
{
    RefreshProjections,
    OpenAgentContext,
    CreateDefinition,
    FeedDefaults,
    LaunchRun,
    OpenLiveDashboard
}

public enum ProcessWorkspaceProjectionStatus
{
    Ready,
    RefreshRequested,
    ProjectionStoreUnavailable
}

public enum ProcessRuntimeHistoryWindow
{
    LiveHour,
    OneDay,
    SevenDays,
    ThirtyDays
}

public enum ProcessWorkspaceAgentEntryKind
{
    WorkspaceContext,
    ProjectContext,
    RunContext,
    LaunchPlanContext
}

[Description("Classification of catalog scope kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionCatalogScopeKind
{
    All,
    Global,
    Project
}

[Description("Classification of catalog item status. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionCatalogItemStatus
{
    TemplateDefault,
    Draft,
    Published,
    RequiresReview
}

public enum ProcessDefinitionCatalogCommandKind
{
    FeedDefaults
}

public enum ProcessDefinitionCatalogCommandStatus
{
    Accepted,
    NoDefinitionsAvailable
}

[Description("Classification of authoring status. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionAuthoringStatus
{
    TemplateDefault,
    Draft,
    Published,
    Archived
}

[Description("Classification of criticality level. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionCriticalityLevel
{
    Unspecified,
    Low,
    Standard,
    High,
    MissionCritical
}

[Description("Classification of autonomy level. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionAutonomyLevel
{
    Unspecified,
    Manual,
    Assisted,
    Guarded,
    Delegated
}

[Description("Classification of operating mode kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionOperatingModeKind
{
    Unspecified,
    Manual,
    AssistedExecution,
    GovernedLive
}

[Description("Classification of editor command kind. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionEditorCommandKind
{
    SaveDraft,
    Publish,
    Archive,
    Delete
}

[Description("Classification of editor command status. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionEditorCommandStatus
{
    Accepted,
    Rejected
}

[Description("Classification of editor lint severity. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionEditorLintSeverity
{
    Info,
    Warning,
    Error
}

[Description("Classification of editor lint section. Numeric JSON values are listed by the OpenAPI schema.")]
public enum ProcessDefinitionEditorLintSection
{
    Identity,
    Governance,
    Contracts,
    Simulation
}

[Description("Opaque catalog item key represented by its value member. Preserve the value; do not derive it from display text.")]
public readonly record struct ProcessDefinitionCatalogItemKey
{
    public ProcessDefinitionCatalogItemKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Definition catalog item key is required.", nameof(value));
        }

        Value = value.Trim();
    }

    [Description("Opaque identity or authoring-version text carried by this wrapper; preserve exactly.")]
    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessDefinitionCatalogRefreshToken
{
    public ProcessDefinitionCatalogRefreshToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Definition catalog refresh token is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

[Description("Opaque version of the editor projection. This is an authoring concurrency token, not an authentication credential.")]
public readonly record struct ProcessDefinitionEditorVersionToken
{
    public ProcessDefinitionEditorVersionToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Definition editor version token is required.", nameof(value));
        }

        Value = value.Trim();
    }

    [Description("Opaque identity or authoring-version text carried by this wrapper; preserve exactly.")]
    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record ProcessWorkspaceShellScope(
    ProcessWorkspaceScopeKind Kind,
    Guid? ProjectId)
{
    public static ProcessWorkspaceShellScope Global { get; } = new(ProcessWorkspaceScopeKind.Global, null);

    public static ProcessWorkspaceShellScope ForProject(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project-scoped process workspace requires a non-empty project id.", nameof(projectId));
        }

        return new ProcessWorkspaceShellScope(ProcessWorkspaceScopeKind.Project, projectId);
    }
}

public sealed record ProcessWorkspaceSelectionProjection(
    Guid? ProcessId,
    Guid? RunId,
    Guid? LaunchPlanId);

public sealed record ProcessDefinitionCatalogQueryProjection(
    string? SearchText,
    ProcessDefinitionCatalogItemKey? SelectedDefinitionKey,
    ProcessDefinitionCatalogScopeKind ScopeFilter,
    int Take);

public sealed record ProcessWorkspaceShellRequest(
    ProcessWorkspaceShellScope Scope,
    ProcessWorkspaceSelectionProjection Selection,
    ProcessDefinitionCatalogQueryProjection DefinitionCatalogQuery,
    ProcessTemplateCatalogQueryProjection TemplateCatalogQuery,
    bool ForceRefresh,
    ProcessRuntimeWorkspaceQueryProjection? RuntimeQuery = null,
    ProcessDefinitionWorkspaceLoadOptions? DefinitionLoadOptions = null) {
    public ProcessProjectionProjectBinding? ProjectBinding { get; init; }
}

public sealed record ProcessDefinitionWorkspaceLoadOptions
{
    public static ProcessDefinitionWorkspaceLoadOptions Full { get; } = new();

    public static ProcessDefinitionWorkspaceLoadOptions CatalogOnly { get; } = new()
    {
        IncludeSelectedEditor = false,
        IncludeRoleEditor = false,
        IncludeStepEditor = false,
        IncludeCanvas = false,
        IncludeTemplateCatalog = false
    };

    public static ProcessDefinitionWorkspaceLoadOptions Basics { get; } = new()
    {
        IncludeRoleEditor = false,
        IncludeStepEditor = false,
        IncludeCanvas = false,
        IncludeTemplateCatalog = false
    };

    public static ProcessDefinitionWorkspaceLoadOptions Roles { get; } = new()
    {
        IncludeStepEditor = false,
        IncludeCanvas = false,
        IncludeTemplateCatalog = false
    };

    public static ProcessDefinitionWorkspaceLoadOptions Steps { get; } = new()
    {
        IncludeRoleEditor = false,
        IncludeTemplateCatalog = false
    };

    public static ProcessDefinitionWorkspaceLoadOptions Exchange { get; } = new()
    {
        IncludeRoleEditor = false,
        IncludeStepEditor = false,
        IncludeCanvas = false
    };

    public bool IncludeSelectedEditor { get; init; } = true;

    public bool IncludeRoleEditor { get; init; } = true;

    public bool IncludeStepEditor { get; init; } = true;

    public bool IncludeCanvas { get; init; } = true;

    public bool IncludeTemplateCatalog { get; init; } = true;
}

public sealed record ProcessWorkspaceTabProjection(
    ProcessWorkspaceTabKey Key,
    string Text,
    string Icon,
    string Description,
    string? CountText,
    bool IsEnabled);

public sealed record ProcessWorkspaceCommandProjection(
    ProcessWorkspaceCommandKind Kind,
    string Text,
    string Icon,
    bool IsEnabled,
    string? DisabledReason);

public sealed record ProcessWorkspaceAuthorizationProjection(
    bool CanReadDefinitions,
    bool CanRefreshProjections,
    bool CanOpenAgentContext,
    bool CanEditDefinitions,
    bool CanLaunchRuns);

public sealed record ProcessWorkspaceProjectionRefreshProjection(
    ProcessWorkspaceProjectionStatus Status,
    DateTimeOffset ObservedAtUtc,
    long SourceGlobalSequence,
    int BacklogEventCount,
    string Summary);

public sealed record ProcessDefinitionScopeGroupProjection(
    ProcessDefinitionCatalogScopeKind ScopeKind,
    string Label,
    string Description,
    int Count,
    bool IsSelected);

[Description("A process definition in the global catalog, with lifecycle, scope and compatibility information.")]
public sealed record ProcessDefinitionCatalogItemProjection(
    [property: Description("Opaque catalog identity of this item; do not derive it from its display name.")]
    ProcessDefinitionCatalogItemKey Key,
    [property: Description("Catalog scope classification used by the process definition filter.")]
    ProcessDefinitionCatalogScopeKind ScopeKind,
    [property: Description("Display name of the process definition.")]
    string Name,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary,
    [property: Description("Current lifecycle or command outcome of this projection.")]
    ProcessDefinitionCatalogItemStatus Status,
    [property: Description("Operational criticality classification of the definition.")]
    string Criticality,
    [property: Description("Operating mode governing execution of the definition.")]
    string OperatingMode,
    [property: Description("Last recorded definition update time in UTC.")]
    DateTimeOffset UpdatedAtUtc,
    [property: Description("Number of compatibility findings for this definition.")]
    int CompatibilityIssueCount);

public sealed record ProcessDefinitionCatalogCommandReceipt(
    Guid ReceiptId,
    ProcessDefinitionCatalogCommandKind CommandKind,
    ProcessDefinitionCatalogCommandStatus Status,
    ProcessDefinitionCatalogRefreshToken RefreshToken,
    int AffectedDefinitionCount,
    DateTimeOffset AcceptedAtUtc,
    string Summary);

[Description("Definition identity, ownership, intended customer and value statement.")]
public sealed record ProcessDefinitionEditorIdentityProjection(
    [property: Description("Display name of the process definition.")]
    string Name,
    [property: Description("Display label of the definition scope.")]
    string ScopeLabel,
    [property: Description("Display name of the customer associated with the definition.")]
    string CustomerName,
    [property: Description("Display name of the definition owner.")]
    string OwnerName,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary,
    [property: Description("Description of the business value the process is intended to deliver.")]
    string ValueStatement);

[Description("Definition criticality, autonomy, working state and operator governance explanations.")]
public sealed record ProcessDefinitionEditorGovernanceProjection(
    [property: Description("Operational criticality classification of the definition.")]
    ProcessDefinitionCriticalityLevel Criticality,
    [property: Description("Level of autonomy permitted by the definition governance policy.")]
    ProcessDefinitionAutonomyLevel AutonomyLevel,
    [property: Description("Operating mode governing execution of the definition.")]
    ProcessDefinitionOperatingModeKind OperatingMode,
    [property: Description("Working lifecycle state of the editable definition.")]
    ProcessDefinitionAuthoringStatus WorkingStatus,
    [property: Description("Explanation of any manager override applied to the definition.")]
    string ManagerOverrideSummary,
    [property: Description("Operator-authored notes explaining governance decisions.")]
    string GovernanceNotes,
    [property: Description("Operator-authored explanation of the current definition changes.")]
    string ChangeSummary,
    [property: Description("Human-readable summary of the effective governance policy.")]
    string GovernancePolicySummary);

[Description("Human-readable interface, constitutional and operating-mode contracts of a definition.")]
public sealed record ProcessDefinitionEditorContractProjection(
    [property: Description("Human-readable contract for interaction with the process definition.")]
    string InterfaceContractSummary,
    [property: Description("Human-readable constitutional rules applying to the definition.")]
    string ConstitutionRuleSummary,
    [property: Description("Human-readable explanation of the selected operating mode.")]
    string OperatingModeSummary);

[Description("Definition simulation prerequisites and readiness; reading this projection never starts a simulation.")]
public sealed record ProcessDefinitionEditorSimulationProjection(
    [property: Description("Human-readable explanation of simulation prerequisites and missing inputs.")]
    string SimulationReadinessSummary,
    [property: Description("Number of steps in the definition.")]
    int StepCount,
    [property: Description("Number of roles that must be staffed.")]
    int RequiredRoleCount,
    [property: Description("Number of required artifact expectations in the definition.")]
    int RequiredArtifactExpectationCount,
    [property: Description("Whether the current definition satisfies simulation prerequisites.")]
    bool IsReadyForSimulation);

public sealed record ProcessDefinitionEditorDraftProjection(
    ProcessDefinitionCatalogItemKey DefinitionKey,
    ProcessDefinitionEditorIdentityProjection Identity,
    ProcessDefinitionEditorGovernanceProjection Governance,
    ProcessDefinitionEditorContractProjection Contracts,
    ProcessDefinitionEditorSimulationProjection Simulation);

[Description("One definition validation finding with its stable reason code and suggested correction.")]
public sealed record ProcessDefinitionEditorLintIssueProjection(
    [property: Description("Stable machine-readable reason code of this validation finding.")]
    string Code,
    [property: Description("Severity of this validation finding.")]
    ProcessDefinitionEditorLintSeverity Severity,
    [property: Description("Authoring section to which this validation finding belongs.")]
    ProcessDefinitionEditorLintSection Section,
    [property: Description("Human-readable explanation of this validation finding.")]
    string Message,
    [property: Description("Suggested authoring correction for this validation finding.")]
    string Suggestion);

[Description("Definition validation findings and derived warning/blocking indicators.")]
public sealed record ProcessDefinitionEditorLintProjection(
    [property: Description("Validation findings for the current projection.")]
    IReadOnlyList<ProcessDefinitionEditorLintIssueProjection> Issues)
{
    [Description("True when at least one finding has warning or error severity.")]
    public bool HasWarningsOrErrors => Issues.Any(issue => issue.Severity is ProcessDefinitionEditorLintSeverity.Warning or ProcessDefinitionEditorLintSeverity.Error);

    [Description("True when at least one error finding blocks the authoring action.")]
    public bool HasBlockingIssues => Issues.Any(issue => issue.Severity == ProcessDefinitionEditorLintSeverity.Error);
}

[Description("Availability of a definition-authoring command; this read API does not execute it.")]
public sealed record ProcessDefinitionEditorCommandProjection(
    [property: Description("Classification of this projected authoring item.")]
    ProcessDefinitionEditorCommandKind Kind,
    [property: Description("User-facing command label.")]
    string Text,
    [property: Description("Presentation icon name for the command or action.")]
    string Icon,
    [property: Description("Whether the authoring application currently allows this command; this flag does not grant HTTP authority.")]
    bool IsEnabled,
    [property: Description("Human-readable reason a command is unavailable, or null when it is available.")]
    string? DisabledReason);

[Description("Most recent definition command result and validation findings at its observed version.")]
public sealed record ProcessDefinitionEditorCommandReceipt(
    [property: Description("Unique identifier of the recorded authoring command receipt.")]
    Guid ReceiptId,
    [property: Description("Authoring command whose outcome this receipt records.")]
    ProcessDefinitionEditorCommandKind CommandKind,
    [property: Description("Current lifecycle or command outcome of this projection.")]
    ProcessDefinitionEditorCommandStatus Status,
    [property: Description("Opaque version of this authoring projection; it is not an authentication token.")]
    ProcessDefinitionEditorVersionToken VersionToken,
    [property: Description("UTC instant when this command result was observed.")]
    DateTimeOffset ObservedAtUtc,
    [property: Description("Human-readable explanation of this item; display text is not an executable contract.")]
    string Summary,
    [property: Description("Validation findings associated with the recorded command.")]
    IReadOnlyList<ProcessDefinitionEditorLintIssueProjection> LintIssues);

public sealed record ProcessDefinitionEditorCommand(
    ProcessWorkspaceShellScope Scope,
    ProcessDefinitionCatalogItemKey DefinitionKey,
    ProcessDefinitionEditorCommandKind CommandKind,
    ProcessDefinitionEditorVersionToken? ExpectedVersionToken,
    ProcessDefinitionEditorDraftProjection Draft);

public sealed record ProcessDefinitionEditorCommandResult(
    ProcessDefinitionEditorCommandReceipt Receipt,
    ProcessDefinitionEditorProjection Projection);

[Description("Current definition overview and optional role, step, canvas and template authoring projections. Read-only over this HTTP surface.")]
public sealed record ProcessDefinitionEditorProjection(
    [property: Description("Opaque catalog key of the process definition; preserve it exactly when constructing read URLs.")]
    ProcessDefinitionCatalogItemKey DefinitionKey,
    [property: Description("Opaque version of this authoring projection; it is not an authentication token.")]
    ProcessDefinitionEditorVersionToken VersionToken,
    [property: Description("Current lifecycle or command outcome of this projection.")]
    ProcessDefinitionAuthoringStatus Status,
    [property: Description("Definition name, ownership and business-purpose projection.")]
    ProcessDefinitionEditorIdentityProjection Identity,
    [property: Description("Current definition governance settings and explanations.")]
    ProcessDefinitionEditorGovernanceProjection Governance,
    [property: Description("Declared interface and behavior contracts for this authoring item.")]
    ProcessDefinitionEditorContractProjection Contracts,
    [property: Description("Simulation prerequisites and readiness, without executing a simulation.")]
    ProcessDefinitionEditorSimulationProjection Simulation,
    [property: Description("Validation findings for the current authoring snapshot.")]
    ProcessDefinitionEditorLintProjection Lint,
    [property: Description("Commands available in the authoring application; these read endpoints do not execute commands.")]
    IReadOnlyList<ProcessDefinitionEditorCommandProjection> Commands,
    [property: Description("Most recent command receipt, or null when this snapshot has no command receipt.")]
    ProcessDefinitionEditorCommandReceipt? LastCommandReceipt)
{
    [Description("Optional role authoring projection associated with this definition.")]
    public ProcessDefinitionRoleEditorProjection? RoleEditor { get; init; }

    [Description("Optional canvas authoring projection associated with this definition.")]
    public ProcessDefinitionCanvasEditorProjection? Canvas { get; init; }

    [Description("Optional step authoring projection associated with this definition.")]
    public ProcessDefinitionStepEditorProjection? StepEditor { get; init; }

    [Description("Optional template catalog projected for this definition.")]
    public ProcessTemplateCatalogProjection? TemplateCatalog { get; init; }
}

public sealed record ProcessDefinitionCatalogProjection(
    int PublishedDefinitionCount,
    int DraftDefinitionCount,
    int TemplateCompatibilityIssueCount,
    string Summary,
    string SearchText,
    ProcessDefinitionCatalogItemKey? SelectedDefinitionKey,
    IReadOnlyList<ProcessDefinitionScopeGroupProjection> ScopeGroups,
    IReadOnlyList<ProcessDefinitionCatalogItemProjection> Items,
    ProcessDefinitionCatalogItemProjection? SelectedItem,
    ProcessDefinitionEditorProjection? SelectedEditor,
    ProcessDefinitionCatalogCommandReceipt? LastCommandReceipt);

public sealed record ProcessLiveRunSummaryProjection(
    int ActiveRunCount,
    int AttentionRunCount,
    int FailedRunCount,
    DateTimeOffset? LastEventAtUtc,
    string Summary);

public sealed record ProcessRuntimeWorkspaceQueryProjection(
    ProcessRuntimeHistoryWindow HistoryWindow,
    int EventPage,
    int EventPageSize,
    Guid? SelectedRunId,
    bool AutoSelectRun = true,
    int TakeRuns = 100,
    ProcessRuntimeWorkspaceLoadOptions? LoadOptions = null)
{
    public IReadOnlyList<ProcessLiveProcessSnapshot>? PreviouslyLoadedRuns { get; init; }
}

public sealed record ProcessRuntimeStatsProjection(
    int ObservedRunCount,
    int ActiveRunCount,
    int AttentionRunCount,
    int FailedRunCount,
    int EventCount,
    int ManagerEventCount,
    int ToolCallCount,
    long DurationMs,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int TotalTokens,
    decimal EstimatedCost,
    decimal ActualCost)
{
    public static ProcessRuntimeStatsProjection Empty { get; } = new(
        ObservedRunCount: 0,
        ActiveRunCount: 0,
        AttentionRunCount: 0,
        FailedRunCount: 0,
        EventCount: 0,
        ManagerEventCount: 0,
        ToolCallCount: 0,
        DurationMs: 0,
        InputTokens: 0,
        CachedInputTokens: 0,
        OutputTokens: 0,
        TotalTokens: 0,
        EstimatedCost: 0m,
        ActualCost: 0m);
}

public sealed record ProcessRuntimeMetricPointProjection(
    DateTimeOffset TimestampUtc,
    int EventCount,
    int ManagerEventCount,
    int ToolCallCount,
    long DurationMs,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int TotalTokens,
    decimal EstimatedCost,
    decimal ActualCost);

public sealed record ProcessRuntimeToolUsageProjection(
    string ToolName,
    int CallCount,
    DateTimeOffset LastUsedAtUtc,
    string Summary);

public sealed record ProcessRuntimeActiveAgentProjection(
    Guid RunId,
    Guid StepInstanceId,
    string RunLabel,
    string StepKey,
    string RoleKey,
    string ExecutorKind,
    string ExecutorId,
    string ExecutorDisplayName,
    string Status,
    bool IsWorking,
    bool IsLeaseExpired,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ClaimedAtUtc,
    DateTimeOffset? LeaseExpiresAtUtc,
    string Summary)
{
    public Guid? ExecutionRunId { get; init; }

    public Guid? AgentId { get; init; }

    public string AgentName { get; init; } = string.Empty;

    public string AgentAvatarImageUrl { get; init; } = string.Empty;

    public string ProviderName { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string ExecutionState { get; init; } = string.Empty;

    public string ExecutionOutcome { get; init; } = string.Empty;

    public DateTimeOffset? ExecutionStartedAtUtc { get; init; }

    public DateTimeOffset? ExecutionUpdatedAtUtc { get; init; }

    public string CurrentActivity { get; init; } = string.Empty;

    public string LastError { get; init; } = string.Empty;

    public string ObservationSource { get; init; } = string.Empty;

    public IReadOnlyList<ProcessRuntimeActiveAgentActivityProjection> RecentActivities { get; init; } = [];

    public IReadOnlyList<ProcessRuntimeActiveAgentToolProjection> RecentTools { get; init; } = [];

    public IReadOnlyList<ProcessRuntimeActiveAgentArtifactProjection> Artifacts { get; init; } = [];
}

public sealed record ProcessRuntimeActiveAgentActivityProjection(
    DateTimeOffset CreatedAtUtc,
    string State,
    string Phase,
    string Message);

public sealed record ProcessRuntimeActiveAgentToolProjection(
    string ToolName,
    string RuntimeToolProviderKey,
    string RequestSummary,
    string ExitSummary,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);

public sealed record ProcessRuntimeActiveAgentArtifactProjection(
    string ArtifactKind,
    string DisplayName,
    string RelativePath,
    string Summary,
    DateTimeOffset CreatedAtUtc);

public sealed record ProcessRuntimeWorkspaceProjection(
    ProcessRuntimeHistoryWindow HistoryWindow,
    int EventPage,
    int EventPageSize,
    bool HasMoreEvents,
    Guid? SelectedRunId,
    ProcessRunDetailProjection? SelectedRun,
    IReadOnlyList<ProcessLiveProcessSnapshot> Runs,
    IReadOnlyList<ProcessTimelineEventProjection> Events,
    IReadOnlyList<ProcessIncidentProjection> Incidents,
    IReadOnlyList<ProcessManagerMessageProjection> ManagerMessages,
    IReadOnlyList<ProcessRuntimeActiveAgentProjection> ActiveAgents,
    ProcessRuntimeStatsProjection Stats,
    IReadOnlyList<ProcessRuntimeMetricPointProjection> MetricPoints,
    IReadOnlyList<ProcessRuntimeToolUsageProjection> ToolUsage,
    ProcessProjectionFreshness? Freshness,
    string Summary,
    string AttentionSummary)
{
    public ProcessRunRecord? SelectedRunRecord { get; init; }

    public IReadOnlyList<ProcessLiveProcessSnapshot>? ReusableRuns { get; init; }

    public ProcessWorkspaceProvenanceVector Provenance { get; init; } =
        ProcessWorkspaceProvenanceVector.Empty;

    public static ProcessRuntimeWorkspaceProjection Empty { get; } = new(
        ProcessRuntimeHistoryWindow.OneDay,
        EventPage: 0,
        EventPageSize: 25,
        HasMoreEvents: false,
        SelectedRunId: null,
        SelectedRun: null,
        Runs: [],
        Events: [],
        Incidents: [],
        ManagerMessages: [],
        ActiveAgents: [],
        ProcessRuntimeStatsProjection.Empty,
        MetricPoints: [],
        ToolUsage: [],
        Freshness: null,
        Summary: "Runtime projection snapshots are not available in this workspace shell.",
        AttentionSummary: "No runtime attention signals are available.");
}

public sealed record ProcessWorkspaceAgentEntryProjection(
    ProcessWorkspaceAgentEntryKind Kind,
    bool IsAvailable,
    string Label,
    string ContextKey,
    string? DisabledReason);

public sealed record ProcessWorkspaceShellProjection(
    ProcessWorkspaceShellScope Scope,
    ProcessWorkspaceSelectionProjection Selection,
    string Title,
    string Subtitle,
    ProcessDefinitionCatalogProjection DefinitionCatalog,
    ProcessLiveRunSummaryProjection LiveRuns,
    ProcessWorkspaceProjectionRefreshProjection Refresh,
    ProcessWorkspaceAuthorizationProjection Authorization,
    IReadOnlyList<ProcessWorkspaceTabProjection> Tabs,
    IReadOnlyList<ProcessWorkspaceCommandProjection> Commands,
    ProcessWorkspaceAgentEntryProjection AgentEntry)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProcessProjectionProjectBinding? ProjectBinding { get; init; }

    public ProcessRuntimeWorkspaceProjection Runtime { get; init; } = ProcessRuntimeWorkspaceProjection.Empty;

    public ProcessWorkspaceProvenanceVector Provenance { get; init; } =
        ProcessWorkspaceProvenanceVector.Empty;
}
