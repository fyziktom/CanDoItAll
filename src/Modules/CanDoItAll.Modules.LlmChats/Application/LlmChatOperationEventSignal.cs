using System.Collections.Concurrent;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatOperationEventSignal(TimeProvider timeProvider) : ILlmChatOperationEventSignal
{
    private readonly ConcurrentDictionary<SignalKey, SignalState> _states = new();

    public void Publish(
        LlmChatRuntimeIdentity runtimeIdentity,
        LlmChatOperationId operationId,
        long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        var state = _states.GetOrAdd(new(runtimeIdentity, operationId), static _ => new());
        state.Publish(sequence);
    }

    public async ValueTask WaitAsync(
        LlmChatRuntimeIdentity runtimeIdentity,
        LlmChatOperationId operationId,
        long afterSequence,
        TimeSpan maximumDelay,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterSequence);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumDelay, TimeSpan.Zero);
        var state = _states.GetOrAdd(new(runtimeIdentity, operationId), static _ => new());
        var pending = state.GetWaitTask(afterSequence);
        if (pending.IsCompleted)
        {
            return;
        }

        try
        {
            await pending.WaitAsync(maximumDelay, timeProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }
    }

    private sealed class SignalState
    {
        private readonly object _gate = new();
        private long _latestSequence;
        private TaskCompletionSource _changed = CreateCompletionSource();

        public void Publish(long sequence)
        {
            TaskCompletionSource changed;
            lock (_gate)
            {
                if (sequence <= _latestSequence)
                {
                    return;
                }

                _latestSequence = sequence;
                changed = _changed;
                _changed = CreateCompletionSource();
            }

            changed.TrySetResult();
        }

        public Task GetWaitTask(long afterSequence)
        {
            lock (_gate)
            {
                return _latestSequence > afterSequence
                    ? Task.CompletedTask
                    : _changed.Task;
            }
        }

        private static TaskCompletionSource CreateCompletionSource()
            => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly record struct SignalKey(
        LlmChatRuntimeIdentity RuntimeIdentity,
        LlmChatOperationId OperationId);
}
