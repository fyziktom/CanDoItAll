using System.Text.Json.Serialization;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.AgentFramework.Memory;

public static class MemoryAgentRuntimeToolNames
{
    public const string ContextQuery = "memory_context_query";
    public const string OperationStatus = "memory_operation_status";
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

public sealed record MemoryOperationStatusToolInput(Guid OperationId);

public sealed record MemoryToolTrustBoundaryResult(string Instruction, string DataMarker);

public sealed record MemoryToolWarningResult(string Kind, string Message);

public sealed record MemoryToolCitationResult(string SourceRef, string Label);

public sealed record MemoryContextSectionToolResult(
    string Title,
    string Text,
    IReadOnlyList<MemoryToolCitationResult> Citations,
    decimal Confidence);

public sealed record MemoryContextPackToolResult(
    MemoryToolTrustBoundaryResult TrustBoundary,
    Guid ContextPackId,
    string Summary,
    IReadOnlyList<MemoryContextSectionToolResult> Sections,
    IReadOnlyList<MemoryToolWarningResult> Warnings,
    decimal Confidence,
    MemoryFeedbackHandleToolResult? FeedbackHandle);

public sealed record MemoryFeedbackHandleToolResult(string Value);

public sealed record MemoryAsyncOperationToolResult(
    Guid OperationId,
    string StatusPath,
    DateTimeOffset ExpiresAtUtc,
    TimeSpan PollAfter,
    bool CallbackAvailable);

public sealed record MemoryFinalOperationToolResult(
    MemoryToolTrustBoundaryResult TrustBoundary,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryOperationStatus>))]
    MemoryOperationStatus Status,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryPayloadKind>))]
    MemoryPayloadKind? OutputKind,
    string? OutputText,
    MemoryContextPackToolResult? ContextPack,
    bool OutputIsReadable,
    IReadOnlyList<MemoryToolWarningResult> Warnings,
    IReadOnlyList<MemoryFeedbackHandleToolResult> FeedbackHandles,
    IReadOnlyList<string> SourceRefs);

public sealed record MemoryContextQueryToolResult(
    MemoryToolTrustBoundaryResult TrustBoundary,
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

public sealed record MemoryOperationStatusToolResult(
    MemoryToolTrustBoundaryResult TrustBoundary,
    MemoryToolResultStatus Status,
    bool Success,
    string Diagnostic,
    string? ProviderInstanceId,
    Guid? OperationId,
    string? OperationStatus,
    string? StatusReason,
    MemoryFeedbackHandleToolResult? FeedbackHandle,
    MemoryFinalOperationToolResult? FinalResult,
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
            MemoryOperationHandlerStatus.ProviderDenied => MemoryToolResultStatus.ProviderDenied,
            MemoryOperationHandlerStatus.AccessDenied => MemoryToolResultStatus.ProviderDenied,
            MemoryOperationHandlerStatus.ProviderSelectionRequired => MemoryToolResultStatus.NoProviderConfigured,
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
