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

public sealed record ProcessWorkspaceShellRequest(
    ProcessWorkspaceShellScope Scope,
    ProcessWorkspaceSelectionProjection Selection,
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

public sealed record ProcessDefinitionCatalogSummaryProjection(
    int PublishedDefinitionCount,
    int DraftDefinitionCount,
    int TemplateCompatibilityIssueCount,
    string Summary);

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
    ProcessDefinitionCatalogSummaryProjection DefinitionCatalog,
    ProcessLiveRunSummaryProjection LiveRuns,
    ProcessWorkspaceProjectionRefreshProjection Refresh,
    ProcessWorkspaceAuthorizationProjection Authorization,
    IReadOnlyList<ProcessWorkspaceTabProjection> Tabs,
    IReadOnlyList<ProcessWorkspaceCommandProjection> Commands,
    ProcessWorkspaceAgentEntryProjection AgentEntry);
