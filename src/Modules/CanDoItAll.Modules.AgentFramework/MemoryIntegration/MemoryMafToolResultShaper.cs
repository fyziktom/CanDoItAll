using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Modules.AgentFramework;

public static class MemoryMafToolResultShaper
{
    public static MemoryContextQueryToolResult ToQueryResult(
        MemoryOperationHandlerResult<MemoryContextPack> result)
    {
        var status = MemoryToolResultMapper.ToToolStatus(result.Status);
        var output = result.Output;
        return new MemoryContextQueryToolResult(
            status,
            MemoryToolResultMapper.IsSuccess(status),
            result.Diagnostic,
            ResolveProviderInstanceId(result.Selection, result.OperationRecord),
            result.OperationRecord?.OperationId.Value ?? result.AcceptedOperation?.OperationId.Value,
            output?.Summary ?? string.Empty,
            output?.Sections.Select(ToSectionResult).ToArray() ?? [],
            output?.Warnings.Select(ToWarningResult).ToArray() ?? [],
            output?.ProviderConfidence,
            ToFeedbackHandleResult(result.FeedbackHandle ?? output?.FeedbackHandle),
            ToAsyncOperationResult(result.AcceptedOperation),
            result.DriverDispatchAttempted);
    }

    public static MemoryIngestTextToolResult ToIngestionResult(
        MemoryOperationHandlerResult<MemorySourceCaptureOperationResult> result)
    {
        var status = MemoryToolResultMapper.ToToolStatus(result.Status);
        return new MemoryIngestTextToolResult(
            status,
            MemoryToolResultMapper.IsSuccess(status),
            result.Diagnostic,
            ResolveProviderInstanceId(result.Selection, result.OperationRecord),
            result.OperationRecord?.OperationId.Value,
            result.Output?.JobRecord.JobId,
            result.Output?.JobRecord.CapturedSnapshotId?.Value,
            result.Output?.PayloadForms.Select(form => form.ToString()).ToArray() ?? [],
            result.DriverDispatchAttempted);
    }

    public static MemoryFeedbackSubmitToolResult ToFeedbackResult(
        MemoryOperationHandlerResult<MemoryFeedbackRecord> result)
    {
        var status = MemoryToolResultMapper.ToToolStatus(result.Status);
        return new MemoryFeedbackSubmitToolResult(
            status,
            MemoryToolResultMapper.IsSuccess(status),
            result.Diagnostic,
            ResolveProviderInstanceId(result.Selection, result.OperationRecord) ?? result.Output?.ProviderInstanceId.Value,
            result.Output?.FeedbackRecordId.ToString(),
            result.Output?.OperationId?.Value,
            result.DriverDispatchAttempted);
    }

    public static MemoryOperationStatusToolResult ToStatusResult(
        MemoryOperationHandlerResult<MemoryOperationRecord> result)
    {
        var status = MemoryToolResultMapper.ToToolStatus(result.Status);
        return new MemoryOperationStatusToolResult(
            status,
            MemoryToolResultMapper.IsSuccess(status),
            result.Diagnostic,
            ResolveProviderInstanceId(result.Selection, result.OperationRecord),
            result.Output?.OperationId.Value,
            result.Output?.Status.ToString(),
            result.Output?.StatusReason,
            ToFeedbackHandleResult(result.FeedbackHandle),
            result.DriverDispatchAttempted);
    }

    public static MemoryOperationCancelToolResult ToCancelResult(
        MemoryOperationHandlerResult<MemoryOperationRecord> result)
    {
        var status = MemoryToolResultMapper.ToToolStatus(result.Status);
        return new MemoryOperationCancelToolResult(
            status,
            MemoryToolResultMapper.IsSuccess(status),
            result.Diagnostic,
            ResolveProviderInstanceId(result.Selection, result.OperationRecord),
            result.Output?.OperationId.Value,
            result.Output?.Status.ToString(),
            result.DriverDispatchAttempted);
    }

    public static MemoryEventAcknowledgeToolResult ToEventAcknowledgeResult(
        MemoryOperationHandlerResult<MemoryEventOutboxRecord> result)
    {
        var status = MemoryToolResultMapper.ToToolStatus(result.Status);
        return new MemoryEventAcknowledgeToolResult(
            status,
            MemoryToolResultMapper.IsSuccess(status),
            result.Diagnostic,
            ResolveProviderInstanceId(result.Selection, result.OperationRecord) ?? result.Output?.ProviderInstanceId.Value,
            result.Output?.OutboxRecordId.ToString(),
            result.DriverDispatchAttempted);
    }

    public static string? ResolveProviderInstanceId(
        MemoryProviderSelectionResult selection,
        MemoryOperationRecord? operationRecord)
    {
        return selection.SelectedProvider?.InstanceId.Value ??
               operationRecord?.ProviderInstanceId.Value ??
               selection.CandidateProviderIds.FirstOrDefault().Value;
    }

    public static MemoryFeedbackHandleToolResult? ToFeedbackHandleResult(MemoryFeedbackHandle? feedbackHandle)
    {
        return feedbackHandle is { } handle
            ? new MemoryFeedbackHandleToolResult(handle.Value)
            : null;
    }

    public static MemoryContextQueryToolResult RejectedQuery(
        MemoryToolResultStatus status,
        string diagnostic)
    {
        return new MemoryContextQueryToolResult(
            status,
            Success: false,
            diagnostic,
            ProviderInstanceId: null,
            OperationId: null,
            Summary: string.Empty,
            Sections: [],
            Warnings: [],
            Confidence: null,
            FeedbackHandle: null,
            AsyncOperation: null,
            DispatchAttempted: false);
    }

    public static MemoryIngestTextToolResult RejectedIngestion(
        MemoryToolResultStatus status,
        string diagnostic)
    {
        return new MemoryIngestTextToolResult(
            status,
            Success: false,
            diagnostic,
            ProviderInstanceId: null,
            OperationId: null,
            JobId: null,
            CapturedSnapshotId: null,
            PayloadForms: [],
            DispatchAttempted: false);
    }

    public static MemoryFeedbackSubmitToolResult RejectedFeedback(
        MemoryToolResultStatus status,
        string diagnostic)
    {
        return new MemoryFeedbackSubmitToolResult(
            status,
            Success: false,
            diagnostic,
            ProviderInstanceId: null,
            FeedbackRecordId: null,
            OperationId: null,
            DispatchAttempted: false);
    }

    public static MemoryOperationStatusToolResult RejectedStatus(
        MemoryToolResultStatus status,
        string diagnostic)
    {
        return new MemoryOperationStatusToolResult(
            status,
            Success: false,
            diagnostic,
            ProviderInstanceId: null,
            OperationId: null,
            OperationStatus: null,
            StatusReason: null,
            FeedbackHandle: null,
            DispatchAttempted: false);
    }

    public static MemoryOperationCancelToolResult RejectedCancel(
        MemoryToolResultStatus status,
        string diagnostic)
    {
        return new MemoryOperationCancelToolResult(
            status,
            Success: false,
            diagnostic,
            ProviderInstanceId: null,
            OperationId: null,
            OperationStatus: null,
            DispatchAttempted: false);
    }

    public static MemoryEventAcknowledgeToolResult RejectedEventAcknowledge(
        MemoryToolResultStatus status,
        string diagnostic)
    {
        return new MemoryEventAcknowledgeToolResult(
            status,
            Success: false,
            diagnostic,
            ProviderInstanceId: null,
            OutboxRecordId: null,
            DispatchAttempted: false);
    }

    private static MemoryContextSectionToolResult ToSectionResult(MemoryContextSection section)
    {
        return new MemoryContextSectionToolResult(
            section.Title,
            section.Text,
            section.Citations.Select(citation => new MemoryToolCitationResult(citation.SourceRef, citation.Label)).ToArray(),
            section.Confidence);
    }

    private static MemoryToolWarningResult ToWarningResult(MemoryWarning warning)
    {
        return new MemoryToolWarningResult(warning.Kind.ToString(), warning.Message);
    }

    private static MemoryAsyncOperationToolResult? ToAsyncOperationResult(MemoryOperationAccepted? accepted)
    {
        return accepted is null
            ? null
            : new MemoryAsyncOperationToolResult(
                accepted.OperationId.Value,
                accepted.StatusPath,
                accepted.ExpiresAtUtc,
                accepted.PollAfter,
                accepted.CallbackAvailable);
    }
}
