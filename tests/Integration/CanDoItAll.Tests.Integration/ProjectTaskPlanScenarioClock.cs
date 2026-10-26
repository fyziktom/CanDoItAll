namespace CanDoItAll.Tests.Integration;

internal sealed class ProjectTaskPlanScenarioClock(TimeProvider? source = null) : TimeProvider {
    private readonly TimeProvider live = source ?? System;
    private readonly Lock gate = new();
    private readonly List<ScenarioTimer> timers = [];
    private DateTimeOffset? frozen;
    private long timestamp;
    private TimeSpan utcOffset;
    private long timestampOffset;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow() {
        lock (gate) {
            return frozen ?? live.GetUtcNow() + utcOffset;
        }
    }

    public override long GetTimestamp() {
        lock (gate) {
            return frozen is null ? live.GetElapsedTime(0).Ticks + timestampOffset : timestamp;
        }
    }

    internal void Freeze() {
        lock (gate) {
            if (frozen is not null) {
                throw new InvalidOperationException("The scenario clock is already frozen.");
            }
            timestamp = GetTimestamp();
            frozen = GetUtcNow();
        }
    }

    internal void Resume() {
        lock (gate) {
            if (frozen is not { } now) {
                throw new InvalidOperationException("The scenario clock is not frozen.");
            }
            if (timers.Count != 0) {
                throw new InvalidOperationException("Dispose all frozen timers before resuming live time.");
            }
            utcOffset = TimeSpan.FromTicks(Math.Max(0, (now - live.GetUtcNow()).Ticks));
            timestampOffset = Math.Max(0, timestamp - live.GetElapsedTime(0).Ticks);
            frozen = null;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
        lock (gate) {
            if (frozen is null) {
                return live.CreateTimer(callback, state, dueTime, period);
            }
            var timer = new ScenarioTimer(this, callback, state);
            timer.Change(dueTime, period);
            timers.Add(timer);
            return timer;
        }
    }

    internal void Advance(TimeSpan duration) {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        List<ScenarioTimer> due;
        lock (gate) {
            frozen = (frozen ?? throw new InvalidOperationException("Freeze the scenario clock before advancing it.")) + duration;
            timestamp = checked(timestamp + duration.Ticks);
            due = timers.Where(timer => timer.Due <= timestamp).ToList();
            foreach (var timer in due) {
                timer.Reschedule(timestamp);
            }
        }
        foreach (var timer in due) {
            timer.Fire();
        }
    }

    private sealed class ScenarioTimer(ProjectTaskPlanScenarioClock owner, TimerCallback callback, object? state) : ITimer {
        internal long Due { get; set; } = long.MaxValue;
        private bool disposed;
        private long periodTicks;

        public bool Change(TimeSpan dueTime, TimeSpan period) {
            if (dueTime < TimeSpan.Zero && dueTime != Timeout.InfiniteTimeSpan) {
                throw new ArgumentOutOfRangeException(nameof(dueTime));
            }
            if (period < TimeSpan.Zero && period != Timeout.InfiniteTimeSpan) {
                throw new ArgumentOutOfRangeException(nameof(period));
            }
            lock (owner.gate) {
                if (disposed) {
                    return false;
                }
                periodTicks = Math.Max(0, period.Ticks);
                Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : checked(owner.timestamp + dueTime.Ticks);
                return true;
            }
        }

        internal void Reschedule(long now)
            => Due = periodTicks == 0 ? long.MaxValue : checked(now + periodTicks - (now - Due) % periodTicks);

        internal void Fire() {
            lock (owner.gate) {
                if (disposed) {
                    return;
                }
            }
            callback(state);
        }

        public void Dispose() {
            lock (owner.gate) {
                disposed = true;
                Due = long.MaxValue;
                owner.timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync() {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
