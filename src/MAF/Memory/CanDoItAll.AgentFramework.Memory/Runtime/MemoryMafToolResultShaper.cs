using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.AgentFramework.Memory;

public static class MemoryMafToolResultShaper
{
    public static MemoryContextQueryToolResult ToQueryResult(
        MemoryOperationHandlerResult<MemoryContextPack> result)
    {
        var status = MemoryToolResultMapper.ToToolStatus(result.Status);
        var output = result.Output;
        var contextPack = output is null ? null : MemoryContextPackToolMapper.Map(output);
        return new MemoryContextQueryToolResult(
            MemoryToolTrustFraming.Boundary,
            status,
            MemoryToolResultMapper.IsSuccess(status),
            MemoryToolTrustFraming.FrameDiagnostic(result.Diagnostic, result.DriverDispatchAttempted),
            ResolveProviderInstanceId(result.Selection, result.OperationRecord),
            result.OperationRecord?.OperationId.Value ?? result.AcceptedOperation?.OperationId.Value,
            contextPack?.Summary ?? string.Empty,
            contextPack?.Sections ?? [],
            contextPack?.Warnings ?? [],
            contextPack?.Confidence,
            ToFeedbackHandleResult(result.FeedbackHandle ?? output?.FeedbackHandle),
            ToAsyncOperationResult(result.AcceptedOperation),
            result.DriverDispatchAttempted);
    }

    public static MemoryOperationStatusToolResult ToStatusResult(
        MemoryOperationHandlerResult<MemoryOperationRecord> result)
    {
        var status = MemoryToolResultMapper.ToToolStatus(result.Status);
        return new MemoryOperationStatusToolResult(
            MemoryToolTrustFraming.Boundary,
            status,
            MemoryToolResultMapper.IsSuccess(status),
            MemoryToolTrustFraming.FrameDiagnostic(result.Diagnostic, result.DriverDispatchAttempted),
            ResolveProviderInstanceId(result.Selection, result.OperationRecord),
            result.Output?.OperationId.Value,
            result.Output?.Status.ToString(),
            MemoryToolTrustFraming.FrameOptional(result.Output?.StatusReason),
            ToFeedbackHandleResult(result.FeedbackHandle),
            MemoryFinalOperationResultShaper.FromOperation(result.Output),
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
        return MemoryContextPackToolMapper.MapFeedbackHandle(feedbackHandle);
    }

    public static MemoryContextQueryToolResult RejectedQuery(
        MemoryToolResultStatus status,
        string diagnostic) => MemoryMafRejectedToolResults.Query(status, diagnostic);

    public static MemoryOperationStatusToolResult RejectedStatus(
        MemoryToolResultStatus status,
        string diagnostic) => MemoryMafRejectedToolResults.Status(status, diagnostic);

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
