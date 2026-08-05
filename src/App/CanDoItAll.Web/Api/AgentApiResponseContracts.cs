using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Web.Api;

internal sealed record AgentCatalogItemApiResponse(
    Guid Id,
    string Name,
    string RoleTitle,
    string Summary,
    AgentLifecycleStatus Status,
    string Model,
    bool IsTemplate,
    string? AvatarImageUrl);

internal sealed record AgentPendingApprovalApiResponse(
    string ApprovalId,
    string ToolName,
    string ToolKind);

internal sealed record AgentChatMessageApiResponse(
    Guid Id,
    ChatMessageRole Role,
    string Content,
    DateTimeOffset CreatedAtUtc);

internal sealed record AgentChatSessionSummaryApiResponse(
    Guid Id,
    Guid AgentId,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int MessageCount,
    string LastMessagePreview,
    int PendingApprovalCount,
    bool AutoApprovePendingToolCalls);

internal sealed record AgentChatSessionApiResponse(
    Guid Id,
    Guid AgentId,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<AgentChatMessageApiResponse> Messages,
    Guid? LatestExecutionRunId,
    IReadOnlyList<AgentPendingApprovalApiResponse> PendingApprovals,
    bool AutoApprovePendingToolCalls);

internal sealed record AgentChatRunSummaryApiResponse(
    Guid ExecutionRunId,
    Guid AgentId,
    Guid? ChatSessionId,
    string Title,
    ExecutionState State,
    string Phase,
    string Message,
    RunOutcome? Outcome,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    TimeSpan? Duration,
    decimal KnownCostUsd,
    bool HasUnknownCost);

internal sealed record AgentChatWorkspaceApiResponse(
    Guid AgentId,
    IReadOnlyList<AgentChatSessionSummaryApiResponse> Sessions,
    AgentChatSessionApiResponse? SelectedSession,
    Guid? SelectedSessionId,
    AgentChatRunSummaryApiResponse? LatestRun,
    AgentExecutionRunApiResponse? SelectedRun);

internal sealed record AgentChatPageBootstrapApiResponse(
    IReadOnlyList<AgentCatalogItemApiResponse> Agents,
    Guid? InitialAgentId,
    AgentChatWorkspaceApiResponse? SelectedAgentWorkspace);

internal sealed record AgentExecutionRunApiResponse(
    Guid Id,
    Guid AgentId,
    Guid? ChatSessionId,
    string Title,
    string ProviderName,
    string Model,
    ExecutionState State,
    RunOutcome? Outcome,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<AgentPendingApprovalApiResponse> PendingApprovals,
    int PendingApprovalCount,
    bool AutoApprovePendingToolCalls,
    long Revision);

internal sealed record AgentExecutionApprovalApiResponse(
    string ApprovalId,
    Guid ExecutionRunId,
    string ToolName,
    string ToolKind,
    ExecutionApprovalStatus Status,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    string DecisionSourceKind);

internal sealed record AgentExecutionLogApiResponse(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    ExecutionState State,
    string Phase,
    string Message);

internal sealed record AgentRunMetricApiResponse(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    RunOutcome Outcome,
    string ProviderName,
    string Model,
    long DurationMs,
    int InputTokens,
    int CachedInputTokens,
    int CacheWriteTokens,
    int OutputTokens,
    int ToolCalls,
    decimal CostUsd);

internal sealed record AgentChatRuntimeApiResponse(
    IReadOnlyList<AgentExecutionLogApiResponse> ExecutionLog,
    IReadOnlyList<AgentRunMetricApiResponse> Metrics);

internal sealed record AgentExecutionArtifactApiResponse(
    Guid Id,
    string ArtifactKind,
    string DisplayName,
    string RelativePath,
    string ContentType,
    string ProducedBy,
    string Summary,
    DateTimeOffset CreatedAtUtc);

internal sealed record AgentExecutionCheckpointApiResponse(
    Guid Id,
    string CheckpointKind,
    ExecutionState RunState,
    int PendingApprovalCount,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset? ResumedAtUtc);

internal sealed record AgentExecutionToolReceiptApiResponse(
    Guid Id,
    Guid ExecutionRunId,
    string ToolFamily,
    string ToolName,
    string RiskClass,
    string ApprovalMode,
    string IsolationGuarantee,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string RuntimeToolProviderName,
    ToolExecutionSideEffectMode DeclaredSideEffectMode);

internal sealed record AgentProviderUsageTotalsApiResponse(
    int ObservationCount,
    int KnownObservationCount,
    int UnknownObservationCount,
    int InputTokens,
    int CachedInputTokens,
    int CacheWriteTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    int ToolCallCount,
    int KnownCostObservationCount,
    int UnknownCostObservationCount,
    decimal KnownCostUsd);

internal sealed record AgentExecutionRunDetailApiResponse(
    AgentExecutionRunApiResponse Run,
    AgentChatSessionApiResponse? ChatSession,
    IReadOnlyList<AgentExecutionLogApiResponse> ExecutionLog,
    IReadOnlyList<AgentRunMetricApiResponse> Metrics,
    IReadOnlyList<AgentExecutionApprovalApiResponse> Approvals,
    IReadOnlyList<AgentExecutionArtifactApiResponse> Artifacts,
    IReadOnlyList<AgentExecutionCheckpointApiResponse> Checkpoints,
    IReadOnlyList<AgentExecutionToolReceiptApiResponse> ToolReceipts,
    AgentProviderUsageTotalsApiResponse UsageTotals);

internal sealed record AgentChatRunApiResponse(
    Guid ChatSessionId,
    AgentChatMessageApiResponse AssistantMessage,
    AgentRunMetricApiResponse Metric,
    Guid ExecutionRunId,
    ExecutionState State);

internal sealed record AgentExecutionRunResultApiResponse(
    Guid ExecutionRunId,
    Guid? ChatSessionId,
    string ResponseText,
    AgentChatMessageApiResponse? AssistantMessage,
    AgentRunMetricApiResponse Metric,
    ExecutionState State,
    AgentStructuredOutputApiResponse? StructuredOutput);

internal sealed record AgentStructuredOutputValidationErrorApiResponse(
    string Code,
    string Message,
    string Path);

internal sealed record AgentStructuredOutputApiResponse(
    JsonElement? Data,
    AgentJsonSchemaOutputValidationStatus ValidationStatus,
    IReadOnlyList<AgentStructuredOutputValidationErrorApiResponse> ValidationErrors);

internal static class AgentApiResponseMapper
{
    public static AgentChatPageBootstrapApiResponse ToChatPageBootstrap(ChatPageBootstrapSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new AgentChatPageBootstrapApiResponse(
            source.Agents.Select(ToAgentCatalogItem).ToArray(),
            source.InitialAgentId,
            source.SelectedAgentWorkspace is null
                ? null
                : ToChatWorkspace(source.SelectedAgentWorkspace));
    }

    public static AgentChatWorkspaceApiResponse ToChatWorkspace(ChatAgentWorkspaceSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new AgentChatWorkspaceApiResponse(
            source.AgentId,
            source.Sessions.Select(ToChatSessionSummary).ToArray(),
            source.SelectedSession is null
                ? null
                : ToChatSession(source.SelectedSession),
            source.SelectedSessionId,
            source.LatestRun is null
                ? null
                : ToChatRunSummary(source.LatestRun),
            source.SelectedRun is null
                ? null
                : ToExecutionRun(source.SelectedRun));
    }

    public static AgentChatSessionApiResponse ToChatSession(ChatSessionRecord source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var compatibility = source.Compatibility;
        return new AgentChatSessionApiResponse(
            source.Id,
            source.AgentId,
            source.Title,
            source.CreatedAtUtc,
            source.UpdatedAtUtc,
            source.Messages.Select(ToChatMessage).ToArray(),
            source.LatestExecutionRunId,
            MapPendingApprovals(compatibility?.PendingApprovals ?? []),
            compatibility?.AutoApprovePendingToolCalls ?? false);
    }

    public static IReadOnlyList<AgentChatSessionSummaryApiResponse> ToChatSessionSummaries(
        IReadOnlyList<ChatSessionSummaryRecord> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(ToChatSessionSummary).ToArray();
    }

    public static IReadOnlyList<AgentChatSessionApiResponse> ToChatSessions(
        IReadOnlyList<ChatSessionRecord> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(ToChatSession).ToArray();
    }

    public static IReadOnlyList<AgentExecutionRunApiResponse> ToExecutionRuns(
        IReadOnlyList<ExecutionRunRecord> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(ToExecutionRun).ToArray();
    }

    public static AgentExecutionRunApiResponse ToExecutionRun(ExecutionRunRecord source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var pendingApprovals = MapPendingApprovals(source.PendingApprovals);
        return new AgentExecutionRunApiResponse(
            source.Id,
            source.AgentId,
            source.ChatSessionId,
            source.Title,
            source.ProviderName,
            source.Model,
            source.State,
            source.Outcome,
            source.CreatedAtUtc,
            source.UpdatedAtUtc,
            source.StartedAtUtc,
            source.CompletedAtUtc,
            pendingApprovals,
            pendingApprovals.Count,
            source.AutoApprovePendingToolCalls,
            source.Revision);
    }

    public static AgentExecutionRunDetailApiResponse ToExecutionRunDetail(ExecutionRunDetail source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new AgentExecutionRunDetailApiResponse(
            ToExecutionRun(source.Run),
            source.ChatSession is null
                ? null
                : ToChatSession(source.ChatSession),
            source.ExecutionLog.Select(ToExecutionLog).ToArray(),
            source.Metrics.Select(ToMetric).ToArray(),
            ToExecutionApprovals(source.Approvals),
            source.Artifacts.Select(ToArtifact).ToArray(),
            source.Checkpoints.Select(ToCheckpoint).ToArray(),
            ToToolReceipts(source.ToolReceipts),
            ToProviderUsageTotals(source.UsageObservations));
    }

    public static AgentChatRunApiResponse ToChatRunResult(AgentChatRunResult source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new AgentChatRunApiResponse(
            source.ChatSessionId,
            ToChatMessage(source.AssistantMessage),
            ToMetric(source.Metric),
            source.ExecutionRunId,
            source.State);
    }

    public static AgentExecutionRunResultApiResponse ToExecutionRunResult(ExecutionRunResult source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new AgentExecutionRunResultApiResponse(
            source.ExecutionRunId,
            source.ChatSessionId,
            source.ResponseText,
            source.AssistantMessage is null
                ? null
                : ToChatMessage(source.AssistantMessage),
            ToMetric(source.Metric),
            source.State,
            source.StructuredOutput is null
                ? null
                : ToStructuredOutput(source.StructuredOutput));
    }

    public static IReadOnlyList<AgentExecutionApprovalApiResponse> ToExecutionApprovals(
        IReadOnlyList<ExecutionApprovalRecord> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(static approval => new AgentExecutionApprovalApiResponse(
            approval.ApprovalId,
            approval.ExecutionRunId,
            approval.ToolName,
            approval.ToolKind,
            approval.Status,
            approval.RequestedAtUtc,
            approval.DecidedAtUtc,
            approval.DecisionSourceKind)).ToArray();
    }

    public static IReadOnlyList<AgentExecutionToolReceiptApiResponse> ToToolReceipts(
        IReadOnlyList<ToolExecutionReceiptRecord> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(static receipt => new AgentExecutionToolReceiptApiResponse(
            receipt.Id,
            receipt.ExecutionRunId,
            receipt.ToolFamily,
            receipt.ToolName,
            receipt.RiskClass,
            receipt.ApprovalMode,
            receipt.IsolationGuarantee,
            receipt.StartedAtUtc,
            receipt.CompletedAtUtc,
            receipt.RuntimeToolProviderName,
            receipt.DeclaredSideEffectMode)).ToArray();
    }

    public static IReadOnlyList<AgentExecutionLogApiResponse> ToExecutionLog(
        IReadOnlyList<ExecutionLogEntry> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(ToExecutionLog).ToArray();
    }

    public static IReadOnlyList<AgentRunMetricApiResponse> ToMetrics(
        IReadOnlyList<AgentRunMetric> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(ToMetric).ToArray();
    }

    public static AgentChatRuntimeApiResponse ToChatRuntime(ChatRuntimeSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new AgentChatRuntimeApiResponse(
            ToExecutionLog(source.ExecutionLog),
            ToMetrics(source.Metrics));
    }

    public static IReadOnlyList<AgentExecutionArtifactApiResponse> ToArtifacts(
        IReadOnlyList<ExecutionArtifactRecord> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(ToArtifact).ToArray();
    }

    public static IReadOnlyList<AgentExecutionCheckpointApiResponse> ToCheckpoints(
        IReadOnlyList<ExecutionWorkflowCheckpointRecord> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(ToCheckpoint).ToArray();
    }

    private static AgentCatalogItemApiResponse ToAgentCatalogItem(AgentDefinition source)
    {
        return new AgentCatalogItemApiResponse(
            source.Id,
            source.Name,
            source.RoleTitle,
            source.Summary,
            source.Status,
            source.Model,
            source.IsTemplate,
            source.AvatarImageUrl);
    }

    private static AgentChatMessageApiResponse ToChatMessage(ChatMessageRecord source)
    {
        return new AgentChatMessageApiResponse(
            source.Id,
            source.Role,
            source.Content,
            source.CreatedAtUtc);
    }

    private static AgentChatSessionSummaryApiResponse ToChatSessionSummary(ChatSessionSummaryRecord source)
    {
        return new AgentChatSessionSummaryApiResponse(
            source.Id,
            source.AgentId,
            source.Title,
            source.CreatedAtUtc,
            source.UpdatedAtUtc,
            source.MessageCount,
            source.LastMessagePreview,
            source.PendingApprovalCount,
            source.AutoApprovePendingToolCalls);
    }

    private static AgentChatRunSummaryApiResponse ToChatRunSummary(ChatRunSummaryRecord source)
    {
        return new AgentChatRunSummaryApiResponse(
            source.ExecutionRunId,
            source.AgentId,
            source.ChatSessionId,
            source.Title,
            source.State,
            source.Phase,
            source.Message,
            source.Outcome,
            source.CreatedAtUtc,
            source.UpdatedAtUtc,
            source.StartedAtUtc,
            source.CompletedAtUtc,
            source.Duration,
            source.KnownCostUsd,
            source.HasUnknownCost);
    }

    private static AgentExecutionLogApiResponse ToExecutionLog(ExecutionLogEntry source)
    {
        return new AgentExecutionLogApiResponse(
            source.Id,
            source.CreatedAtUtc,
            source.State,
            source.Phase,
            source.Message);
    }

    private static AgentRunMetricApiResponse ToMetric(AgentRunMetric source)
    {
        return new AgentRunMetricApiResponse(
            source.Id,
            source.CreatedAtUtc,
            source.Outcome,
            source.ProviderName,
            source.Model,
            source.DurationMs,
            source.InputTokens,
            source.CachedInputTokens,
            source.CacheWriteTokens,
            source.OutputTokens,
            source.ToolCalls,
            source.CostUsd);
    }

    private static AgentExecutionArtifactApiResponse ToArtifact(ExecutionArtifactRecord source)
    {
        return new AgentExecutionArtifactApiResponse(
            source.Id,
            source.ArtifactKind,
            source.DisplayName,
            source.RelativePath,
            source.ContentType,
            source.ProducedBy,
            source.Summary,
            source.CreatedAtUtc);
    }

    private static AgentExecutionCheckpointApiResponse ToCheckpoint(
        ExecutionWorkflowCheckpointRecord source)
    {
        return new AgentExecutionCheckpointApiResponse(
            source.Id,
            source.CheckpointKind,
            source.RunState,
            source.PendingApprovalIds.Count,
            source.CapturedAtUtc,
            source.ResumedAtUtc);
    }

    private static IReadOnlyList<AgentPendingApprovalApiResponse> MapPendingApprovals(
        IReadOnlyList<PendingToolApprovalRecord> source)
    {
        return source.Select(static approval => new AgentPendingApprovalApiResponse(
            approval.ApprovalId,
            approval.ToolName,
            approval.ToolKind)).ToArray();
    }

    private static AgentProviderUsageTotalsApiResponse ToProviderUsageTotals(
        IReadOnlyList<ProviderUsageObservation> source)
    {
        var knownUsage = source
            .Where(static observation => observation.UsageStatus is
                ProviderUsageObservationStatus.Observed or
                ProviderUsageObservationStatus.ObservedFromMetric)
            .ToArray();
        var knownCosts = source
            .Select(static observation => observation.ProviderCostUsd ?? observation.CalculatedCostUsd)
            .Where(static cost => cost.HasValue)
            .Select(static cost => cost.GetValueOrDefault())
            .ToArray();

        return new AgentProviderUsageTotalsApiResponse(
            source.Count,
            knownUsage.Length,
            source.Count - knownUsage.Length,
            knownUsage.Sum(static observation => observation.InputTokens),
            knownUsage.Sum(static observation => observation.CachedInputTokens),
            knownUsage.Sum(static observation => observation.CacheWriteTokens),
            knownUsage.Sum(static observation => observation.OutputTokens),
            knownUsage.Sum(static observation => observation.ReasoningTokens),
            knownUsage.Sum(static observation => observation.TotalTokens),
            knownUsage.Sum(static observation => observation.ToolCallCount),
            knownCosts.Length,
            source.Count - knownCosts.Length,
            decimal.Round(knownCosts.Sum(), 6, MidpointRounding.AwayFromZero));
    }

    private static AgentStructuredOutputApiResponse ToStructuredOutput(
        AgentJsonSchemaOutputResult source)
    {
        const int maximumValidationErrors = 20;
        return new AgentStructuredOutputApiResponse(
            source.Data,
            source.ValidationStatus,
            source.ValidationErrors
                .Take(maximumValidationErrors)
                .Select(static error => new AgentStructuredOutputValidationErrorApiResponse(
                    NormalizeBounded(error.Code, 128),
                    NormalizeBounded(error.Message, 1024),
                    NormalizeBounded(error.Path, 512)))
                .ToArray());
    }

    private static string NormalizeBounded(string value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength];
    }
}
