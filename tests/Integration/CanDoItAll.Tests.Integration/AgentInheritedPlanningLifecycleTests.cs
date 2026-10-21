using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class AgentInheritedPlanningLifecycleTests {
    [Fact]
    public async Task Recovered_startup_runs_inside_the_original_child_interval_and_is_included_in_closed_work() {
        await using var fixture = await AgentInheritedPlanningCoordinatorFixture.CreateAsync();
        fixture.Wire.OnRequest = _ => {
            fixture.Source.Clock.Advance(TimeSpan.FromSeconds(1));
            return Task.CompletedTask;
        };
        var result = await fixture.Coordinator().RunPreparingWithinLifecycleAsync(fixture.Expected, async (live, token) => {
            token.ThrowIfCancellationRequested();
            Assert.NotNull(AgentToolRunLease.Current);
            var retained = await fixture.ReadAsync();
            Assert.Equal(live.Revision, retained.Revision);
            Assert.Equal(live.ToolAdmission!.PlanningDecision!.Work.Open, retained.ToolAdmission!.PlanningDecision!.Work.Open);
            Assert.NotNull(retained.ToolAdmission.PlanningDecision.Work.Open);
            fixture.Source.Clock.Advance(TimeSpan.FromSeconds(2));
            return (fixture.Agent, fixture.Provider);
        }, (execution, token) => execution.RunAsync(token), default);
        Assert.Equal(TimeSpan.FromSeconds(3).Ticks, result.Run.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
        Assert.Null(result.Run.ToolAdmission.PlanningDecision.Work.Open);
        Assert.Single(fixture.Wire.Requests);
        Assert.Null(AgentToolRunLease.Current);
    }

    [Theory]
    [InlineData(StartupFailure.Read)]
    [InlineData(StartupFailure.ContractChanged)]
    [InlineData(StartupFailure.Overrun)]
    public async Task Closed_recovery_startup_failure_settles_work_before_any_provider_or_completion(StartupFailure failure) {
        await using var fixture = await AgentInheritedPlanningCoordinatorFixture.CreateAsync();
        var completed = false;
        var elapsed = TimeSpan.FromSeconds(failure == StartupFailure.Overrun ? 31 : 2);
        var error = await Record.ExceptionAsync(() => fixture.Coordinator().RunPreparingWithinLifecycleAsync(fixture.Expected,
            (_, _) => {
                Assert.NotNull(AgentToolRunLease.Current?.PlanningWork);
                fixture.Source.Clock.Advance(elapsed);
                if (failure == StartupFailure.Read) {
                    throw new IOException("The owned startup read closed with failure.");
                }
                return Task.FromResult((fixture.Agent, failure == StartupFailure.ContractChanged
                    ? fixture.Provider with { BaseUrl = "https://changed.example.test/v1" } : fixture.Provider));
            }, (execution, token) => {
                completed = true;
                return execution.RunAsync(token);
            }, default));
        if (failure == StartupFailure.Read) {
            Assert.IsType<IOException>(error);
        } else if (failure == StartupFailure.ContractChanged) {
            Assert.Equal("planning.runtime-contract-changed", Assert.IsType<AgentToolAdmissionException>(error).Code);
        } else {
            Assert.Equal(AgentPlanningAdmissionRefusal.WorkBudgetExhausted, Assert.IsType<AgentPlanningAdmissionException>(error).Reason);
        }
        var closed = await fixture.ReadAsync();
        Assert.Equal(RunOutcome.Failed, closed.Outcome);
        Assert.Equal(elapsed.Ticks, closed.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
        Assert.Null(closed.ToolAdmission.PlanningDecision.Work.Open);
        Assert.True(closed.ToolAdmission.ProviderDispatches.IsDefaultOrEmpty);
        Assert.Empty(fixture.Wire.Requests);
        Assert.False(completed);
        Assert.Null(AgentToolRunLease.Current);
    }

    public enum StartupFailure { Read, ContractChanged, Overrun }

    [Fact]
    public async Task Live_capability_is_single_use_and_invalid_after_lifecycle_closes() {
        await using var fixture = await AgentInheritedPlanningCoordinatorFixture.CreateAsync();
        AgentInheritedPlanningExecution? retained = null;
        await fixture.Coordinator().RunWithinLifecycleAsync(fixture.Agent, fixture.Provider, fixture.Expected, async (execution, token) => {
            retained = execution;
            var result = await execution.RunAsync(token);
            var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => execution.RunAsync(token));
            Assert.Equal("planning.execution-already-started", failure.Code);
            Assert.NotNull(AgentToolRunLease.Current);
            return result;
        });
        Assert.Null(AgentToolRunLease.Current);
        var disposed = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => retained!.RunAsync());
        Assert.Equal("tool-admission.lease-lost", disposed.Code);
        Assert.Single(fixture.Wire.Requests);
    }

    [Fact]
    public async Task Competing_start_never_enters_the_failure_writing_lifecycle() {
        await using var fixture = await AgentInheritedPlanningCoordinatorFixture.CreateAsync();
        await using var live = await new AgentPlanningDecisionOwner(fixture.Source.Journal).StartInheritedAsync(fixture.Expected,
            AgentInheritedPlanningFixture.AllowRead, default);
        var original = await fixture.ReadAsync();
        var entered = false;
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Coordinator().RunWithinLifecycleAsync(fixture.Agent, fixture.Provider,
            fixture.Expected, (_, _) => {
                entered = true;
                return Task.FromResult(0);
            }));
        Assert.False(entered);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(await fixture.ReadAsync()));
        Assert.Empty(fixture.Wire.Requests);
    }

    [Fact]
    public async Task Lifecycle_failure_releases_lease_but_preserves_unknown_interval() {
        await using var fixture = await AgentInheritedPlanningCoordinatorFixture.CreateAsync();
        await Assert.ThrowsAsync<IOException>(() => fixture.Coordinator().RunWithinLifecycleAsync<int>(fixture.Agent, fixture.Provider,
            fixture.Expected, (_, _) => Task.FromException<int>(new IOException("completion-boundary"))));
        Assert.Null(AgentToolRunLease.Current);
        var original = await fixture.ReadAsync();
        Assert.NotNull(original.ToolAdmission!.PlanningDecision!.Work.Open);
        var failure = await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => fixture.RunAsync(AgentPlanningDecisionExpectation.From(original)));
        Assert.Equal(AgentPlanningAdmissionRefusal.InterruptedWorkAccountingUnavailable, failure.Reason);
        Assert.Empty(fixture.Wire.Requests);
    }
}
