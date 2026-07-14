using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public enum HrAgentUsageScope
{
    All,
    BasicChat,
    Process,
    Workflow,
    Other
}

public enum HrAgentTextTrust
{
    UntrustedAgentCatalogData,
    UntrustedPeerAgentResponse
}

public sealed record HrAgentsSearchInput(
    string Query = "",
    AgentLifecycleStatus? Status = null,
    AgentWorkloadKind? Workload = null,
    int Take = 20);

public sealed record HrAgentIdInput(Guid AgentId);

public sealed record HrAgentSearchItem(
    Guid AgentId,
    string Name,
    string RoleTitle,
    string Summary,
    AgentLifecycleStatus Status,
    AgentWorkloadKind Workload,
    string ProviderName,
    string Model,
    int CapabilityCount,
    IReadOnlyList<string> Tags,
    DateTimeOffset UpdatedAtUtc)
{
    public HrAgentTextTrust TextTrust => HrAgentTextTrust.UntrustedAgentCatalogData;
}

public sealed record HrAgentSearchResult(
    IReadOnlyList<HrAgentSearchItem> Agents,
    int Count,
    bool IsTruncated);

public sealed record HrAgentSafePermissions(
    bool CanUseTools,
    bool CanAskOtherAgents,
    bool CanEscalateToHuman,
    bool CanObserveOtherAgents,
    bool CanScheduleWork,
    bool RequiresApprovalForExternalCalls,
    bool AutoApproveExternalCallsByDefault);

public sealed record HrAgentCapabilityDescriptor(
    Guid Id,
    string Key,
    CapabilityKind Kind,
    string Name,
    CapabilityProofStatus ProofStatus)
{
    public HrAgentTextTrust TextTrust => HrAgentTextTrust.UntrustedAgentCatalogData;
}

public enum HrAgentAvatarKind
{
    None,
    BundledAsset,
    EmbeddedData,
    ExternalReference
}

public sealed record HrAgentAvatarMetadata(
    bool IsPresent,
    HrAgentAvatarKind Kind,
    string ContentType,
    int? ByteCount);

public sealed record HrAgentSafeSettings(
    Guid AgentId,
    string Name,
    string RoleTitle,
    string Summary,
    string Instructions,
    HrAgentAvatarMetadata Avatar,
    AgentLifecycleStatus Status,
    Guid? ProviderProfileId,
    string ProviderName,
    string Model,
    AgentWorkloadKind Workload,
    AgentChatHistoryMode ChatHistoryMode,
    double Temperature,
    HrAgentSafePermissions Permissions,
    IReadOnlyList<HrAgentCapabilityDescriptor> Capabilities,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public HrAgentTextTrust TextTrust => HrAgentTextTrust.UntrustedAgentCatalogData;
}

public sealed record HrAgentProviderOption(
    Guid Id,
    string Name,
    ProviderKind Kind,
    ProviderProfilePurpose Purpose,
    string DefaultModel,
    IReadOnlyList<string> SuggestedModels,
    bool IsEnabled)
{
    public HrAgentTextTrust TextTrust => HrAgentTextTrust.UntrustedAgentCatalogData;
}

public sealed record HrAgentTeamOption(Guid Id, string Name, string Description)
{
    public HrAgentTextTrust TextTrust => HrAgentTextTrust.UntrustedAgentCatalogData;
}

public sealed record HrAgentCreationOptionsResult(
    IReadOnlyList<HrAgentProviderOption> Providers,
    IReadOnlyList<HrAgentCapabilityDescriptor> Capabilities,
    IReadOnlyList<HrAgentTeamOption> Teams,
    IReadOnlyList<AgentLifecycleStatus> LifecycleStatuses,
    IReadOnlyList<AgentWorkloadKind> Workloads,
    IReadOnlyList<AgentChatHistoryMode> ChatHistoryModes)
{
    public HrAgentTextTrust TextTrust => HrAgentTextTrust.UntrustedAgentCatalogData;
}

public sealed record HrAgentPermissionsInput(
    bool CanUseTools = true,
    bool CanAskOtherAgents = true,
    bool CanEscalateToHuman = true,
    bool CanObserveOtherAgents = false,
    bool CanScheduleWork = false,
    bool RequiresApprovalForExternalCalls = true);

public sealed record HrAgentCreateInput(
    string Name,
    string RoleTitle,
    string Summary,
    string Instructions,
    Guid? ProviderProfileId = null,
    string Model = "",
    AgentWorkloadKind Workload = AgentWorkloadKind.General,
    AgentChatHistoryMode ChatHistoryMode = AgentChatHistoryMode.FrameworkManaged,
    double Temperature = 0.2d,
    IReadOnlyList<Guid>? CapabilityIds = null,
    IReadOnlyList<string>? Tags = null,
    HrAgentPermissionsInput? Permissions = null,
    Guid? TeamId = null);

public sealed record HrAgentPermissionsPatch(
    bool? CanUseTools = null,
    bool? CanAskOtherAgents = null,
    bool? CanEscalateToHuman = null,
    bool? CanObserveOtherAgents = null,
    bool? CanScheduleWork = null,
    bool? RequiresApprovalForExternalCalls = null);

public sealed record HrAgentSettingsUpdateInput(
    Guid AgentId,
    DateTimeOffset ExpectedUpdatedAtUtc,
    string? Name = null,
    string? RoleTitle = null,
    string? Summary = null,
    string? Instructions = null,
    AgentLifecycleStatus? Status = null,
    Guid? ProviderProfileId = null,
    bool ClearProviderProfile = false,
    string? Model = null,
    AgentWorkloadKind? Workload = null,
    AgentChatHistoryMode? ChatHistoryMode = null,
    double? Temperature = null,
    IReadOnlyList<Guid>? CapabilityIds = null,
    IReadOnlyList<string>? Tags = null,
    HrAgentPermissionsPatch? Permissions = null);

public sealed record HrAgentMutationResult(
    Guid AgentId,
    string Name,
    AgentLifecycleStatus Status,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<string> Warnings);

public sealed record HrAgentAvatarGenerateInput(
    Guid AgentId,
    DateTimeOffset ExpectedUpdatedAtUtc,
    string VisualBrief,
    int OutputCompression = 35);

public sealed record HrAgentAvatarGenerateResult(
    Guid AgentId,
    string ProviderName,
    string Model,
    string ContentType,
    int ContentLength,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<string> Warnings);

public sealed record HrAgentUsageInput(
    Guid? AgentId = null,
    HrAgentUsageScope Scope = HrAgentUsageScope.All,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null);

public sealed record HrAgentUsageResult(
    Guid? AgentId,
    HrAgentUsageScope Scope,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int RunCount,
    int FailedRunCount,
    int ObservationCount,
    int KnownUsageObservationCount,
    int EstimatedUsageObservationCount,
    int UnknownUsageObservationCount,
    int KnownCostObservationCount,
    int UnknownCostObservationCount,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    decimal KnownCostUsd,
    bool IsComplete,
    string CostQualification);

public sealed record HrAgentProcessHistoryInput(
    Guid AgentId,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int Take = 20);

public sealed record HrAgentProcessAttempt(
    Guid ExecutionRunId,
    string ProcessStepId,
    int AttemptNumber,
    ExecutionState State,
    RunOutcome? Outcome,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string FailureEvidence);

public sealed record HrAgentProcessParticipant(
    Guid AgentId,
    string Name,
    bool CanObserveOtherAgents,
    bool EligibleReviewManager)
{
    public HrAgentTextTrust TextTrust => HrAgentTextTrust.UntrustedAgentCatalogData;
}

public sealed record HrAgentProcessRunReview(
    Guid ProcessRunId,
    int AttemptCount,
    int ReturnedAttemptCount,
    bool AttemptsTruncated,
    int RepeatedStepCount,
    int FailedAttemptCount,
    int SuccessfulAttemptCount,
    DateTimeOffset FirstObservedAtUtc,
    DateTimeOffset LastObservedAtUtc,
    IReadOnlyList<HrAgentProcessAttempt> Attempts,
    int ParticipantCount,
    int ReturnedParticipantCount,
    bool ParticipantsTruncated,
    IReadOnlyList<HrAgentProcessParticipant> Participants);

public sealed record HrAgentProcessHistoryResult(
    Guid AgentId,
    string AgentName,
    IReadOnlyList<HrAgentProcessRunReview> ProcessRuns,
    int Count,
    bool IsTruncated)
{
    public HrAgentTextTrust TextTrust => HrAgentTextTrust.UntrustedAgentCatalogData;
}

public sealed record HrAgentManagerReviewRequestInput(
    Guid ProcessRunId,
    Guid TargetAgentId,
    Guid ManagerAgentId,
    string Question);

public sealed record HrAgentManagerReviewRequestResult(
    Guid ProcessRunId,
    Guid TargetAgentId,
    Guid ManagerAgentId,
    Guid? ChatSessionId,
    Guid ExecutionRunId,
    string ManagerResponse,
    string Qualification)
{
    public HrAgentTextTrust ManagerResponseTrust => HrAgentTextTrust.UntrustedPeerAgentResponse;
}
