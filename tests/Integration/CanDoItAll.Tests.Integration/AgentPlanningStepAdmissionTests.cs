using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class AgentPlanningStepAdmissionTests {
    [Fact]
    public async Task Original_expectation_claims_the_lease_and_opens_work_in_one_write() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        var original = await fixture.ReadAsync();
        fixture.OnCommit = stage => {
            if (stage == ExistingRunDetailCommitStage.RunPersisted) {
                fixture.Clock.Advance(TimeSpan.FromSeconds(3));
            }
        };
        await using var lease = await fixture.Owner.StartAsync(AgentPlanningDecisionExpectation.From(original), AllowRead, default);
        fixture.OnCommit = null;
        using var bound = lease.Bind();
        var work = Assert.IsType<AgentPlanningLiveWork>(lease.PlanningWork);
        var admitted = await fixture.ReadAsync();
        Assert.Equal(original.Revision + 1, admitted.Revision);
        Assert.Equal(original.ToolAdmission!.Revision + 1, admitted.ToolAdmission!.Revision);
        Assert.Equal(ExecutionState.Running, admitted.State);
        Assert.Equal(lease.Id, admitted.ToolAdmission.ActiveDispatchLeaseId);
        Assert.Equal(work.IntervalId, admitted.ToolAdmission.PlanningDecision!.Work.Open!.Id);
        Assert.Equal(work.StepId, Assert.Single(admitted.ToolAdmission.PlanningDecision.Current.Steps).Id);
        await fixture.CaptureAsync(work);
        var observation = await fixture.Owner.ObserveStepAsync(work, default);
        Assert.Equal(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(3), observation.RemainingWork);
        Assert.True(admitted.ToolAdmission.ProviderDispatches.IsDefaultOrEmpty);
    }

    [Fact]
    public async Task Separate_owners_racing_the_same_expectation_admit_exactly_one_step() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var original = await fixture.ReadAsync();
        var expected = AgentPlanningDecisionExpectation.From(original);
        var other = new AgentPlanningDecisionOwner(new(fixture.Files.NewStore(), fixture.Files.Profile, fixture.Clock));
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readers = 0;
        async ValueTask<bool> Read(AgentToolSessionObservation observation, CancellationToken token) {
            Assert.Equal(fixture.Files.Session, observation.Session.Reference);
            if (Interlocked.Increment(ref readers) == 2) {
                ready.SetResult();
            }
            await ready.Task.WaitAsync(token);
            return true;
        }
        var first = fixture.Owner.StartAsync(expected, Read, deadline.Token);
        var second = other.StartAsync(expected, Read, deadline.Token);
        var completed = await Task.WhenAny(first, second).WaitAsync(deadline.Token);
        await using var winner = await completed;
        var interval = winner.PlanningWork!.IntervalId;
        await winner.DisposeAsync();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => ReferenceEquals(completed, first) ? second : first);
        var retained = await fixture.ReadAsync();
        Assert.Equal(2, readers);
        Assert.Equal(original.Revision + 1, retained.Revision);
        Assert.Equal(original.ToolAdmission!.Revision + 1, retained.ToolAdmission!.Revision);
        Assert.Equal(interval, retained.ToolAdmission.PlanningDecision!.Work.Open!.Id);
        Assert.Single(retained.ToolAdmission.PlanningDecision.Current.Steps);
        Assert.True(retained.ToolAdmission.ProviderDispatches.IsDefaultOrEmpty);
    }

    [Fact]
    public async Task A_stale_expectation_is_not_upgraded_after_an_ordinary_lease_claim() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        var expected = AgentPlanningDecisionExpectation.From(await fixture.ReadAsync());
        await using (var previous = await fixture.Journal.AcquireRunAsync(fixture.Files.Session, default)) {
            Assert.NotEqual(Guid.Empty, previous.Id);
        }
        var before = await fixture.ReadAsync();
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Owner.StartAsync(expected, AllowRead, default));
        var retained = await fixture.ReadAsync();
        Assert.Equal(before.Revision, retained.Revision);
        Assert.Equal(before.ToolAdmission!.Revision, retained.ToolAdmission!.Revision);
        Assert.Empty(retained.ToolAdmission.PlanningDecision!.Current.Steps);
        Assert.Null(retained.ToolAdmission.PlanningDecision.Work.Open);
    }

    [Fact]
    public async Task Denied_current_read_never_opens_work_or_changes_the_run() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        var original = await fixture.ReadAsync();
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.Owner.StartAsync(
            AgentPlanningDecisionExpectation.From(original), (_, _) => ValueTask.FromResult(false), default));
        var retained = await fixture.ReadAsync();
        Assert.Equal(original.Revision, retained.Revision);
        Assert.Equal(original.ToolAdmission!.Revision, retained.ToolAdmission!.Revision);
        Assert.Null(retained.ToolAdmission.PlanningDecision!.Work.Open);
        Assert.Empty(retained.ToolAdmission.PlanningDecision.Current.Steps);
    }

    [Fact]
    public async Task Lost_start_acknowledgement_retains_work_and_refuses_a_replacement_interval() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        var expected = AgentPlanningDecisionExpectation.From(await fixture.ReadAsync());
        fixture.Fault = ExistingRunDetailCommitStage.RunPersisted;
        AgentToolAdmissionJournalFixture.RequireScriptedFault(await Assert.ThrowsAnyAsync<Exception>(() =>
            fixture.Owner.StartAsync(expected, AllowRead, default)), "planning-publication-ack");
        var original = await fixture.ReadAsync();
        var interval = original.ToolAdmission!.PlanningDecision!.Work.Open;
        Assert.NotNull(interval);
        var restarted = new AgentPlanningDecisionOwner(new(fixture.Files.NewStore(), fixture.Files.Profile, fixture.Clock));
        var failure = await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => restarted.StartAsync(
            AgentPlanningDecisionExpectation.From(original), AllowRead, default));
        Assert.Equal(AgentPlanningAdmissionRefusal.InterruptedWorkAccountingUnavailable, failure.Reason);
        var retained = await fixture.ReadAsync();
        Assert.Equal(original.Revision, retained.Revision);
        Assert.Equal(interval, retained.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Single(retained.ToolAdmission.PlanningDecision.Current.Steps);
        Assert.True(retained.ToolAdmission.ProviderDispatches.IsDefaultOrEmpty);
    }

    [Fact]
    public async Task Cancelling_while_waiting_for_the_dispatch_lease_does_not_publish_work() {
        await using var fixture = await AgentPlanningLiveOwnerFixture.CreateAsync();
        var original = await fixture.ReadAsync();
        await using var held = await ((ISandboxWorkspaceExecutionRunLeaseStore)fixture.Files.NewStore())
            .AcquireToolDispatchLeaseAsync(original.Id);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var checkedRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = fixture.Owner.StartAsync(AgentPlanningDecisionExpectation.From(original), (_, _) => {
            checkedRead.SetResult();
            return ValueTask.FromResult(true);
        }, deadline.Token);
        await checkedRead.Task.WaitAsync(deadline.Token);
        await deadline.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        var retained = await fixture.ReadAsync();
        Assert.Equal(original.Revision, retained.Revision);
        Assert.Equal(original.ToolAdmission!.Revision, retained.ToolAdmission!.Revision);
        Assert.Null(retained.ToolAdmission.PlanningDecision!.Work.Open);
        Assert.Empty(retained.ToolAdmission.PlanningDecision.Current.Steps);
    }

    private static ValueTask<bool> AllowRead(AgentToolSessionObservation _, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(true);
    }
}
