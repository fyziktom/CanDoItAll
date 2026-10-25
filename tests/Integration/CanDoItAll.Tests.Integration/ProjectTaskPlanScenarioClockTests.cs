namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanScenarioClockTests {
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
}
