using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

public sealed class LlmChatOperationEventRetentionSchedule
{
    private const int MaximumProfileStates = 128;
    private readonly object _gate = new();
    private readonly Dictionary<LlmChatRuntimeIdentity, ScheduleState> _states = [];

    public bool TryAcquire(
        LlmChatRuntimeIdentity runtimeIdentity,
        DateTimeOffset observedAtUtc,
        TimeSpan interval)
    {
        lock (_gate)
        {
            EvictSupersededGenerations(runtimeIdentity);
            if (_states.TryGetValue(runtimeIdentity, out var state))
            {
                state.LastObservedAtUtc = observedAtUtc;
                if (state.Running || state.NextRunAtUtc > observedAtUtc)
                {
                    return false;
                }
            }
            else
            {
                state = new ScheduleState();
                _states.Add(runtimeIdentity, state);
            }

            state.Running = true;
            state.NextRunAtUtc = observedAtUtc + interval;
            state.LastObservedAtUtc = observedAtUtc;
            TrimInactiveStates(runtimeIdentity);
            return true;
        }
    }

    public void Complete(
        LlmChatRuntimeIdentity runtimeIdentity,
        DateTimeOffset observedAtUtc,
        TimeSpan interval,
        bool retryImmediately)
    {
        lock (_gate)
        {
            if (!_states.TryGetValue(runtimeIdentity, out var state))
            {
                return;
            }

            state.Running = false;
            state.NextRunAtUtc = retryImmediately ? observedAtUtc : observedAtUtc + interval;
            state.LastObservedAtUtc = observedAtUtc;
        }
    }

    private void EvictSupersededGenerations(LlmChatRuntimeIdentity current)
    {
        foreach (var pair in _states.Where(pair =>
                     pair.Key.ProfileId == current.ProfileId &&
                     pair.Key != current &&
                     !pair.Value.Running).ToArray())
        {
            _states.Remove(pair.Key);
        }
    }

    private void TrimInactiveStates(LlmChatRuntimeIdentity current)
    {
        foreach (var pair in _states
                     .Where(pair => pair.Key != current && !pair.Value.Running)
                     .OrderBy(pair => pair.Value.LastObservedAtUtc)
                     .Take(Math.Max(0, _states.Count - MaximumProfileStates))
                     .ToArray())
        {
            _states.Remove(pair.Key);
        }
    }

    private sealed class ScheduleState
    {
        public DateTimeOffset NextRunAtUtc { get; set; }

        public DateTimeOffset LastObservedAtUtc { get; set; }

        public bool Running { get; set; }
    }
}

public sealed class LlmChatOperationEventRetentionService(
    LlmChatOperationEventJournal eventJournal,
    LlmChatOperationEventRetentionSchedule schedule,
    ILlmChatOperationScopeAccessor operationScope,
    LlmChatStreamingOptions options,
    TimeProvider timeProvider)
{
    public async Task<int> ApplyIfDueAsync(CancellationToken cancellationToken = default)
    {
        options.Validate();
        var identity = operationScope.Current?.RuntimeIdentity
            ?? throw new InvalidOperationException("An LLM Chat runtime operation scope is required for event retention.");
        var startedAtUtc = timeProvider.GetUtcNow();
        if (!schedule.TryAcquire(identity, startedAtUtc, options.CleanupInterval))
        {
            return 0;
        }

        var retryImmediately = true;
        try
        {
            var deleted = await eventJournal.DeleteExpiredTerminalEventsAsync(cancellationToken)
                .ConfigureAwait(false);
            retryImmediately = deleted == options.CleanupBatchSize;
            return deleted;
        }
        finally
        {
            schedule.Complete(
                identity,
                timeProvider.GetUtcNow(),
                options.CleanupInterval,
                retryImmediately);
        }
    }
}
