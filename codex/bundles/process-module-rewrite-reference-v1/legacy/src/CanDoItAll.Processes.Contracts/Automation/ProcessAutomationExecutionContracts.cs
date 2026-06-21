namespace CanDoItAll.Processes.Contracts;

public enum ProcessAutomationFinalizerMode
{
    Required
}

public enum ProcessAutomationStructuredOutputKind
{
    None,
    ProcessStepOutcomeResult
}

public enum ProcessAutomationExecutionState
{
    Idle,
    Preparing,
    Running,
    WaitingOnTool,
    Persisting,
    Completed,
    Failed
}

public enum ProcessAutomationRunOutcome
{
    Succeeded,
    Failed,
    Cancelled
}

public enum ProcessAutomationChatMessageRole
{
    System,
    User,
    Assistant
}

public enum ProcessAutomationProviderUsageStatus
{
    Observed = 0,
    MissingAfterProviderActivity = 1,
    UsageUnavailable = 2,
    EstimatedFromMetric = 3,
    ObservedFromMetric = 4
}

public sealed record ProcessAutomationExecutionRequest(
    Guid AgentId,
    string Prompt,
    ProcessAutomationInvocationSource Source,
    ProcessAutomationInvocationPolicy Policy,
    bool AutoApprovePendingToolCalls,
    ProcessAutomationStructuredOutputKind StructuredOutputKind);

public sealed record ProcessAutomationInvocationSource(
    string SourceKind,
    string SourceId,
    string CorrelationId,
    string CausationId,
    string RequestedBy,
    string RequestedByKind,
    string MetadataJson,
    string ProcessRunId,
    string ProcessStepId,
    string SchedulerRunId = "",
    string MessageId = "");

public sealed record ProcessAutomationInvocationPolicy(
    ProcessAutomationFinalizerMode? FinalizerMode,
    int? MaxStructuredOutputRepairAttempts,
    bool RequireStructuredOutputValidation);

public sealed record ProcessAutomationExecutionRunQuery(
    Guid? AgentId = null,
    Guid? ChatSessionId = null,
    string? CorrelationId = null,
    string? SourceKind = null,
    string? SourceId = null,
    int Take = 50,
    string? ProcessRunId = null,
    string? ProcessStepId = null,
    string? SchedulerRunId = null,
    string? MessageId = null,
    ProcessAutomationExecutionState? State = null,
    ProcessAutomationRunOutcome? Outcome = null,
    DateTimeOffset? CreatedFromUtc = null,
    DateTimeOffset? CreatedToUtc = null,
    DateTimeOffset? UpdatedFromUtc = null,
    DateTimeOffset? UpdatedToUtc = null);

public sealed record ProcessAutomationPendingToolApproval(
    string ApprovalId,
    string CallId,
    string ToolName,
    string ToolKind,
    string Details,
    string ArgumentsJson);

public sealed record ProcessAutomationChatMessage(
    Guid Id,
    ProcessAutomationChatMessageRole Role,
    string Content,
    DateTimeOffset CreatedAtUtc,
    int TokenEstimate);

public sealed record ProcessAutomationChatSession(
    Guid Id,
    Guid AgentId,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ProcessAutomationChatMessage> Messages,
    Guid? LatestExecutionRunId);

public sealed record ProcessAutomationExecutionRunRecord(
    Guid Id,
    Guid AgentId,
    Guid? ChatSessionId,
    string Title,
    string SourceKind,
    string SourceId,
    string CorrelationId,
    string CausationId,
    string RequestedBy,
    string RequestedByKind,
    string MetadataJson,
    string InputSummary,
    string ResultSummary,
    string ProviderName,
    string Model,
    ProcessAutomationExecutionState State,
    ProcessAutomationRunOutcome? Outcome,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string RuntimeSessionKey,
    string? SerializedSessionStateJson,
    IReadOnlyList<ProcessAutomationPendingToolApproval> PendingApprovals,
    bool AutoApprovePendingToolCalls = false,
    string ProcessRunId = "",
    string ProcessStepId = "",
    string SchedulerRunId = "",
    string MessageId = "",
    long Revision = 1,
    string StructuredOutputContractKey = "",
    string StructuredOutputTypeName = "",
    string StructuredOutputSchemaName = "",
    string StructuredOutputSchemaDescription = "");

public sealed record ProcessAutomationExecutionLogEntry(
    Guid Id,
    Guid AgentId,
    Guid? ChatSessionId,
    DateTimeOffset CreatedAtUtc,
    ProcessAutomationExecutionState State,
    string Phase,
    string Message)
{
    public Guid ExecutionRunId { get; init; }
}

public sealed record ProcessAutomationRunMetric(
    Guid Id,
    Guid AgentId,
    Guid? ChatSessionId,
    DateTimeOffset CreatedAtUtc,
    ProcessAutomationRunOutcome Outcome,
    string ProviderName,
    string Model,
    long DurationMs,
    int InputTokens,
    int OutputTokens,
    int ToolCalls)
{
    public Guid ExecutionRunId { get; init; }

    public int CachedInputTokens { get; init; }

    public decimal CostUsd { get; init; }
}

public sealed record ProcessAutomationExecutionArtifact(
    Guid Id,
    Guid ExecutionRunId,
    string ArtifactKind,
    string DisplayName,
    string RelativePath,
    string ContentType,
    string ProducedBy,
    string Summary,
    DateTimeOffset CreatedAtUtc);

public sealed record ProcessAutomationToolExecutionReceipt(
    Guid Id,
    Guid ExecutionRunId,
    string ToolFamily,
    string ToolName,
    string RiskClass,
    string ApprovalMode,
    string IsolationGuarantee,
    string RequestSummary,
    string WorkingDirectory,
    string ExitSummary,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc)
{
    public string RuntimeToolProviderKey { get; init; } = string.Empty;

    public string RuntimeToolProviderName { get; init; } = string.Empty;
}

public sealed record ProcessAutomationProviderUsageObservation(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    string ProviderName,
    string ProviderKind,
    string Model,
    string TransportKind,
    string SourcePhase,
    ProcessAutomationProviderUsageStatus UsageStatus,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    int ToolCallCount)
{
    public Guid? ExecutionRunId { get; init; }

    public Guid? AgentId { get; init; }

    public Guid? ChatSessionId { get; init; }

    public string ProviderResponseId { get; init; } = string.Empty;

    public string ProviderRequestId { get; init; } = string.Empty;

    public string RuntimeSessionKey { get; init; } = string.Empty;

    public string ProcessRunId { get; init; } = string.Empty;

    public string ProcessStepId { get; init; } = string.Empty;

    public string WorkflowRunId { get; init; } = string.Empty;

    public string WorkflowNodeId { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    public decimal? ProviderCostUsd { get; init; }

    public decimal? CalculatedCostUsd { get; init; }

    public string PricingProfileHash { get; init; } = string.Empty;

    public string PricingVersion { get; init; } = string.Empty;

    public string RawUsageJson { get; init; } = string.Empty;

    public string DiagnosticsJson { get; init; } = string.Empty;
}

public sealed record ProcessAutomationExecutionRunDetail(
    ProcessAutomationExecutionRunRecord Run,
    ProcessAutomationChatSession? ChatSession,
    IReadOnlyList<ProcessAutomationExecutionLogEntry> ExecutionLog,
    IReadOnlyList<ProcessAutomationRunMetric> Metrics)
{
    public IReadOnlyList<ProcessAutomationExecutionArtifact> Artifacts { get; init; } = [];

    public IReadOnlyList<ProcessAutomationToolExecutionReceipt> ToolReceipts { get; init; } = [];

    public IReadOnlyList<ProcessAutomationProviderUsageObservation> UsageObservations { get; init; } = [];
}

public sealed record ProcessAutomationExecutionRunResult(
    Guid ExecutionRunId,
    Guid? ChatSessionId,
    string ResponseText,
    ProcessAutomationRunMetric? Metric);

public sealed class ProcessAutomationExecutionFailedException : InvalidOperationException
{
    public ProcessAutomationExecutionFailedException(
        Guid agentId,
        Guid executionRunId,
        Guid? chatSessionId,
        string providerName,
        string modelName,
        string failureKind,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        AgentId = agentId;
        ExecutionRunId = executionRunId;
        ChatSessionId = chatSessionId;
        ProviderName = providerName;
        ModelName = modelName;
        FailureKind = failureKind;
    }

    public Guid AgentId { get; }

    public Guid ExecutionRunId { get; }

    public Guid? ChatSessionId { get; }

    public string ProviderName { get; }

    public string ModelName { get; }

    public string FailureKind { get; }
}
