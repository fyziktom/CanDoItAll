using System.Collections.Concurrent;
using System.Threading.Channels;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.ProviderPipelines;

public sealed class ProviderLocalBatchDispatcherHub<TPayload, TResult> : IAsyncDisposable
{
    private readonly ConcurrentDictionary<ProviderDispatchKey, ProviderLocalBatchDispatcher<TPayload, TResult>> dispatchers = new();

    public int DispatcherCount => dispatchers.Count;

    public Task<TResult> DispatchAsync(
        ProviderBatchEnvelope<TPayload> envelope,
        ProviderBatchPolicy policy,
        Func<IReadOnlyList<ProviderBatchExecutionItem<TPayload>>, CancellationToken, Task<IReadOnlyList<ProviderBatchItemResult<TResult>>>> executeBatchAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(executeBatchAsync);

        if (envelope.DispatchKey != policy.DispatchKey)
        {
            throw new InvalidOperationException("Batch envelope key must match the policy dispatch key.");
        }

        if (!policy.Limits.SupportsBatching)
        {
            return DispatchDirectAsync(envelope, executeBatchAsync, cancellationToken);
        }

        var dispatcher = dispatchers.GetOrAdd(
            envelope.DispatchKey,
            _ => new ProviderLocalBatchDispatcher<TPayload, TResult>(policy, executeBatchAsync));
        return dispatcher.EnqueueAsync(envelope, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var dispatcher in dispatchers.Values)
        {
            await dispatcher.DisposeAsync().ConfigureAwait(false);
        }

        dispatchers.Clear();
    }

    private static async Task<TResult> DispatchDirectAsync(
        ProviderBatchEnvelope<TPayload> envelope,
        Func<IReadOnlyList<ProviderBatchExecutionItem<TPayload>>, CancellationToken, Task<IReadOnlyList<ProviderBatchItemResult<TResult>>>> executeBatchAsync,
        CancellationToken cancellationToken)
    {
        var correlationId = ResolveCorrelationId(envelope.CorrelationId);
        var item = new ProviderBatchExecutionItem<TPayload>(
            correlationId,
            envelope.DispatchKey,
            envelope.Payload);
        var results = await executeBatchAsync([item], cancellationToken).ConfigureAwait(false);
        var mapped = results.FirstOrDefault(result => result.CorrelationId == correlationId);
        if (mapped is null)
        {
            throw new InvalidOperationException($"Provider batch result did not include correlation id '{correlationId}'.");
        }

        if (!mapped.Success)
        {
            throw mapped.Exception ?? new InvalidOperationException("Provider batch item failed without exception details.");
        }

        return mapped.Result!;
    }

    internal static Guid ResolveCorrelationId(Guid? correlationId)
    {
        return correlationId is { } value && value != Guid.Empty
            ? value
            : Guid.NewGuid();
    }
}

public sealed class ProviderLocalBatchDispatcher<TPayload, TResult> : IAsyncDisposable
{
    private readonly ProviderBatchPolicy policy;
    private readonly Func<IReadOnlyList<ProviderBatchExecutionItem<TPayload>>, CancellationToken, Task<IReadOnlyList<ProviderBatchItemResult<TResult>>>> executeBatchAsync;
    private readonly Channel<QueuedBatchRequest> channel;
    private readonly ConcurrentDictionary<Guid, QueuedBatchRequest> pendingRequests = new();
    private readonly CancellationTokenSource shutdown = new();
    private readonly SemaphoreSlim inFlightBatches;
    private readonly Task workerTask;
    private bool isDisposed;

    public ProviderLocalBatchDispatcher(
        ProviderBatchPolicy policy,
        Func<IReadOnlyList<ProviderBatchExecutionItem<TPayload>>, CancellationToken, Task<IReadOnlyList<ProviderBatchItemResult<TResult>>>> executeBatchAsync)
    {
        this.policy = ValidatePolicy(policy);
        this.executeBatchAsync = executeBatchAsync ?? throw new ArgumentNullException(nameof(executeBatchAsync));
        channel = Channel.CreateBounded<QueuedBatchRequest>(new BoundedChannelOptions(policy.Limits.MaxQueueDepth)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
        inFlightBatches = new SemaphoreSlim(policy.Limits.MaxInFlightBatches, policy.Limits.MaxInFlightBatches);
        workerTask = Task.Run(ProcessQueueAsync);
    }

    public Task<TResult> EnqueueAsync(
        ProviderBatchEnvelope<TPayload> envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ThrowIfDisposed();

        if (envelope.DispatchKey != policy.DispatchKey)
        {
            throw new InvalidOperationException("Batch envelope key does not match this dispatcher policy.");
        }

        if (pendingRequests.Count >= policy.Limits.MaxQueueDepth)
        {
            return policy.QueueFullBehavior == ProviderBatchQueueFullBehavior.FailFast
                ? Task.FromException<TResult>(new ProviderBatchQueueCapacityExceededException(policy.DispatchKey, policy.Limits.MaxQueueDepth))
                : EnqueueWithWaitAsync(envelope, cancellationToken);
        }

        var queued = CreateQueuedRequest(envelope, cancellationToken);
        if (!channel.Writer.TryWrite(queued))
        {
            pendingRequests.TryRemove(queued.CorrelationId, out _);
            return policy.QueueFullBehavior == ProviderBatchQueueFullBehavior.FailFast
                ? Task.FromException<TResult>(new ProviderBatchQueueCapacityExceededException(policy.DispatchKey, policy.Limits.MaxQueueDepth))
                : EnqueueWithWaitAsync(envelope, cancellationToken);
        }

        return queued.Task;
    }

    public async ValueTask DisposeAsync()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        channel.Writer.TryComplete();
        await shutdown.CancelAsync().ConfigureAwait(false);

        foreach (var pending in pendingRequests.Values)
        {
            pending.TrySetCanceled();
        }

        pendingRequests.Clear();

        try
        {
            await workerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        inFlightBatches.Dispose();
        shutdown.Dispose();
    }

    private async Task<TResult> EnqueueWithWaitAsync(
        ProviderBatchEnvelope<TPayload> envelope,
        CancellationToken cancellationToken)
    {
        var queued = CreateQueuedRequest(envelope, cancellationToken);
        await channel.Writer.WriteAsync(queued, cancellationToken).ConfigureAwait(false);
        return await queued.Task.ConfigureAwait(false);
    }

    private QueuedBatchRequest CreateQueuedRequest(
        ProviderBatchEnvelope<TPayload> envelope,
        CancellationToken cancellationToken)
    {
        var correlationId = ProviderLocalBatchDispatcherHub<TPayload, TResult>.ResolveCorrelationId(envelope.CorrelationId);
        var queued = new QueuedBatchRequest(
            correlationId,
            envelope.DispatchKey,
            envelope.Payload,
            DateTimeOffset.UtcNow.Add(policy.Limits.RequestTimeout));
        if (!pendingRequests.TryAdd(correlationId, queued))
        {
            throw new InvalidOperationException($"A provider batch request with correlation id '{correlationId}' is already queued.");
        }

        queued.RegisterCancellation(
            cancellationToken,
            policy.Limits.RequestTimeout,
            () => pendingRequests.TryRemove(correlationId, out _));
        return queued;
    }

    private async Task ProcessQueueAsync()
    {
        await foreach (var first in channel.Reader.ReadAllAsync(shutdown.Token).ConfigureAwait(false))
        {
            var batch = await BuildBatchAsync(first, shutdown.Token).ConfigureAwait(false);
            if (batch.Count == 0)
            {
                continue;
            }

            await inFlightBatches.WaitAsync(shutdown.Token).ConfigureAwait(false);
            _ = ExecuteAndReleaseAsync(batch, shutdown.Token);
        }
    }

    private async Task<IReadOnlyList<QueuedBatchRequest>> BuildBatchAsync(
        QueuedBatchRequest first,
        CancellationToken cancellationToken)
    {
        var batch = new List<QueuedBatchRequest>(policy.Limits.MaxBatchSize);
        AddIfActive(batch, first);
        var flushAt = DateTimeOffset.UtcNow.Add(policy.Limits.MaxQueueDelay);

        while (batch.Count < policy.Limits.MaxBatchSize)
        {
            while (batch.Count < policy.Limits.MaxBatchSize && channel.Reader.TryRead(out var queued))
            {
                AddIfActive(batch, queued);
            }

            if (batch.Count >= policy.Limits.MaxBatchSize)
            {
                break;
            }

            var remaining = flushAt - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            bool hasMore;
            try
            {
                hasMore = await channel.Reader.WaitToReadAsync(cancellationToken).AsTask().WaitAsync(remaining, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                break;
            }

            if (!hasMore)
            {
                break;
            }
        }

        return batch;
    }

    private async Task ExecuteAndReleaseAsync(
        IReadOnlyList<QueuedBatchRequest> batch,
        CancellationToken cancellationToken)
    {
        try
        {
            var activeBatch = batch
                .Where(request => request.TryMarkDispatching())
                .ToList();
            if (activeBatch.Count == 0)
            {
                return;
            }

            var items = activeBatch
                .Select(request => new ProviderBatchExecutionItem<TPayload>(
                    request.CorrelationId,
                    request.DispatchKey,
                    request.Payload))
                .ToList();
            var results = await executeBatchAsync(items, cancellationToken).ConfigureAwait(false);
            var resultsByCorrelationId = results.ToDictionary(result => result.CorrelationId);

            foreach (var request in activeBatch)
            {
                if (!resultsByCorrelationId.TryGetValue(request.CorrelationId, out var result))
                {
                    request.TrySetException(new InvalidOperationException($"Provider batch result did not include correlation id '{request.CorrelationId}'."));
                }
                else if (result.Success)
                {
                    request.TrySetResult(result.Result!);
                }
                else
                {
                    request.TrySetException(result.Exception ?? new InvalidOperationException("Provider batch item failed without exception details."));
                }

                pendingRequests.TryRemove(request.CorrelationId, out _);
            }
        }
        catch (Exception exception)
        {
            foreach (var request in batch)
            {
                request.TrySetException(exception);
                pendingRequests.TryRemove(request.CorrelationId, out _);
            }
        }
        finally
        {
            inFlightBatches.Release();
        }
    }

    private static void AddIfActive(
        ICollection<QueuedBatchRequest> batch,
        QueuedBatchRequest request)
    {
        if (!request.IsCompleted && request.DeadlineUtc > DateTimeOffset.UtcNow)
        {
            batch.Add(request);
        }
        else if (!request.IsCompleted && request.DeadlineUtc <= DateTimeOffset.UtcNow)
        {
            request.TrySetException(new TimeoutException("Provider batch request timed out before dispatch."));
        }
    }

    private static ProviderBatchPolicy ValidatePolicy(ProviderBatchPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (!policy.Limits.SupportsBatching)
        {
            throw new InvalidOperationException("ProviderLocalBatchDispatcher requires batching-enabled dispatch limits.");
        }

        return policy;
    }

    private void ThrowIfDisposed()
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(ProviderLocalBatchDispatcher<TPayload, TResult>));
        }
    }

    private sealed class QueuedBatchRequest(
        Guid correlationId,
        ProviderDispatchKey dispatchKey,
        TPayload payload,
        DateTimeOffset deadlineUtc)
    {
        private readonly TaskCompletionSource<TResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int dispatchState;

        public Guid CorrelationId { get; } = correlationId;

        public ProviderDispatchKey DispatchKey { get; } = dispatchKey;

        public TPayload Payload { get; } = payload;

        public DateTimeOffset DeadlineUtc { get; } = deadlineUtc;

        public Task<TResult> Task => completion.Task;

        public bool IsCompleted => completion.Task.IsCompleted;

        public void RegisterCancellation(
            CancellationToken cancellationToken,
            TimeSpan timeout,
            Action onCompleted)
        {
            var cancellationRegistration = cancellationToken.Register(
                static state =>
                {
                    var tuple = (Tuple<QueuedBatchRequest, Action>)state!;
                    if (tuple.Item1.TrySetCanceled())
                    {
                        tuple.Item2();
                    }
                },
                Tuple.Create(this, onCompleted));
            var timeoutCancellation = new CancellationTokenSource(timeout);
            var timeoutRegistration = timeoutCancellation.Token.Register(
                static state =>
                {
                    var tuple = (Tuple<QueuedBatchRequest, Action>)state!;
                    if (tuple.Item1.TrySetException(new TimeoutException("Provider batch request timed out.")))
                    {
                        tuple.Item2();
                    }
                },
                Tuple.Create(this, onCompleted));

            completion.Task.ContinueWith(
                _ =>
                {
                    cancellationRegistration.Dispose();
                    timeoutRegistration.Dispose();
                    timeoutCancellation.Dispose();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public bool TryMarkDispatching()
        {
            return Interlocked.CompareExchange(ref dispatchState, 1, 0) == 0 &&
                   !IsCompleted;
        }

        public bool TrySetResult(TResult result)
        {
            return completion.TrySetResult(result);
        }

        public bool TrySetCanceled()
        {
            return completion.TrySetCanceled();
        }

        public bool TrySetException(Exception exception)
        {
            return completion.TrySetException(exception);
        }
    }
}
