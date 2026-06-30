using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.ProviderPipelines;

public enum ProviderBatchQueueFullBehavior
{
    FailFast,
    Wait
}

public sealed record ProviderBatchPolicy(
    ProviderDispatchKey DispatchKey,
    ProviderDispatchLimits Limits,
    ProviderBatchQueueFullBehavior QueueFullBehavior = ProviderBatchQueueFullBehavior.FailFast);

public sealed record ProviderBatchEnvelope<TPayload>(
    ProviderDispatchKey DispatchKey,
    TPayload Payload,
    Guid? CorrelationId = null);

public sealed record ProviderBatchExecutionItem<TPayload>(
    Guid CorrelationId,
    ProviderDispatchKey DispatchKey,
    TPayload Payload);

public sealed record ProviderBatchItemResult<TResult>(
    Guid CorrelationId,
    bool Success,
    TResult? Result,
    Exception? Exception)
{
    public static ProviderBatchItemResult<TResult> Succeeded(Guid correlationId, TResult result)
    {
        return new ProviderBatchItemResult<TResult>(correlationId, true, result, null);
    }

    public static ProviderBatchItemResult<TResult> Failed(Guid correlationId, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new ProviderBatchItemResult<TResult>(correlationId, false, default, exception);
    }
}

public sealed class ProviderBatchQueueCapacityExceededException : InvalidOperationException
{
    public ProviderBatchQueueCapacityExceededException(ProviderDispatchKey dispatchKey, int maxQueueDepth)
        : base($"Provider batch queue for model '{dispatchKey.Model}' is full. Max queue depth: {maxQueueDepth}.")
    {
        DispatchKey = dispatchKey;
        MaxQueueDepth = maxQueueDepth;
    }

    public ProviderDispatchKey DispatchKey { get; }

    public int MaxQueueDepth { get; }
}
