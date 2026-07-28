using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes.AgentChat;

internal sealed class ProcessAgentChatFreshnessLease : IDisposable
{
    private readonly object gate = new();
    private readonly TimeProvider timeProvider;
    private readonly Func<Task> recaptureAsync;
    private readonly Action<Exception> reportFailure;
    private ITimer? timer;
    private AgentChatContextAttachmentDraft? scheduledDraft;
    private long generation;
    private bool isDisposed;

    public ProcessAgentChatFreshnessLease(
        TimeProvider timeProvider,
        Func<Task> recaptureAsync,
        Action<Exception> reportFailure)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(recaptureAsync);
        ArgumentNullException.ThrowIfNull(reportFailure);
        this.timeProvider = timeProvider;
        this.recaptureAsync = recaptureAsync;
        this.reportFailure = reportFailure;
    }

    public void Schedule(AgentChatContextAttachmentDraft? draft)
    {
        var normalizedDeadlineUtc = draft?.FreshUntilUtc?.ToUniversalTime();
        ITimer? replacedTimer;

        lock (gate)
        {
            if (isDisposed)
            {
                return;
            }

            if (ReferenceEquals(scheduledDraft, draft) &&
                (timer is not null || draft is null))
            {
                return;
            }

            replacedTimer = timer;
            timer = null;
            scheduledDraft = draft;
            var scheduledGeneration = ++generation;
            if (normalizedDeadlineUtc is { } deadlineUtc)
            {
                var dueTime = deadlineUtc - timeProvider.GetUtcNow();
                if (dueTime < TimeSpan.Zero)
                {
                    dueTime = TimeSpan.Zero;
                }

                timer = timeProvider.CreateTimer(
                    static state =>
                    {
                        var timerState = (TimerState)state!;
                        timerState.Owner.OnTimerElapsed(timerState.Generation);
                    },
                    new TimerState(this, scheduledGeneration),
                    dueTime,
                    Timeout.InfiniteTimeSpan);
            }
        }

        replacedTimer?.Dispose();
    }

    public void Dispose()
    {
        ITimer? timerToDispose;

        lock (gate)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            generation++;
            scheduledDraft = null;
            timerToDispose = timer;
            timer = null;
        }

        timerToDispose?.Dispose();
    }

    private void OnTimerElapsed(long scheduledGeneration)
    {
        ITimer? elapsedTimer;

        lock (gate)
        {
            if (isDisposed || generation != scheduledGeneration)
            {
                return;
            }

            elapsedTimer = timer;
            timer = null;
            scheduledDraft = null;
        }

        elapsedTimer?.Dispose();
        _ = RecaptureAsync(scheduledGeneration);
    }

    private async Task RecaptureAsync(long scheduledGeneration)
    {
        lock (gate)
        {
            if (isDisposed || generation != scheduledGeneration)
            {
                return;
            }
        }

        try
        {
            await recaptureAsync();
        }
        catch (ObjectDisposedException exception)
        {
            lock (gate)
            {
                if (isDisposed)
                {
                    return;
                }
            }

            reportFailure(exception);
        }
        catch (Exception exception)
        {
            reportFailure(exception);
        }
    }

    private sealed record TimerState(
        ProcessAgentChatFreshnessLease Owner,
        long Generation);
}
