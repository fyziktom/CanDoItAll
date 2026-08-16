using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatOperationDispatchSignal : ILlmChatOperationDispatchSignal
{
    private readonly object signalGate = new();
    private readonly SemaphoreSlim signal = new(0, 1);
    private int executorCount;
    private int progressingCount;

    public bool HasAvailableExecutor => Volatile.Read(ref executorCount) > 0;

    public LlmChatDispatchAvailability Availability => new(
        Volatile.Read(ref executorCount),
        Volatile.Read(ref progressingCount));

    public IDisposable RegisterExecutor()
    {
        Interlocked.Increment(ref executorCount);
        Signal();
        return new ExecutorRegistration(this);
    }

    public IDisposable BeginProgress()
    {
        var progressing = Interlocked.Increment(ref progressingCount);
        if (progressing <= Volatile.Read(ref executorCount))
        {
            return new ProgressRegistration(this);
        }

        Interlocked.Decrement(ref progressingCount);
        throw new InvalidOperationException("LLM Chat dispatcher progress requires a registered worker.");
    }

    public void Signal()
    {
        lock (signalGate)
        {
            if (signal.CurrentCount == 0)
            {
                signal.Release();
            }
        }
    }

    public async ValueTask WaitAsync(TimeSpan maximumDelay, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumDelay, TimeSpan.Zero);
        await signal.WaitAsync(maximumDelay, cancellationToken).ConfigureAwait(false);
    }

    private void UnregisterExecutor()
    {
        if (Interlocked.Decrement(ref executorCount) < 0)
        {
            throw new InvalidOperationException("LLM Chat dispatcher executor registration is unbalanced.");
        }
    }

    private void EndProgress()
    {
        if (Interlocked.Decrement(ref progressingCount) < 0)
        {
            throw new InvalidOperationException("LLM Chat dispatcher progress registration is unbalanced.");
        }
    }

    private sealed class ExecutorRegistration(LlmChatOperationDispatchSignal owner) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                owner.UnregisterExecutor();
            }
        }
    }

    private sealed class ProgressRegistration(LlmChatOperationDispatchSignal owner) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                owner.EndProgress();
            }
        }
    }
}
