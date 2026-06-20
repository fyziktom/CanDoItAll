namespace CanDoItAll.Processes.Projections;

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

public enum ProcessDefinitionCatalogScopeKind
{
    All,
    Global,
    Project
}

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

public enum ProcessDefinitionAuthoringStatus
{
    TemplateDefault,
    Draft,
    Published,
    Archived
}

public enum ProcessDefinitionCriticalityLevel
{
    Unspecified,
    Low,
    Standard,
    High,
    MissionCritical
}

public enum ProcessDefinitionAutonomyLevel
{
    Unspecified,
    Manual,
    Assisted,
    Guarded,
    Delegated
}

public enum ProcessDefinitionOperatingModeKind
{
    Unspecified,
    Manual,
    AssistedExecution,
    GovernedLive
}

public enum ProcessDefinitionEditorCommandKind
{
    SaveDraft,
    Publish,
    Archive,
    Delete
}

public enum ProcessDefinitionEditorCommandStatus
{
    Accepted,
    Rejected
}

public enum ProcessDefinitionEditorLintSeverity
{
    Info,
    Warning,
    Error
}

public enum ProcessDefinitionEditorLintSection
{
    Identity,
    Governance,
    Contracts,
    Simulation
}

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
    ProcessRuntimeWorkspaceQueryProjection? RuntimeQuery = null);

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

public sealed record ProcessDefinitionCatalogItemProjection(
    ProcessDefinitionCatalogItemKey Key,
    ProcessDefinitionCatalogScopeKind ScopeKind,
    string Name,
    string Summary,
    ProcessDefinitionCatalogItemStatus Status,
    string Criticality,
    string OperatingMode,
    DateTimeOffset UpdatedAtUtc,
    int CompatibilityIssueCount);

public sealed record ProcessDefinitionCatalogCommandReceipt(
    Guid ReceiptId,
    ProcessDefinitionCatalogCommandKind CommandKind,
    ProcessDefinitionCatalogCommandStatus Status,
    ProcessDefinitionCatalogRefreshToken RefreshToken,
    int AffectedDefinitionCount,
    DateTimeOffset AcceptedAtUtc,
    string Summary);

public sealed record ProcessDefinitionEditorIdentityProjection(
    string Name,
    string ScopeLabel,
    string CustomerName,
    string OwnerName,
    string Summary,
    string ValueStatement);

public sealed record ProcessDefinitionEditorGovernanceProjection(
    ProcessDefinitionCriticalityLevel Criticality,
    ProcessDefinitionAutonomyLevel AutonomyLevel,
    ProcessDefinitionOperatingModeKind OperatingMode,
    ProcessDefinitionAuthoringStatus WorkingStatus,
    string ManagerOverrideSummary,
    string GovernanceNotes,
    string ChangeSummary,
    string GovernancePolicySummary);

public sealed record ProcessDefinitionEditorContractProjection(
    string InterfaceContractSummary,
    string ConstitutionRuleSummary,
    string OperatingModeSummary);

public sealed record ProcessDefinitionEditorSimulationProjection(
    string SimulationReadinessSummary,
    int StepCount,
    int RequiredRoleCount,
    int RequiredArtifactExpectationCount,
    bool IsReadyForSimulation);

public sealed record ProcessDefinitionEditorDraftProjection(
    ProcessDefinitionCatalogItemKey DefinitionKey,
    ProcessDefinitionEditorIdentityProjection Identity,
    ProcessDefinitionEditorGovernanceProjection Governance,
    ProcessDefinitionEditorContractProjection Contracts,
    ProcessDefinitionEditorSimulationProjection Simulation);

public sealed record ProcessDefinitionEditorLintIssueProjection(
    string Code,
    ProcessDefinitionEditorLintSeverity Severity,
    ProcessDefinitionEditorLintSection Section,
    string Message,
    string Suggestion);

public sealed record ProcessDefinitionEditorLintProjection(
    IReadOnlyList<ProcessDefinitionEditorLintIssueProjection> Issues)
{
    public bool HasWarningsOrErrors => Issues.Any(issue => issue.Severity is ProcessDefinitionEditorLintSeverity.Warning or ProcessDefinitionEditorLintSeverity.Error);

    public bool HasBlockingIssues => Issues.Any(issue => issue.Severity == ProcessDefinitionEditorLintSeverity.Error);
}

public sealed record ProcessDefinitionEditorCommandProjection(
    ProcessDefinitionEditorCommandKind Kind,
    string Text,
    string Icon,
    bool IsEnabled,
    string? DisabledReason);

public sealed record ProcessDefinitionEditorCommandReceipt(
    Guid ReceiptId,
    ProcessDefinitionEditorCommandKind CommandKind,
    ProcessDefinitionEditorCommandStatus Status,
    ProcessDefinitionEditorVersionToken VersionToken,
    DateTimeOffset ObservedAtUtc,
    string Summary,
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

public sealed record ProcessDefinitionEditorProjection(
    ProcessDefinitionCatalogItemKey DefinitionKey,
    ProcessDefinitionEditorVersionToken VersionToken,
    ProcessDefinitionAuthoringStatus Status,
    ProcessDefinitionEditorIdentityProjection Identity,
    ProcessDefinitionEditorGovernanceProjection Governance,
    ProcessDefinitionEditorContractProjection Contracts,
    ProcessDefinitionEditorSimulationProjection Simulation,
    ProcessDefinitionEditorLintProjection Lint,
    IReadOnlyList<ProcessDefinitionEditorCommandProjection> Commands,
    ProcessDefinitionEditorCommandReceipt? LastCommandReceipt)
{
    public ProcessDefinitionRoleEditorProjection? RoleEditor { get; init; }

    public ProcessDefinitionCanvasEditorProjection? Canvas { get; init; }

    public ProcessDefinitionStepEditorProjection? StepEditor { get; init; }

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
    int TakeRuns = 100);

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
    public ProcessRuntimeWorkspaceProjection Runtime { get; init; } = ProcessRuntimeWorkspaceProjection.Empty;
}
