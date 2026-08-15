using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatOperationEventRetentionSchedule
{
    private readonly object _gate = new();
    private readonly Dictionary<LlmChatRuntimeIdentity, DateTimeOffset> _nextRuns = [];

    public bool TryAcquire(
        LlmChatRuntimeIdentity runtimeIdentity,
        DateTimeOffset observedAtUtc,
        TimeSpan interval)
    {
        lock (_gate)
        {
            if (_nextRuns.TryGetValue(runtimeIdentity, out var nextRun) && nextRun > observedAtUtc)
            {
                return false;
            }

            _nextRuns[runtimeIdentity] = observedAtUtc + interval;
            return true;
        }
    }
}

public sealed class LlmChatOperationEventRetentionService(
    LlmChatOperationEventJournal eventJournal,
    LlmChatOperationEventRetentionSchedule schedule,
    ILlmChatOperationScopeAccessor operationScope,
    LlmChatStreamingOptions options,
    TimeProvider timeProvider)
{
    public Task<int> ApplyIfDueAsync(CancellationToken cancellationToken = default)
    {
        options.Validate();
        var identity = operationScope.Current?.RuntimeIdentity
            ?? throw new InvalidOperationException("An LLM Chat runtime operation scope is required for event retention.");
        return schedule.TryAcquire(identity, timeProvider.GetUtcNow(), options.CleanupInterval)
            ? eventJournal.DeleteExpiredTerminalEventsAsync(cancellationToken)
            : Task.FromResult(0);
    }
}
