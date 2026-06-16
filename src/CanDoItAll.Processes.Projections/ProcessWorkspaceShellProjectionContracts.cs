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
    bool ForceRefresh);

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
    ProcessWorkspaceAgentEntryProjection AgentEntry);
