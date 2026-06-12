using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

public interface IProcessManagerChatService
{
    Task<ProcessManagerChatProjection> LoadAsync(
        ProcessManagerChatProjectionQuery query,
        CancellationToken cancellationToken = default);

    Task<ProcessManagerChatProjection> StartNewThreadAsync(
        ProcessManagerChatProjectionQuery query,
        CancellationToken cancellationToken = default);

    Task<ProcessManagerChatProjection> SendMessageAsync(
        ProcessManagerChatMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<ProcessManagerChatProjection> RenameThreadAsync(
        ProcessManagerChatRenameRequest request,
        CancellationToken cancellationToken = default);

    Task<ProcessManagerChatProjection> RespondToApprovalsAsync(
        ProcessManagerChatApprovalRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessManagerChatProjectionQuery(
    Guid ProcessRunId,
    Guid ProcessDefinitionId,
    string ProcessDefinitionName,
    string ProcessRunName,
    ProcessRunStatus RunStatus,
    int CompletedStepCount,
    int TotalStepCount,
    int BlockedStepCount,
    int CapabilityGapCount,
    int ActiveExecutionCount,
    int PendingApprovalCount,
    decimal EstimatedCost,
    decimal ActualCost,
    DateTimeOffset UpdatedAtUtc,
    Guid? ManagerAgentId,
    string ManagerAgentName,
    Guid? ProjectId,
    Guid? ChatSessionId = null)
{
    public decimal TreeEstimatedCost { get; init; } = EstimatedCost;

    public decimal TreeActualCost { get; init; } = ActualCost;

    public int DescendantRunCount { get; init; }
}

public sealed record ProcessManagerChatMessageRequest(
    ProcessManagerChatProjectionQuery Query,
    Guid? ChatSessionId,
    string Prompt);

public sealed record ProcessManagerChatRenameRequest(
    ProcessManagerChatProjectionQuery Query,
    Guid ChatSessionId,
    string Title);

public sealed record ProcessManagerChatApprovalRequest(
    ProcessManagerChatProjectionQuery Query,
    Guid ChatSessionId,
    bool Approved,
    bool AutoApprovePendingToolCalls);

public sealed record ProcessManagerChatProjection(
    ProcessManagerChatProjectionQuery Query,
    AgentDefinition? ManagerAgent,
    ChatAgentWorkspaceSnapshot? Workspace,
    IReadOnlyList<ChatSessionSummaryRecord> RecentSessions,
    IReadOnlyList<ExecutionLogEntry> ExecutionLog,
    IReadOnlyList<AgentRunMetric> Metrics,
    string ManagerLabel,
    string RunLabel,
    string RunStateText,
    string RunStateTone,
    string ManagerResolutionReasonCode,
    int ManagerResolutionConfidence,
    string ManagerResolutionSummary,
    string ErrorMessage)
{
    public bool IsAvailable => ManagerAgent is not null && string.IsNullOrWhiteSpace(ErrorMessage);
}
