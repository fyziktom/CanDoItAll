using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class AgentInheritedPlanningLifecycleTests {
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
