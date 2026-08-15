using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatOperationDispatchSignal : ILlmChatOperationDispatchSignal
{
    private readonly object signalGate = new();
    private readonly SemaphoreSlim signal = new(0, 1);
    private int executorCount;

    public bool HasAvailableExecutor => Volatile.Read(ref executorCount) > 0;

    public IDisposable RegisterExecutor()
    {
        Interlocked.Increment(ref executorCount);
        Signal();
        return new ExecutorRegistration(this);
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
}
