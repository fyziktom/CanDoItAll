using System.Collections.Concurrent;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatOperationEventSignal(TimeProvider timeProvider) : ILlmChatOperationEventSignal
{
    private const int MaximumRetainedStates = 4_096;
    private static readonly TimeSpan IdleRetention = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<SignalKey, SignalState> _states = new();
    private long _nextSweepUtcTicks;

    public void Publish(
        LlmChatRuntimeIdentity runtimeIdentity,
        LlmChatOperationId operationId,
        long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        var now = timeProvider.GetUtcNow();
        var state = _states.GetOrAdd(new(runtimeIdentity, operationId), _ => new(now));
        state.Publish(sequence, now);
        Trim(now);
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
        var now = timeProvider.GetUtcNow();
        var state = _states.GetOrAdd(new(runtimeIdentity, operationId), _ => new(now));
        var pending = state.AcquireWait(afterSequence, now);
        try
        {
            if (pending.IsCompleted)
            {
                return;
            }

            await pending.WaitAsync(maximumDelay, timeProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }
        finally
        {
            var completedAtUtc = timeProvider.GetUtcNow();
            state.ReleaseWait(completedAtUtc);
            Trim(completedAtUtc);
        }
    }

    private void Trim(DateTimeOffset now)
    {
        if (_states.Count <= MaximumRetainedStates && !TryStartSweep(now))
        {
            return;
        }

        foreach (var pair in _states)
        {
            if (pair.Value.CanEvict(now, requireIdle: true))
            {
                _states.TryRemove(pair);
            }
        }

        var excess = _states.Count - MaximumRetainedStates;
        if (excess <= 0)
        {
            return;
        }

        foreach (var pair in _states
                     .Where(item => item.Value.CanEvict(now, requireIdle: false))
                     .OrderBy(item => item.Value.LastTouchedAtUtc)
                     .ThenBy(item => item.Key.OperationId.Value)
                     .Take(excess))
        {
            _states.TryRemove(pair);
        }
    }

    private bool TryStartSweep(DateTimeOffset now)
    {
        var nowTicks = now.UtcTicks;
        var nextTicks = Volatile.Read(ref _nextSweepUtcTicks);
        return nowTicks >= nextTicks &&
               Interlocked.CompareExchange(
                   ref _nextSweepUtcTicks,
                   (now + SweepInterval).UtcTicks,
                   nextTicks) == nextTicks;
    }

    private sealed class SignalState(DateTimeOffset createdAtUtc)
    {
        private readonly object _gate = new();
        private long _latestSequence;
        private TaskCompletionSource _changed = CreateCompletionSource();
        private DateTimeOffset _lastTouchedAtUtc = createdAtUtc;
        private int _activeWaiters;

        public DateTimeOffset LastTouchedAtUtc
        {
            get
            {
                lock (_gate)
                {
                    return _lastTouchedAtUtc;
                }
            }
        }

        public void Publish(long sequence, DateTimeOffset observedAtUtc)
        {
            TaskCompletionSource changed;
            lock (_gate)
            {
                _lastTouchedAtUtc = observedAtUtc;
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

        public Task AcquireWait(long afterSequence, DateTimeOffset observedAtUtc)
        {
            lock (_gate)
            {
                _activeWaiters++;
                _lastTouchedAtUtc = observedAtUtc;
                return _latestSequence > afterSequence
                    ? Task.CompletedTask
                    : _changed.Task;
            }
        }

        public void ReleaseWait(DateTimeOffset observedAtUtc)
        {
            lock (_gate)
            {
                _activeWaiters--;
                _lastTouchedAtUtc = observedAtUtc;
            }
        }

        public bool CanEvict(DateTimeOffset observedAtUtc, bool requireIdle)
        {
            lock (_gate)
            {
                return _activeWaiters == 0 &&
                       (!requireIdle || observedAtUtc - _lastTouchedAtUtc >= IdleRetention);
            }
        }

        private static TaskCompletionSource CreateCompletionSource()
            => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private readonly record struct SignalKey(
        LlmChatRuntimeIdentity RuntimeIdentity,
        LlmChatOperationId OperationId);
}
