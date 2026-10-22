using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class AgentInheritedParentStopTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Idle_approval_stop_rejects_only_undispatched_work_and_replays_without_a_new_revision(ReadBasisMode mode) {
        await using var source = await AgentInheritedPlanningFixture.CreateForPublicationAsync(parentMode: mode);
        var waiting = await source.WaitForApprovalAsync();
        var original = waiting.ToolAdmission!;
        var owner = Owner(source);
        var parent = Parent(waiting);
        Assert.Equal(AgentInheritedPlanningStopOutcome.Stopped, await owner.StopAsync(waiting.Id, parent));
        var stopped = await source.ReadAsync();
        Assert.Equal(RunOutcome.Cancelled, stopped.Outcome);
        Assert.Equal(AgentPlanningGenerationState.Cancelled, stopped.ToolAdmission!.PlanningDecision!.Current.State);
        Assert.Equal(original.PlanningDecision!.InheritedOrigin, stopped.ToolAdmission.PlanningDecision.InheritedOrigin);
        Assert.Equal(original.PlanningDecision.Work.ClosedWorkTicks, stopped.ToolAdmission.PlanningDecision.Work.ClosedWorkTicks);
        Assert.Null(stopped.ToolAdmission.PlanningDecision.Work.Open);
        Assert.Equal(JsonSerializer.Serialize(original.ProviderDispatches), JsonSerializer.Serialize(stopped.ToolAdmission.ProviderDispatches));
        Assert.Empty(stopped.PendingApprovals);
        Assert.All(stopped.ToolAdmission.Batches.SelectMany(batch => batch.Proposals), proposal => {
            Assert.Equal(AgentToolProposalState.Cancelled, proposal.State);
            Assert.Equal(ExecutionApprovalStatus.Rejected, proposal.ApprovalStatus);
            Assert.Equal(AgentToolEffectState.NotCommitted, proposal.EffectState);
            Assert.Equal(AgentToolCancellationDisposition.NotDispatched, proposal.Cancellation!.Disposition);
        });
        var retained = JsonSerializer.Serialize(await source.Files.NewStore().GetExecutionRunDetailAsync(waiting.Id));
        Assert.Equal(AgentInheritedPlanningStopOutcome.AlreadyClosed, await Owner(source).StopAsync(waiting.Id, parent));
        Assert.Equal(retained, JsonSerializer.Serialize(await source.Files.NewStore().GetExecutionRunDetailAsync(waiting.Id)));
    }

    [Fact]
    public async Task Another_owner_holding_the_real_dispatch_lease_remains_pending() {
        await using var source = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var waiting = await source.WaitForApprovalAsync();
        var before = JsonSerializer.Serialize(waiting);
        await using (var held = await source.Files.NewStore().AcquireToolDispatchLeaseAsync(waiting.Id)) {
            Assert.Equal(AgentInheritedPlanningStopOutcome.LeaseBusy, await Owner(source).StopAsync(waiting.Id, Parent(waiting)));
            Assert.Equal(before, JsonSerializer.Serialize(await source.ReadAsync()));
        }
        Assert.Equal(AgentInheritedPlanningStopOutcome.Stopped, await Owner(source).StopAsync(waiting.Id, Parent(waiting)));
    }

    [Fact]
    public async Task Caller_cancellation_during_the_real_lease_wait_is_propagated() {
        await using var source = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var waiting = await source.WaitForApprovalAsync();
        await using var held = await source.Files.NewStore().AcquireToolDispatchLeaseAsync(waiting.Id);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Owner(source).StopAsync(waiting.Id, Parent(waiting), cancellation.Token));
        Assert.Equal(waiting.Revision, (await source.ReadAsync()).Revision);
    }

    [Fact]
    public async Task Lost_file_commit_acknowledgement_recovers_the_original_stop() {
        await using var source = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var waiting = await source.WaitForApprovalAsync();
        var injected = false;
        var store = source.Files.NewStore(stage => {
            if (!injected && stage == ExistingRunDetailCommitStage.RunPersisted) {
                injected = true;
                throw new IOException("Lost parent-stop acknowledgement.");
            }
        });
        await Assert.ThrowsAsync<IOException>(() => Owner(source, store).StopAsync(waiting.Id, Parent(waiting)));
        Assert.True(injected);
        var recovered = await source.ReadAsync();
        Assert.Equal(RunOutcome.Cancelled, recovered.Outcome);
        Assert.Equal(AgentInheritedPlanningStopOutcome.AlreadyClosed, await Owner(source).StopAsync(waiting.Id, Parent(waiting)));
        Assert.Equal(recovered.Revision, (await source.ReadAsync()).Revision);
        Assert.Equal(waiting.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks, recovered.ToolAdmission!.PlanningDecision!.Work.ClosedWorkTicks);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Revoked_parent_after_lease_or_at_file_publication_preserves_the_original_journal(int boundary) {
        await using var source = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        var waiting = await source.WaitForApprovalAsync();
        var before = JsonSerializer.Serialize(await source.Files.NewStore().GetExecutionRunDetailAsync(waiting.Id));
        var observations = 0;
        var parent = new AgentInheritedParentStop(waiting.ToolAdmission!.PlanningDecision!.InheritedOrigin!, (_, token) => {
            token.ThrowIfCancellationRequested();
            if (++observations == boundary) {
                throw new UnauthorizedAccessException("Original parent capability changed.");
            }
            return Task.CompletedTask;
        });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Owner(source).StopAsync(waiting.Id, parent));
        Assert.Equal(boundary, observations);
        Assert.Equal(before, JsonSerializer.Serialize(await source.Files.NewStore().GetExecutionRunDetailAsync(waiting.Id)));
    }

    [Fact]
    public async Task Released_dispatch_lease_does_not_prove_that_interrupted_work_closed() {
        await using var source = await AgentInheritedPlanningFixture.CreateForPublicationAsync();
        await using (var lease = await source.StartAsync()) {
            using var bound = lease.Bind();
            await source.InheritAsync(lease.PlanningWork!);
            await source.EnterAsync(lease.PlanningWork!);
        }
        var interrupted = await source.ReadAsync();
        var before = JsonSerializer.Serialize(interrupted);
        Assert.Equal(AgentInheritedPlanningStopOutcome.OpenWork, await Owner(source).StopAsync(interrupted.Id, Parent(interrupted)));
        Assert.Equal(before, JsonSerializer.Serialize(await source.ReadAsync()));
        Assert.NotNull(interrupted.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.True(interrupted.ToolAdmission.HasUnresolvedProviderDispatch);
    }

    private static AgentInheritedPlanningStopper Owner(AgentInheritedPlanningFixture source, FileSandboxWorkspaceStore? store = null) {
        store ??= source.Files.NewStore();
        return new(store, store, store, new AgentExecutionCancellationRegistry(), source.Clock);
    }

    private static AgentInheritedParentStop Parent(ExecutionRunRecord original)
        => new(original.ToolAdmission!.PlanningDecision!.InheritedOrigin!, (_, token) => {
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });
}
