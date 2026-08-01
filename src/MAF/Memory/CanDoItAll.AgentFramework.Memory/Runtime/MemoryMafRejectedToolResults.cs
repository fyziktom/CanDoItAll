namespace CanDoItAll.AgentFramework.Memory;

internal static class MemoryMafRejectedToolResults
{
    public static MemoryContextQueryToolResult Query(
        MemoryToolResultStatus status,
        string diagnostic)
    {
        return new MemoryContextQueryToolResult(
            MemoryToolTrustFraming.Boundary,
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

    public static MemoryOperationStatusToolResult Status(
        MemoryToolResultStatus status,
        string diagnostic)
    {
        return new MemoryOperationStatusToolResult(
            MemoryToolTrustFraming.Boundary,
            status,
            Success: false,
            diagnostic,
            ProviderInstanceId: null,
            OperationId: null,
            OperationStatus: null,
            StatusReason: null,
            FeedbackHandle: null,
            FinalResult: null,
            DispatchAttempted: false);
    }

}
