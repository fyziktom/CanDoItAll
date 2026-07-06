using System.Text.Json.Serialization;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Modules.AgentFramework;

public static class MemoryAgentRuntimeToolNames
{
    public const string ContextQuery = "memory_context_query";
    public const string IngestText = "memory_ingest_text";
    public const string FeedbackSubmit = "memory_feedback_submit";
    public const string OperationStatus = "memory_operation_status";
    public const string OperationCancel = "memory_operation_cancel";
    public const string EventAcknowledge = "memory_event_acknowledge";
}

public static class MemoryAgentRuntimeToolTags
{
    public const string WorkflowId = "memory.workflowId";
    public const string WorkflowNodeId = "memory.workflowNodeId";
    public const string ProcessId = "memory.processId";
    public const string ProcessStepId = "memory.processStepId";
}

[JsonConverter(typeof(JsonStringEnumConverter<MemoryToolResultStatus>))]
public enum MemoryToolResultStatus
{
    Completed = 0,
    Accepted = 1,
    NoProviderConfigured = 2,
    NoEnabledProvider = 3,
    ProviderNotFound = 4,
    ProviderDisabled = 5,
    ProviderDenied = 6,
    CapabilityUnavailable = 7,
    CapabilityDenied = 8,
    CapabilityMismatch = 9,
    DriverUnavailable = 10,
    SourceCaptureFailed = 11,
    SourceScopeDenied = 12,
    NotFound = 13,
    Cancelled = 14,
    Failed = 15,
    TimedOut = 16,
    UnsupportedOperation = 17,
    InvalidRequest = 18,
    ToolDisabled = 19
}

public sealed record MemoryContextQueryToolInput(
    string Query,
    string? ProviderInstanceId = null,
    IReadOnlyList<string>? SourceSnapshotIds = null,
    bool AllowAsync = false);

public sealed record MemoryIngestTextToolInput(
    string Title,
    string ContentText,
    string SourceCategory,
    string? ProviderInstanceId = null,
    IReadOnlyList<string>? Tags = null);

public sealed record MemoryFeedbackSubmitToolInput(
    string ContextPackId,
    MemoryFeedbackOutcome Outcome,
    string? Comment = null,
    string? ProviderInstanceId = null,
    string? Currency = null,
    decimal? Amount = null);

public sealed record MemoryOperationStatusToolInput(
    Guid OperationId);

public sealed record MemoryOperationCancelToolInput(
    Guid OperationId,
    string Reason);

public sealed record MemoryEventAcknowledgeToolInput(
    Guid EventId,
    bool Accepted,
    string Reason,
    string? ProviderInstanceId = null);

public sealed record MemoryToolWarningResult(
    string Kind,
    string Message);

public sealed record MemoryToolCitationResult(
    string SourceRef,
    string Label);

public sealed record MemoryContextSectionToolResult(
    string Title,
    string Text,
    IReadOnlyList<MemoryToolCitationResult> Citations,
    decimal Confidence);

public sealed record MemoryFeedbackHandleToolResult(
    string Value);

public sealed record MemoryAsyncOperationToolResult(
    Guid OperationId,
    string StatusPath,
    DateTimeOffset ExpiresAtUtc,
    TimeSpan PollAfter,
    bool CallbackAvailable);

public sealed record MemoryContextQueryToolResult(
    MemoryToolResultStatus Status,
    bool Success,
    string Diagnostic,
    string? ProviderInstanceId,
    Guid? OperationId,
    string Summary,
    IReadOnlyList<MemoryContextSectionToolResult> Sections,
    IReadOnlyList<MemoryToolWarningResult> Warnings,
    decimal? Confidence,
    MemoryFeedbackHandleToolResult? FeedbackHandle,
    MemoryAsyncOperationToolResult? AsyncOperation,
    bool DispatchAttempted);

public sealed record MemoryIngestTextToolResult(
    MemoryToolResultStatus Status,
    bool Success,
    string Diagnostic,
    string? ProviderInstanceId,
    Guid? OperationId,
    Guid? JobId,
    string? CapturedSnapshotId,
    IReadOnlyList<string> PayloadForms,
    bool DispatchAttempted);

public sealed record MemoryFeedbackSubmitToolResult(
    MemoryToolResultStatus Status,
    bool Success,
    string Diagnostic,
    string? ProviderInstanceId,
    string? FeedbackRecordId,
    Guid? OperationId,
    bool DispatchAttempted);

public sealed record MemoryOperationStatusToolResult(
    MemoryToolResultStatus Status,
    bool Success,
    string Diagnostic,
    string? ProviderInstanceId,
    Guid? OperationId,
    string? OperationStatus,
    string? StatusReason,
    MemoryFeedbackHandleToolResult? FeedbackHandle,
    bool DispatchAttempted);

public sealed record MemoryOperationCancelToolResult(
    MemoryToolResultStatus Status,
    bool Success,
    string Diagnostic,
    string? ProviderInstanceId,
    Guid? OperationId,
    string? OperationStatus,
    bool DispatchAttempted);

public sealed record MemoryEventAcknowledgeToolResult(
    MemoryToolResultStatus Status,
    bool Success,
    string Diagnostic,
    string? ProviderInstanceId,
    string? OutboxRecordId,
    bool DispatchAttempted);

internal static class MemoryToolResultMapper
{
    public static MemoryToolResultStatus ToToolStatus(MemoryOperationHandlerStatus status)
    {
        return status switch
        {
            MemoryOperationHandlerStatus.Completed => MemoryToolResultStatus.Completed,
            MemoryOperationHandlerStatus.Accepted => MemoryToolResultStatus.Accepted,
            MemoryOperationHandlerStatus.NoProviderConfigured => MemoryToolResultStatus.NoProviderConfigured,
            MemoryOperationHandlerStatus.NoEnabledProvider => MemoryToolResultStatus.NoEnabledProvider,
            MemoryOperationHandlerStatus.ProviderNotFound => MemoryToolResultStatus.ProviderNotFound,
            MemoryOperationHandlerStatus.ProviderDisabled => MemoryToolResultStatus.ProviderDisabled,
            MemoryOperationHandlerStatus.CapabilityUnavailable => MemoryToolResultStatus.CapabilityUnavailable,
            MemoryOperationHandlerStatus.CapabilityDenied => MemoryToolResultStatus.CapabilityDenied,
            MemoryOperationHandlerStatus.CapabilityMismatch => MemoryToolResultStatus.CapabilityMismatch,
            MemoryOperationHandlerStatus.DriverUnavailable => MemoryToolResultStatus.DriverUnavailable,
            MemoryOperationHandlerStatus.SourceCaptureFailed => MemoryToolResultStatus.SourceCaptureFailed,
            MemoryOperationHandlerStatus.NotFound => MemoryToolResultStatus.NotFound,
            MemoryOperationHandlerStatus.Cancelled => MemoryToolResultStatus.Cancelled,
            MemoryOperationHandlerStatus.TimedOut => MemoryToolResultStatus.TimedOut,
            MemoryOperationHandlerStatus.UnsupportedOperation => MemoryToolResultStatus.UnsupportedOperation,
            _ => MemoryToolResultStatus.Failed
        };
    }

    public static bool IsSuccess(MemoryToolResultStatus status)
    {
        return status is MemoryToolResultStatus.Completed or
            MemoryToolResultStatus.Accepted or
            MemoryToolResultStatus.Cancelled;
    }
}
