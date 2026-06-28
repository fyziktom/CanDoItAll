namespace CanDoItAll.AgentFramework.Models;

public enum ProviderBatchFailurePolicy
{
    Continue,
    FailFast
}

public enum ProviderBatchPersistenceMode
{
    Transient,
    Checkpointed
}

public enum ProviderBatchItemStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Cancelled,
    Recovered
}

public static class ProviderBatchRejectionCodes
{
    public const string ProviderDisabled = "Provider.Disabled";
    public const string ProviderUnhealthy = "Provider.Unhealthy";
    public const string ModelMissing = "Provider.ModelMissing";
    public const string ModelMismatch = "Provider.ModelMismatch";
    public const string CapabilityUnsupported = "Provider.CapabilityUnsupported";
    public const string RuntimeMismatch = "Provider.RuntimeMismatch";
}

public sealed record ProviderBatchInput<TPayload>(
    Guid InputId,
    int Sequence,
    string SourceReference,
    TPayload Payload);

public sealed record ProviderBatchProviderSelection(
    ProviderProfile Provider,
    string Model = "",
    int? MaxParallelism = null,
    bool RequireHealthy = false);

public sealed record ProviderBatchExecutionPolicy(
    int MaxTotalParallelism = 4,
    int MaxPerProviderParallelism = 1,
    int MaxAttempts = 1,
    ProviderBatchFailurePolicy FailurePolicy = ProviderBatchFailurePolicy.Continue,
    ProviderBatchPersistenceMode PersistenceMode = ProviderBatchPersistenceMode.Transient);

public sealed record ProviderBatchJobRequest<TPayload>(
    Guid JobId,
    IReadOnlyList<ProviderBatchInput<TPayload>> Inputs,
    IReadOnlyList<ProviderBatchProviderSelection> Providers,
    AgentProviderCapabilityKind Capability,
    AgentProviderOperationKind Operation,
    string Model = "",
    ProviderBatchExecutionPolicy? Policy = null);

public sealed record ProviderBatchProviderRejection(
    Guid ProviderProfileId,
    string ProviderName,
    ProviderKind ProviderKind,
    string ReasonCode,
    string Message);

public sealed record ProviderBatchDispatchLane(
    string LaneKey,
    Guid ProviderProfileId,
    ProviderKind ProviderKind,
    string ProviderName,
    string Model,
    ProviderDispatchLimits DispatchLimits,
    int PlannedParallelism);

public sealed record ProviderBatchDispatchAssignment(
    Guid InputId,
    int Sequence,
    string SourceReference,
    string LaneKey,
    Guid ProviderProfileId,
    ProviderKind ProviderKind,
    string ProviderName,
    string Model,
    int PlannedAttempt);

public sealed record ProviderBatchDispatchPlan(
    Guid JobId,
    AgentProviderCapabilityKind Capability,
    AgentProviderOperationKind Operation,
    int InputCount,
    IReadOnlyList<ProviderBatchDispatchLane> Lanes,
    IReadOnlyList<ProviderBatchDispatchAssignment> Assignments,
    IReadOnlyList<ProviderBatchProviderRejection> Rejections);

public sealed record ProviderBatchDispatchOutcome<TResult>(
    TResult Value,
    ProviderUsageObservation? UsageObservation = null,
    string ResultReference = "")
{
    public static ProviderBatchDispatchOutcome<TResult> FromValue(
        TResult value,
        ProviderUsageObservation? usageObservation = null,
        string resultReference = "")
    {
        return new ProviderBatchDispatchOutcome<TResult>(
            value,
            usageObservation,
            resultReference);
    }
}

public sealed record ProviderBatchItemCheckpoint(
    Guid JobId,
    Guid InputId,
    int Sequence,
    ProviderBatchItemStatus Status,
    Guid? ProviderProfileId,
    ProviderKind? ProviderKind,
    string ProviderName,
    string Model,
    int AttemptCount,
    string ResultReference,
    string ErrorCode,
    string ErrorMessage,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProviderBatchJobItemResult<TResult>(
    Guid InputId,
    int Sequence,
    string SourceReference,
    ProviderBatchItemStatus Status,
    Guid? ProviderProfileId,
    ProviderKind? ProviderKind,
    string ProviderName,
    string Model,
    int AttemptCount,
    TResult? Value,
    ProviderUsageObservation? UsageObservation,
    string ResultReference,
    string ErrorCode,
    string ErrorMessage)
{
    public static ProviderBatchJobItemResult<TResult> Succeeded(
        ProviderBatchInput<object?> source,
        ProviderBatchDispatchAssignment assignment,
        int attemptCount,
        ProviderBatchDispatchOutcome<TResult> outcome)
    {
        return new ProviderBatchJobItemResult<TResult>(
            source.InputId,
            source.Sequence,
            source.SourceReference,
            ProviderBatchItemStatus.Succeeded,
            assignment.ProviderProfileId,
            assignment.ProviderKind,
            assignment.ProviderName,
            assignment.Model,
            attemptCount,
            outcome.Value,
            outcome.UsageObservation,
            outcome.ResultReference,
            string.Empty,
            string.Empty);
    }

    public static ProviderBatchJobItemResult<TResult> Failed(
        ProviderBatchInput<object?> source,
        ProviderBatchDispatchAssignment assignment,
        int attemptCount,
        string errorCode,
        string errorMessage)
    {
        return new ProviderBatchJobItemResult<TResult>(
            source.InputId,
            source.Sequence,
            source.SourceReference,
            ProviderBatchItemStatus.Failed,
            assignment.ProviderProfileId,
            assignment.ProviderKind,
            assignment.ProviderName,
            assignment.Model,
            attemptCount,
            default,
            null,
            string.Empty,
            errorCode,
            errorMessage);
    }

    public static ProviderBatchJobItemResult<TResult> Cancelled(
        ProviderBatchInput<object?> source,
        ProviderBatchDispatchAssignment assignment,
        int attemptCount,
        string errorMessage)
    {
        return new ProviderBatchJobItemResult<TResult>(
            source.InputId,
            source.Sequence,
            source.SourceReference,
            ProviderBatchItemStatus.Cancelled,
            assignment.ProviderProfileId,
            assignment.ProviderKind,
            assignment.ProviderName,
            assignment.Model,
            attemptCount,
            default,
            null,
            string.Empty,
            "ProviderBatch.Cancelled",
            errorMessage);
    }

    public static ProviderBatchJobItemResult<TResult> Recovered(
        ProviderBatchInput<object?> source,
        ProviderBatchItemCheckpoint checkpoint)
    {
        return new ProviderBatchJobItemResult<TResult>(
            source.InputId,
            source.Sequence,
            source.SourceReference,
            ProviderBatchItemStatus.Recovered,
            checkpoint.ProviderProfileId,
            checkpoint.ProviderKind,
            checkpoint.ProviderName,
            checkpoint.Model,
            checkpoint.AttemptCount,
            default,
            null,
            checkpoint.ResultReference,
            string.Empty,
            string.Empty);
    }
}

public sealed record ProviderBatchJobResult<TResult>(
    Guid JobId,
    ProviderBatchDispatchPlan Plan,
    IReadOnlyList<ProviderBatchJobItemResult<TResult>> Items,
    IReadOnlyList<ProviderBatchProviderRejection> Rejections)
{
    public bool Succeeded => Items.All(item =>
        item.Status is ProviderBatchItemStatus.Succeeded or ProviderBatchItemStatus.Recovered);
}
