using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public readonly record struct WorkflowExecutorInvocationScopeKey
{
    [JsonConstructor]
    public WorkflowExecutorInvocationScopeKey(string value)
    {
        Value = WorkflowBackendCheckpointValue.RequireSha256(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WorkflowExecutorInvocationKey
{
    [JsonConstructor]
    public WorkflowExecutorInvocationKey(string value)
    {
        Value = WorkflowBackendCheckpointValue.RequireSha256(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WorkflowExecutorInvocationIdempotencyKey
{
    [JsonConstructor]
    public WorkflowExecutorInvocationIdempotencyKey(string value)
    {
        Value = WorkflowBackendCheckpointValue.RequireSha256(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WorkflowExecutorContractVersion
{
    [JsonConstructor]
    public WorkflowExecutorContractVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Workflow executor contract version cannot contain leading or trailing whitespace.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WorkflowExecutorInvocationGeneration
{
    [JsonConstructor]
    public WorkflowExecutorInvocationGeneration(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Workflow executor invocation generation cannot be negative.");
        }

        Value = value;
    }

    public long Value { get; }

    public static WorkflowExecutorInvocationGeneration Initial { get; } = new(0);

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct WorkflowExecutorInvocationConcurrencyVersion
{
    [JsonConstructor]
    public WorkflowExecutorInvocationConcurrencyVersion(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Workflow executor invocation concurrency version cannot be negative.");
        }

        Value = value;
    }

    public long Value { get; }

    public static WorkflowExecutorInvocationConcurrencyVersion Initial { get; } = new(0);

    public WorkflowExecutorInvocationConcurrencyVersion Next() => new(checked(Value + 1));

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct WorkflowExecutorInvocationLeaseOwnerId
{
    [JsonConstructor]
    public WorkflowExecutorInvocationLeaseOwnerId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Workflow executor invocation lease owner id cannot contain leading or trailing whitespace.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct WorkflowExecutorInvocationLeaseEpoch
{
    [JsonConstructor]
    public WorkflowExecutorInvocationLeaseEpoch(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Workflow executor invocation lease epoch must be positive.");
        }

        Value = value;
    }

    public long Value { get; }

    public WorkflowExecutorInvocationLeaseEpoch Next() => new(checked(Value + 1));

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct WorkflowExecutorInvocationFailureCode
{
    [JsonConstructor]
    public WorkflowExecutorInvocationFailureCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Workflow executor invocation failure code cannot contain leading or trailing whitespace.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public static WorkflowExecutorInvocationFailureCode Cancelled { get; } = new("invocation-cancelled");

    public static WorkflowExecutorInvocationFailureCode ExecutionFailed { get; } = new("executor-invocation-failed");

    public static WorkflowExecutorInvocationFailureCode UnsafeResultNotPersisted { get; } = new("unsafe-result-not-persisted");

    public static WorkflowExecutorInvocationFailureCode AttemptLimitReached { get; } = new("attempt-limit-reached");

    public override string ToString() => Value;
}

public enum WorkflowExecutorInvocationState
{
    Claimed,
    Completed,
    FailedRetryable,
    FailedTerminal
}

public sealed record WorkflowExecutorInvocationIdentity(
    WorkflowExecutorInvocationScopeKey ScopeKey,
    WorkflowExecutorInvocationKey Key,
    WorkflowExecutorInvocationIdempotencyKey IdempotencyKey,
    WorkflowRunId RunId,
    WorkflowVersionId WorkflowVersionId,
    WorkflowNodeId NodeId,
    WorkflowExecutorId ExecutorId,
    WorkflowExecutorContractVersion ExecutorContractVersion,
    WorkflowExternalRequestId CausationRequestId,
    WorkflowExternalRequestVersion CausationRequestVersion,
    WorkflowExternalResponseOperationId CausationOperationId,
    WorkflowExecutorInvocationGeneration LogicalGeneration,
    WorkflowExecutorInputHash InputHash);

public sealed record WorkflowExecutorInvocationLease(
    WorkflowExecutorInvocationLeaseOwnerId OwnerId,
    WorkflowExecutorInvocationLeaseEpoch Epoch,
    DateTimeOffset AcquiredAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record WorkflowExecutorInvocationStoredResult(
    WorkflowNodeExecutionResult Result,
    DateTimeOffset CompletedAtUtc)
{
    public override string ToString() => "[REDACTED]";
}

public sealed record WorkflowExecutorInvocationRecord(
    WorkflowExecutorInvocationIdentity Identity,
    WorkflowExecutorInvocationState State,
    int Attempt,
    WorkflowExecutorInvocationConcurrencyVersion ConcurrencyVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public WorkflowExecutorInvocationLease? Lease { get; init; }

    public WorkflowExecutorInvocationStoredResult? StoredResult { get; init; }

    public WorkflowExecutorInvocationFailureCode? FailureCode { get; init; }

    public string SafeMessage { get; init; } = string.Empty;
}

public sealed record WorkflowExecutorInvocationClaim(
    WorkflowExecutorInvocationIdentity Identity,
    WorkflowExecutorInvocationLease Lease,
    int Attempt,
    WorkflowExecutorInvocationConcurrencyVersion ConcurrencyVersion);
