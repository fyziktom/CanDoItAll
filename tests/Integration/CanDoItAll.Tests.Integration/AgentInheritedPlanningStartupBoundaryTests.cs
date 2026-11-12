using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class AgentInheritedPlanningStartupBoundaryTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exhaustion_or_cancellation_after_opening_settles_work_without_entering_startup(bool cancel) {
        await using var fixture = await AgentInheritedPlanningCoordinatorFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var elapsed = TimeSpan.FromSeconds(cancel ? 2 : 31);
        var clock = new OpeningClock(fixture.Source.Clock, () => {
            fixture.Source.Clock.Advance(elapsed);
            if (cancel) {
                cancellation.Cancel();
            }
        });
        var store = fixture.Source.Files.NewStore();
        var journal = new AgentToolAdmissionJournal(store, fixture.Source.Files.Profile, clock,
            backgroundSources: [fixture.Source.Policy]);
        var coordinator = new AgentInheritedPlanningCoordinator(store, journal, fixture.Runtime, [fixture.Module]);
        var preparationEntered = false;
        var lifecycleEntered = false;
        var failure = await Record.ExceptionAsync(() => coordinator.RunPreparingWithinLifecycleAsync(fixture.Expected,
            (_, _) => {
                preparationEntered = true;
                return Task.FromResult((fixture.Agent, fixture.Provider));
            }, (execution, token) => {
                lifecycleEntered = true;
                return execution.RunAsync(token);
            }, cancellation.Token));
        if (cancel) {
            Assert.IsAssignableFrom<OperationCanceledException>(failure);
        } else {
            Assert.Equal(AgentPlanningAdmissionRefusal.WorkBudgetExhausted,
                Assert.IsType<AgentPlanningAdmissionException>(failure).Reason);
        }
        Assert.True(clock.ReachedBoundary);
        Assert.False(preparationEntered);
        Assert.False(lifecycleEntered);
        var closed = await fixture.ReadAsync();
        Assert.Equal(cancel ? RunOutcome.Cancelled : RunOutcome.Failed, closed.Outcome);
        Assert.Equal(elapsed.Ticks, closed.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
        Assert.Null(closed.ToolAdmission.PlanningDecision.Work.Open);
        Assert.True(closed.ToolAdmission.ProviderDispatches.IsDefaultOrEmpty);
        Assert.Empty(fixture.Wire.Requests);
        Assert.Null(AgentToolRunLease.Current);
    }

    private sealed class OpeningClock(TimeProvider original, Action onBoundary) : TimeProvider {
        private int reached;
        internal bool ReachedBoundary => Volatile.Read(ref reached) != 0;
        public override long TimestampFrequency => original.TimestampFrequency;
        public override DateTimeOffset GetUtcNow() => original.GetUtcNow();
        public override long GetTimestamp() {
            if (AgentToolRunLease.Current?.PlanningWork is not null && Interlocked.Exchange(ref reached, 1) == 0) {
                onBoundary();
            }
            return original.GetTimestamp();
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => original.CreateTimer(callback, state, dueTime, period);
    }
}
