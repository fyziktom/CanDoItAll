namespace CanDoItAll.Tests.Integration;

internal sealed class ProjectTaskPlanScenarioClock : TimeProvider {
    private readonly Lock gate = new();
    private readonly List<ScenarioTimer> timers = [];
    private DateTimeOffset? frozen;
    private long timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow() {
        lock (gate) {
            return frozen ?? System.GetUtcNow();
        }
    }

    public override long GetTimestamp() {
        lock (gate) {
            return frozen is null ? global::System.Diagnostics.Stopwatch.GetElapsedTime(0).Ticks : timestamp;
        }
    }

    internal void Freeze() {
        lock (gate) {
            if (frozen is not null) {
                throw new InvalidOperationException("The scenario clock is already frozen.");
            }
            timestamp = GetTimestamp();
            frozen = System.GetUtcNow();
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
        lock (gate) {
            if (frozen is null) {
                return System.CreateTimer(callback, state, dueTime, period);
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
