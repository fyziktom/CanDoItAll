using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public sealed record ChatMessageRecord(
    Guid Id,
    ChatMessageRole Role,
    string Content,
    DateTimeOffset CreatedAtUtc,
    int TokenEstimate);

public sealed record PendingToolApprovalRecord(
    string ApprovalId,
    string CallId,
    string ToolName,
    string ToolKind,
    string Details,
    string ArgumentsJson);

public sealed record PendingToolApprovalDecision(
    string ApprovalId,
    bool Approved);

public sealed record ChatSessionRuntimeCompatibilityRecord
{
    [JsonConstructor]
    public ChatSessionRuntimeCompatibilityRecord(
        string? runtimeSessionKey,
        string? serializedSessionStateJson,
        IReadOnlyList<PendingToolApprovalRecord>? pendingApprovals,
        bool autoApprovePendingToolCalls = false)
    {
        RuntimeSessionKey = runtimeSessionKey ?? string.Empty;
        SerializedSessionStateJson = string.IsNullOrWhiteSpace(serializedSessionStateJson)
            ? null
            : serializedSessionStateJson;
        PendingApprovals = pendingApprovals ?? [];
        AutoApprovePendingToolCalls = autoApprovePendingToolCalls;
    }

    public string RuntimeSessionKey { get; init; }

    public string? SerializedSessionStateJson { get; init; }

    public IReadOnlyList<PendingToolApprovalRecord> PendingApprovals { get; init; }

    public bool AutoApprovePendingToolCalls { get; init; }

    public bool HasState =>
        !string.IsNullOrWhiteSpace(RuntimeSessionKey)
        || !string.IsNullOrWhiteSpace(SerializedSessionStateJson)
        || PendingApprovals.Count > 0
        || AutoApprovePendingToolCalls;

    public static ChatSessionRuntimeCompatibilityRecord? Create(
        string? runtimeSessionKey,
        string? serializedSessionStateJson,
        IReadOnlyList<PendingToolApprovalRecord>? pendingApprovals,
        bool autoApprovePendingToolCalls = false)
    {
        var compatibility = new ChatSessionRuntimeCompatibilityRecord(
            runtimeSessionKey,
            serializedSessionStateJson,
            pendingApprovals,
            autoApprovePendingToolCalls);

        return compatibility.HasState
            ? compatibility
            : null;
    }
}

public sealed record ChatSessionRecord
{
    [JsonConstructor]
    public ChatSessionRecord()
    {
        Title = string.Empty;
        Messages = [];
    }

    public ChatSessionRecord(
        Guid Id,
        Guid AgentId,
        string Title,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        IReadOnlyList<ChatMessageRecord>? Messages,
        Guid? LatestExecutionRunId = null,
        ChatSessionRuntimeCompatibilityRecord? Compatibility = null)
    {
        this.Id = Id;
        this.AgentId = AgentId;
        this.Title = Title;
        this.CreatedAtUtc = CreatedAtUtc;
        this.UpdatedAtUtc = UpdatedAtUtc;
        this.Messages = Messages ?? [];
        this.LatestExecutionRunId = LatestExecutionRunId;
        this.Compatibility = Compatibility;
    }

    public ChatSessionRecord(
        Guid Id,
        Guid AgentId,
        string Title,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        string? RuntimeSessionKey,
        string? SerializedSessionStateJson,
        IReadOnlyList<ChatMessageRecord>? Messages,
        IReadOnlyList<PendingToolApprovalRecord>? PendingApprovals,
        bool AutoApprovePendingToolCalls = false,
        Guid? LatestExecutionRunId = null,
        ChatSessionRuntimeCompatibilityRecord? Compatibility = null)
    {
        this.Id = Id;
        this.AgentId = AgentId;
        this.Title = Title;
        this.CreatedAtUtc = CreatedAtUtc;
        this.UpdatedAtUtc = UpdatedAtUtc;
        this.Messages = Messages ?? [];
        this.LatestExecutionRunId = LatestExecutionRunId;
        this.Compatibility = Compatibility
            ?? ChatSessionRuntimeCompatibilityRecord.Create(
                RuntimeSessionKey,
                SerializedSessionStateJson,
                PendingApprovals,
                AutoApprovePendingToolCalls);
    }

    public Guid Id { get; init; }

    public Guid AgentId { get; init; }

    public string Title { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }

    public IReadOnlyList<ChatMessageRecord> Messages { get; init; }

    public Guid? LatestExecutionRunId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChatSessionRuntimeCompatibilityRecord? Compatibility { get; init; }

    [JsonPropertyName(nameof(ChatSessionRuntimeCompatibilityRecord.RuntimeSessionKey))]
    public string? LegacyRuntimeSessionKey
    {
        init => Compatibility = MergeCompatibility(runtimeSessionKey: value);
    }

    [JsonPropertyName(nameof(ChatSessionRuntimeCompatibilityRecord.SerializedSessionStateJson))]
    public string? LegacySerializedSessionStateJson
    {
        init => Compatibility = MergeCompatibility(serializedSessionStateJson: value);
    }

    [JsonPropertyName(nameof(ChatSessionRuntimeCompatibilityRecord.PendingApprovals))]
    public IReadOnlyList<PendingToolApprovalRecord>? LegacyPendingApprovals
    {
        init => Compatibility = MergeCompatibility(pendingApprovals: value);
    }

    [JsonPropertyName(nameof(ChatSessionRuntimeCompatibilityRecord.AutoApprovePendingToolCalls))]
    public bool LegacyAutoApprovePendingToolCalls
    {
        init => Compatibility = MergeCompatibility(autoApprovePendingToolCalls: value);
    }

    private ChatSessionRuntimeCompatibilityRecord? MergeCompatibility(
        string? runtimeSessionKey = null,
        string? serializedSessionStateJson = null,
        IReadOnlyList<PendingToolApprovalRecord>? pendingApprovals = null,
        bool? autoApprovePendingToolCalls = null)
    {
        return ChatSessionRuntimeCompatibilityRecord.Create(
            runtimeSessionKey ?? Compatibility?.RuntimeSessionKey,
            serializedSessionStateJson ?? Compatibility?.SerializedSessionStateJson,
            pendingApprovals ?? Compatibility?.PendingApprovals,
            autoApprovePendingToolCalls ?? Compatibility?.AutoApprovePendingToolCalls ?? false);
    }
}

public sealed record ChatSessionSummaryRecord(
    Guid Id,
    Guid AgentId,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int MessageCount,
    string LastMessagePreview,
    int PendingApprovalCount,
    bool AutoApprovePendingToolCalls);

public sealed record ChatRunSummaryRecord(
    Guid ExecutionRunId,
    Guid AgentId,
    Guid? ChatSessionId,
    DateTimeOffset UpdatedAtUtc,
    ExecutionState State,
    string Phase,
    string Message,
    RunOutcome? Outcome);

public sealed record ChatAgentWorkspaceSnapshot(
    Guid AgentId,
    IReadOnlyList<ChatSessionSummaryRecord> Sessions,
    ChatSessionRecord? SelectedSession,
    Guid? SelectedSessionId,
    ChatRunSummaryRecord? LatestRun)
{
    public ExecutionRunRecord? SelectedRun { get; init; }
}

public sealed record ChatPageBootstrapSnapshot(
    IReadOnlyList<AgentDefinition> Agents,
    Guid? InitialAgentId,
    ChatAgentWorkspaceSnapshot? SelectedAgentWorkspace);

public sealed record ChatRuntimeSnapshot(
    IReadOnlyList<ExecutionLogEntry> ExecutionLog,
    IReadOnlyList<AgentRunMetric> Metrics);

public sealed record ExecutionLogEntry(
    Guid Id,
    Guid AgentId,
    Guid? ChatSessionId,
    DateTimeOffset CreatedAtUtc,
    ExecutionState State,
    string Phase,
    string Message)
{
    public Guid ExecutionRunId { get; init; }
}

public sealed record AgentRunMetric(
    Guid Id,
    Guid AgentId,
    Guid? ChatSessionId,
    DateTimeOffset CreatedAtUtc,
    RunOutcome Outcome,
    string ProviderName,
    string Model,
    long DurationMs,
    int InputTokens,
    int OutputTokens,
    int ToolCalls)
{
    public Guid ExecutionRunId { get; init; }

    public int CachedInputTokens { get; init; }

    public int CacheWriteTokens { get; init; }

    public decimal CostUsd { get; init; }
}

public sealed record ExecutionArtifactRecord(
    Guid Id,
    Guid ExecutionRunId,
    string ArtifactKind,
    string DisplayName,
    string RelativePath,
    string ContentType,
    string ProducedBy,
    string Summary,
    DateTimeOffset CreatedAtUtc);

public enum ToolExecutionSideEffectMode
{
    Unspecified = 0,
    NoMutation,
    ManagedProcessArtifacts,
    ExternalArtifactDestination,
    ProductMutation
}

public sealed record ToolExecutionReceiptRecord(
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

    public ToolExecutionSideEffectMode DeclaredSideEffectMode { get; init; }
}

public sealed record ExecutionWorkflowCheckpointRecord(
    Guid Id,
    Guid ExecutionRunId,
    string WorkflowSessionId,
    string WorkflowCheckpointId,
    string CheckpointKind,
    ExecutionState RunState,
    IReadOnlyList<string> PendingApprovalIds,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset? ResumedAtUtc,
    string CorrelationId,
    string SourceKind,
    string SourceId,
    string ProcessRunId,
    string ProcessStepId,
    string SchedulerRunId,
    string MessageId,
    string TraceId,
    string SpanId);

public sealed record ExecutionInvocationContext(
    string SourceKind,
    string SourceId,
    string CorrelationId,
    string CausationId,
    string RequestedBy,
    string RequestedByKind,
    string MetadataJson,
    string ProcessRunId = "",
    string ProcessStepId = "",
    string SchedulerRunId = "",
    string MessageId = "",
    ExecutionInvocationPolicy? Policy = null)
{
    public static ExecutionInvocationContext Empty { get; } = new(
        SourceKind: "manual",
        SourceId: string.Empty,
        CorrelationId: string.Empty,
        CausationId: string.Empty,
        RequestedBy: string.Empty,
        RequestedByKind: string.Empty,
        MetadataJson: "{}",
        ProcessRunId: string.Empty,
        ProcessStepId: string.Empty,
        SchedulerRunId: string.Empty,
        MessageId: string.Empty,
        Policy: null);
}

public sealed record ExecutionInvocationPolicy(
    AgentFinalizerMode? FinalizerMode = null,
    int? MaxStructuredOutputRepairAttempts = null,
    bool RequireStructuredOutputValidation = true,
    bool AllowRequiredFinalizerStructuredOutputRecovery = false);

public sealed record AgentRuntimeInputAttachment(
    string Name,
    string ContentType,
    ImmutableArray<byte> Bytes,
    string SourcePath);

public sealed record AgentRuntimeExecutionOptions(
    AgentStructuredOutputContract? StructuredOutput,
    AgentFinalizerMode FinalizerMode,
    bool RequireStructuredOutputValidation,
    int MaxStructuredOutputRepairAttempts,
    AgentRuntimeHandoffExecutionOptions? Handoff = null,
    WorkspaceScopeDescriptor? ContextWorkspaceScope = null,
    bool RequireJsonResponseFormat = false,
    string ResponseFormatJsonSchema = "",
    string ResponseFormatSchemaName = "",
    string ResponseFormatSchemaDescription = "",
    AgentRuntimeContextIntent? ContextIntent = null,
    IReadOnlyList<AgentRuntimeInputAttachment>? InputAttachments = null)
{
    [JsonIgnore]
    public AgentExecutionOperationId? ActivityOperationId { get; init; }

    [JsonIgnore]
    public AgentRuntimeTransientContext? TransientContext { get; init; }
}

public sealed record AgentRuntimeTransientContext
{
    public AgentRuntimeTransientContext(
        string content,
        WorkspaceScopeDescriptor? workspaceScope = null,
        IReadOnlyList<AgentChatContextAttachmentEnvelope>? attachments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        var normalizedContent = content.Trim();
        if (normalizedContent.Length > AgentChatContextLimits.MaximumTransientContextLength)
        {
            throw new ArgumentException(
                $"Transient agent context cannot exceed {AgentChatContextLimits.MaximumTransientContextLength} characters.",
                nameof(content));
        }

        Content = normalizedContent;
        WorkspaceScope = workspaceScope;
        Attachments = attachments?.ToImmutableArray() ?? [];
    }

    public string Content { get; }

    public WorkspaceScopeDescriptor? WorkspaceScope { get; }

    [JsonIgnore]
    public ImmutableArray<AgentChatContextAttachmentEnvelope> Attachments { get; }

    public ImmutableArray<AgentChatContextAttachmentEnvelope>
        GetAttachments<TAttachment>()
        where TAttachment : class, IAgentChatContextAttachment
    {
        return Attachments
            .Where(static attachment =>
                attachment.AttachmentType == typeof(TAttachment))
            .ToImmutableArray();
    }
}

public sealed record AgentRuntimeHandoffExecutionOptions(
    AgentHandoffSettings Settings,
    IReadOnlyList<AgentRuntimeHandoffParticipant> Participants,
    Guid EntryAgentId,
    string CorrelationId);

public sealed record AgentRuntimeHandoffParticipant(
    AgentDefinition Agent,
    ProviderProfile Provider,
    IReadOnlyList<CapabilityCatalogItem> Capabilities,
    IReadOnlyList<AgentMemoryRecord> Memory);

public sealed record ExecutionRunRecord(
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
    ExecutionState State,
    RunOutcome? Outcome,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string RuntimeSessionKey,
    string? SerializedSessionStateJson,
    IReadOnlyList<PendingToolApprovalRecord> PendingApprovals,
    bool AutoApprovePendingToolCalls = false,
    string ProcessRunId = "",
    string ProcessStepId = "",
    string SchedulerRunId = "",
    string MessageId = "",
    long Revision = 1,
    string StructuredOutputContractKey = "",
    string StructuredOutputTypeName = "",
    string StructuredOutputSchemaName = "",
    string StructuredOutputSchemaDescription = "",
    Guid? ProviderProfileId = null,
    string StructuredOutputJsonSchema = "",
    string StructuredOutputSchemaHash = "",
    string StructuredOutputSchemaVersion = "",
    bool StructuredOutputSchemaStrict = false,
    string StructuredOutputRawOutput = "",
    string StructuredOutputValidationStatus = "",
    string StructuredOutputValidationErrorsJson = "[]")
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AgentExecutionOperationId? InitialActivityOperationId { get; init; }
}

public sealed record ExecutionApprovalRecord(
    string ApprovalId,
    Guid ExecutionRunId,
    string CallId,
    string ToolName,
    string ToolKind,
    string Details,
    string ArgumentsJson,
    ExecutionApprovalStatus Status,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    string DecisionSourceKind,
    string DecisionSourceId,
    string DecisionNotes);

public sealed record AgentChatRunOptions(
    AgentExecutionOperationId InitialActivityOperationId,
    bool RuntimeToolProvidersEnabled = true,
    bool WorkspaceToolsEnabled = true)
{
    public ExecutionInvocationContext? Context { get; init; }

    public bool ToolCapabilitiesEnabled { get; init; } = true;

    [JsonIgnore]
    public AgentRuntimeTransientContext? TransientContext { get; init; }
}

public sealed record ExecutionRunRequest(
    Guid AgentId,
    string Prompt,
    AgentExecutionOperationId InitialActivityOperationId,
    Guid? ChatSessionId = null,
    ExecutionInvocationContext? Context = null,
    bool AutoApprovePendingToolCalls = false,
    AgentStructuredOutputContract? StructuredOutput = null,
    IReadOnlyList<string>? InputAttachmentPaths = null,
    AgentJsonSchemaOutputContract? JsonSchemaOutput = null)
{
    [JsonIgnore]
    public AgentRuntimeTransientContext? TransientContext { get; init; }
}

public sealed record ExecutionRunQuery(
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
    ExecutionState? State = null,
    RunOutcome? Outcome = null,
    ExecutionApprovalStatus? ApprovalStatus = null,
    DateTimeOffset? CreatedFromUtc = null,
    DateTimeOffset? CreatedToUtc = null,
    DateTimeOffset? UpdatedFromUtc = null,
    DateTimeOffset? UpdatedToUtc = null)
{
    public IReadOnlyList<string> ProcessRunIds { get; init; } = [];

    public IReadOnlyList<string> ProcessStepIds { get; init; } = [];
}

public sealed record ExecutionRunDetail(
    ExecutionRunRecord Run,
    ChatSessionRecord? ChatSession,
    IReadOnlyList<ExecutionLogEntry> ExecutionLog,
    IReadOnlyList<AgentRunMetric> Metrics)
{
    public IReadOnlyList<ExecutionApprovalRecord> Approvals { get; init; } = [];
    public IReadOnlyList<ExecutionArtifactRecord> Artifacts { get; init; } = [];
    public IReadOnlyList<ExecutionWorkflowCheckpointRecord> Checkpoints { get; init; } = [];
    public IReadOnlyList<ToolExecutionReceiptRecord> ToolReceipts { get; init; } = [];
    public IReadOnlyList<ProviderUsageObservation> UsageObservations { get; init; } = [];
}

public sealed record ExecutionRunResult(
    Guid ExecutionRunId,
    Guid? ChatSessionId,
    string ResponseText,
    ChatMessageRecord? AssistantMessage,
    AgentRunMetric Metric)
{
    public ExecutionState State { get; init; }

    public AgentJsonSchemaOutputResult? StructuredOutput { get; init; }

    [JsonIgnore]
    public AgentChatExecutionCompleted? ContextCompletionNotification { get; init; }
}

public sealed record ExecutionEvent(
    Guid EventId,
    Guid ExecutionRunId,
    Guid AgentId,
    Guid? ChatSessionId,
    string SourceKind,
    string SourceId,
    string CorrelationId,
    string CausationId,
    string RequestedBy,
    string RequestedByKind,
    string MetadataJson,
    ExecutionState State,
    string Phase,
    string Message,
    DateTimeOffset OccurredAtUtc,
    RunOutcome? Outcome = null,
    string ProcessRunId = "",
    string ProcessStepId = "",
    string SchedulerRunId = "",
    string MessageId = "",
    string TraceId = "",
    string SpanId = "");
