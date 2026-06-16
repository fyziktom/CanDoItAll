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
