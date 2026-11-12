using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentInheritedPlanningStartupAccountingTests {
    [Fact]
    public void Initial_closed_work_survives_round_trip_and_reduces_the_original_child_grant() {
        var original = AgentInheritedPlanningTestData.Create().PlanningDecision!;
        var closed = AgentPlanningDecisionTransitions.CloseInitialPreparation(original, TimeSpan.FromSeconds(3),
            original.StartedAtUtc.AddSeconds(3));
        var saved = AgentPlanningDecisionCodec.Deserialize(AgentPlanningDecisionCodec.Serialize(closed));
        Assert.Equal(TimeSpan.FromSeconds(3).Ticks, saved.Work.ClosedWorkTicks);
        Assert.Equal(AgentPlanningWorkSettlementReason.InitialAdmission, saved.Work.LastSettlement!.Reason);
        Assert.Empty(saved.Current.Steps);
        Assert.Equal(original.InheritedOrigin, saved.InheritedOrigin);
        var live = AgentPlanningDecisionTransitions.BeginStep(saved, Guid.NewGuid(), saved.Work.LastObservedAtUtc);
        Assert.Equal(TimeSpan.FromSeconds(27), AgentPlanningDecisionTransitions.RemainingWork(live, TimeSpan.Zero, live.Work.LastObservedAtUtc));
        Assert.Throws<AgentPlanningAdmissionException>(() =>
            AgentPlanningDecisionTransitions.CloseInitialPreparation(saved, TimeSpan.FromSeconds(1), saved.Work.LastObservedAtUtc));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(31)]
    public void Exhausted_initial_work_is_retained_and_cannot_enter_a_provider_step(int seconds) {
        var original = AgentInheritedPlanningTestData.Create().PlanningDecision!;
        var elapsed = TimeSpan.FromSeconds(seconds);
        var closed = AgentPlanningDecisionTransitions.CloseInitialPreparation(original, elapsed, original.StartedAtUtc + elapsed);
        closed.Validate();
        Assert.Equal(elapsed.Ticks, closed.Work.ClosedWorkTicks);
        Assert.Equal(AgentPlanningGenerationState.Failed, closed.Current.State);
        Assert.Throws<AgentPlanningAdmissionException>(() =>
            AgentPlanningDecisionTransitions.BeginStep(closed, Guid.NewGuid(), closed.Work.LastObservedAtUtc));
    }

    [Fact]
    public void Interactive_work_cannot_claim_an_inherited_initial_settlement() {
        var interactive = AgentPlanningDecisionTestData.Create().PlanningDecision!;
        Assert.Throws<AgentPlanningAdmissionException>(() =>
            AgentPlanningDecisionTransitions.CloseInitialPreparation(interactive, TimeSpan.FromSeconds(1), interactive.StartedAtUtc.AddSeconds(1)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Invalid_clock_observation_does_not_produce_an_initial_work_receipt(bool negativeDuration) {
        var original = AgentInheritedPlanningTestData.Create().PlanningDecision!;
        var failure = Assert.Throws<AgentPlanningAdmissionException>(() => AgentPlanningDecisionTransitions.CloseInitialPreparation(original,
            TimeSpan.FromSeconds(negativeDuration ? -1 : 1), original.StartedAtUtc.AddSeconds(negativeDuration ? 1 : -1)));
        Assert.Equal(AgentPlanningAdmissionRefusal.ClockInvalid, failure.Reason);
        Assert.Null(original.Work.LastSettlement);
    }
}
