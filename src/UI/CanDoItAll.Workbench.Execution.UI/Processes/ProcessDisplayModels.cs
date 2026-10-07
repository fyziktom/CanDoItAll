namespace CanDoItAll.Modules.Workbench.Pages;

public sealed record ProjectStructureProcessLinkOption(
    Guid DefinitionId,
    string DisplayName,
    string ScopeLabel,
    string Status,
    bool HasPublishedVersion);


public enum ProjectStructureProcessStartStage {
    Confirm,
    Staffing
}

public sealed record ProjectStructureProcessStartCandidateSelection(
    Guid LaunchPlanRoleId,
    Guid CandidateId);

public sealed record ProjectStructureProcessStartCandidateState(
    Guid CandidateId,
    Guid? TechnicalAgentId,
    string DisplayName,
    string CandidateKindLabel,
    string ExecutorKind,
    string ScoreLabel,
    bool IsSelected,
    bool IsRecommended,
    bool RequiresProvisioning,
    bool IsResolvable,
    string RecommendationSummary,
    string AvailabilitySummary,
    string SourceRegistryKey,
    string AgentProviderName = "",
    string AgentModel = "",
    string AgentRoleTitle = "",
    string AgentSummary = "",
    string AgentStatusLabel = "",
    string AgentWorkloadLabel = "",
    string AgentAvatarImageUrl = "",
    IReadOnlyList<string>? ToolNames = null,
    IReadOnlyList<string>? SkillNames = null,
    int MatchScore = 0);

public sealed record ProjectStructureProcessStartRoleState(
    Guid LaunchPlanRoleId,
    string DisplayName,
    string PreferredExecutorKind,
    bool IsRequired,
    bool IsResolved,
    bool RequiresProvisioning,
    string SelectionSummary,
    string ReadinessSummary,
    IReadOnlyList<ProjectStructureProcessStartCandidateState> Candidates) {
    public string StepKey { get; init; } = string.Empty;
    public string RoleKey { get; init; } = string.Empty;
    public IReadOnlyList<ProjectStructureProcessStartCandidateState> DirectoryCandidates { get; init; } = [];

    public bool HasBlockingGap => IsRequired && !IsResolved;
}


public sealed record ProjectStructureProcessEstimateSummary(
    decimal EstimatedCostUsd,
    int EstimatedElapsedMinutes,
    int EstimatedTouchMinutes,
    string ConfidenceLabel,
    string SourceLabel,
    string Summary);

