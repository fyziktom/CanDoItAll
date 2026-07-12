using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Memory.Tests;

internal enum MemoryWorkerInvocation
{
    OperationPolling = 0,
    FeedbackDelivery = 1,
    ProviderEventPolling = 2,
    ProviderEventInbox = 3,
    ProviderEventOutbox = 4,
    Retention = 5
}

internal sealed class RecordingMemoryWorkers :
    IMemoryAsyncOperationWorker,
    IMemoryFeedbackWorker,
    IMemoryProviderEventWorker,
    IMemoryRetentionWorker
{
    public List<MemoryWorkerInvocation> Invocations { get; } = [];

    public MemoryWorkerInvocation? Failure { get; init; }

    public Task<MemoryAsyncWorkerRunResult> PollOperationsAsync(
        CancellationToken cancellationToken = default) =>
        InvokeAsync(MemoryWorkerInvocation.OperationPolling, cancellationToken);

    public Task<MemoryAsyncWorkerRunResult> DeliverPendingFeedbackAsync(
        CancellationToken cancellationToken = default) =>
        InvokeAsync(MemoryWorkerInvocation.FeedbackDelivery, cancellationToken);

    public Task<MemoryAsyncWorkerRunResult> PollProviderEventsAsync(
        CancellationToken cancellationToken = default) =>
        InvokeAsync(MemoryWorkerInvocation.ProviderEventPolling, cancellationToken);

    public Task<MemoryAsyncWorkerRunResult> DrainInboxAsync(
        CancellationToken cancellationToken = default) =>
        InvokeAsync(MemoryWorkerInvocation.ProviderEventInbox, cancellationToken);

    public Task<MemoryAsyncWorkerRunResult> DrainOutboxAsync(
        CancellationToken cancellationToken = default) =>
        InvokeAsync(MemoryWorkerInvocation.ProviderEventOutbox, cancellationToken);

    public Task<MemoryAsyncWorkerRunResult> ApplyDueRetentionAsync(
        CancellationToken cancellationToken = default) =>
        InvokeAsync(MemoryWorkerInvocation.Retention, cancellationToken);

    public Task<MemoryOperationRecord> CancelOperationAsync(
        MemoryOperationId operationId,
        string reason,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<MemoryEventAdmissionResult> AdmitProviderEventAsync(
        MemoryProviderProfile provider,
        MemoryProviderEvent providerEvent,
        MemoryEventLoopContext loopContext,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    private Task<MemoryAsyncWorkerRunResult> InvokeAsync(
        MemoryWorkerInvocation invocation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Invocations.Add(invocation);
        if (Failure == invocation)
        {
            throw new InvalidOperationException($"Scripted failure for '{invocation}'.");
        }

        return Task.FromResult(MemoryAsyncWorkerRunResult.Empty);
    }
}

internal sealed class BlockingMemoryOperationWorker : IMemoryAsyncOperationWorker
{
    private readonly TaskCompletionSource started = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource cancellationObserved = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => started.Task;

    public Task CancellationObserved => cancellationObserved.Task;

    public async Task<MemoryAsyncWorkerRunResult> PollOperationsAsync(
        CancellationToken cancellationToken = default)
    {
        started.TrySetResult();
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancellationObserved.TrySetResult();
            throw;
        }

        return MemoryAsyncWorkerRunResult.Empty;
    }

    public Task<MemoryOperationRecord> CancelOperationAsync(
        MemoryOperationId operationId,
        string reason,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class PassThroughMemoryWorkerLeaseRunner : IMemoryWorkerLeaseRunner
{
    public MemoryBackgroundWorkerPhase? UnavailablePhase { get; init; }

    public async Task<MemoryWorkerLeaseExecution> RunAsync(
        MemoryBackgroundWorkerPhase phase,
        Func<CancellationToken, Task<MemoryAsyncWorkerRunResult>> execute,
        CancellationToken cancellationToken = default)
    {
        if (phase == UnavailablePhase)
        {
            return MemoryWorkerLeaseExecution.NotAcquired;
        }

        return MemoryWorkerLeaseExecution.Completed(await execute(cancellationToken));
    }
}

internal sealed class RecordingMemoryLoggerFactory : ILoggerFactory
{
    public List<MemoryLogEntry> Entries { get; } = [];

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public ILogger CreateLogger(string categoryName) => new RecordingMemoryLogger(Entries);

    public void Dispose()
    {
    }

    private sealed class RecordingMemoryLogger(List<MemoryLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Add(new MemoryLogEntry(logLevel, formatter(state, exception)));
        }
    }
}

internal sealed record MemoryLogEntry(
    LogLevel Level,
    string Message);
