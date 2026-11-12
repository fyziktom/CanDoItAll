namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanScenarioClockTests {
    [Theory]
    [InlineData(0)]
    [InlineData(120)]
    public void Resuming_live_time_preserves_monotonic_time_after_fast_or_slow_setup(int realSeconds) {
        var source = new LiveClock();
        var clock = new ProjectTaskPlanScenarioClock(source);
        clock.Freeze();
        clock.Advance(TimeSpan.FromSeconds(1));
        var frozenUtc = clock.GetUtcNow();
        var frozenTimestamp = clock.GetTimestamp();
        source.Advance(TimeSpan.FromSeconds(realSeconds));

        clock.Resume();

        var resumedUtc = clock.GetUtcNow();
        var resumedTimestamp = clock.GetTimestamp();
        Assert.Equal(realSeconds == 0 ? frozenUtc : source.GetUtcNow(), resumedUtc);
        Assert.Equal(Math.Max(frozenTimestamp, source.GetTimestamp()), resumedTimestamp);
        source.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(resumedUtc.AddSeconds(2), clock.GetUtcNow());
        Assert.Equal(TimeSpan.FromSeconds(2), clock.GetElapsedTime(resumedTimestamp));
        clock.Freeze();
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(resumedUtc.AddSeconds(3), clock.GetUtcNow());
    }

    [Fact]
    public void Resuming_requires_frozen_timers_to_be_disposed() {
        var clock = new ProjectTaskPlanScenarioClock();
        Assert.Throws<InvalidOperationException>(clock.Resume);
        clock.Freeze();
        using var timer = clock.CreateTimer(_ => { }, null, TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
        Assert.Throws<InvalidOperationException>(clock.Resume);
        timer.Dispose();
        clock.Resume();
        Assert.Throws<InvalidOperationException>(() => clock.Advance(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Resumed_deadline_runs_without_manual_clock_advancement() {
        var clock = new ProjectTaskPlanScenarioClock();
        clock.Freeze();
        clock.Advance(TimeSpan.FromSeconds(1));
        clock.Resume();
        using var deadline = new CancellationTokenSource(TimeSpan.Zero, clock);
        var waiting = Task.Delay(Timeout.InfiniteTimeSpan, deadline.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(waiting.IsCanceled);
    }

    [Fact]
    public async Task Real_periodic_timer_coalesces_elapsed_ticks_and_releases_its_waiter_on_disposal() {
        var clock = new ProjectTaskPlanScenarioClock();
        clock.Freeze();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), clock);
        var first = timer.WaitForNextTickAsync().AsTask();
        Assert.False(first.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(await first.WaitAsync(TimeSpan.FromSeconds(5)));
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.True(await timer.WaitForNextTickAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        var next = timer.WaitForNextTickAsync().AsTask();
        Assert.False(next.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.False(next.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(500));
        Assert.True(await next.WaitAsync(TimeSpan.FromSeconds(5)));
        var pending = timer.WaitForNextTickAsync().AsTask();
        timer.Dispose();
        Assert.False(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        clock.Advance(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Periodic_observation_preserves_one_shot_work_deadlines_and_change_semantics() {
        var clock = new ProjectTaskPlanScenarioClock();
        clock.Freeze();
        var periodicTicks = 0;
        var deadlineTicks = 0;
        using var periodic = clock.CreateTimer(_ => periodicTicks++, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        using var deadline = clock.CreateTimer(_ => deadlineTicks++, null, TimeSpan.FromSeconds(3), Timeout.InfiniteTimeSpan);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, periodicTicks);
        Assert.Equal(0, deadlineTicks);
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.Equal(2, periodicTicks);
        Assert.Equal(1, deadlineTicks);
        Assert.True(periodic.Change(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, periodicTicks);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(3, periodicTicks);
        Assert.Equal(1, deadlineTicks);
        periodic.Dispose();
        Assert.False(periodic.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1)));
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(3, periodicTicks);
    }

    [Fact]
    public void Invalid_period_or_deadline_cannot_leave_an_active_timer() {
        var clock = new ProjectTaskPlanScenarioClock();
        clock.Freeze();
        var ticks = 0;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            clock.CreateTimer(_ => ticks++, null, TimeSpan.FromSeconds(-2), TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            clock.CreateTimer(_ => ticks++, null, TimeSpan.Zero, TimeSpan.FromSeconds(-2)));
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(0, ticks);
    }

    private sealed class LiveClock : TimeProvider {
        private DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override DateTimeOffset GetUtcNow() => now;
        public override long GetTimestamp() => timestamp;
        internal void Advance(TimeSpan duration) {
            now += duration;
            timestamp += duration.Ticks;
        }
    }
}
