using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.SharedKernel;
using static CanDoItAll.Tests.Integration.Runtime.FileAgentPlanningPublicationSnapshotTests;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "HostPlatform")]
[Trait("Category", "FileSystemPortability")]
public sealed class AgentInheritedPlanningReviewTests {
    [Theory]
    [InlineData(AgentPlanningReviewChoice.Approve, ReadBasisMode.RunSnapshot)]
    [InlineData(AgentPlanningReviewChoice.Reject, ReadBasisMode.RunSnapshot)]
    [InlineData(AgentPlanningReviewChoice.Approve, ReadBasisMode.StepSnapshot)]
    [InlineData(AgentPlanningReviewChoice.Reject, ReadBasisMode.StepSnapshot)]
    public async Task Original_choice_is_durable_and_replays_after_recreating_the_owner_without_dispatch(
        AgentPlanningReviewChoice choice, ReadBasisMode mode) {
        await using var fixture = await AgentInheritedPlanningReviewFixture.CreateAsync(mode);
        fixture.Source.Clock.Advance(TimeSpan.FromMinutes(2));
        var request = fixture.Request(choice);
        var first = await fixture.ReviewAsync(request);
        Assert.False(first.Replayed);
        Assert.Equal(choice == AgentPlanningReviewChoice.Approve ? RunOutcome.Succeeded : RunOutcome.Cancelled, first.Run.Outcome);
        Assert.Null(first.Run.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Equal(TimeSpan.FromSeconds(choice == AgentPlanningReviewChoice.Approve ? 2 : 1).Ticks,
            first.Run.ToolAdmission.PlanningDecision.Work.ClosedWorkTicks);
        var original = (await fixture.Source.Files.NewStore().GetExecutionRunDetailAsync(first.Run.Id, fixture.Token))!;
        Assert.Null(original.ChatSession);
        var receipt = Assert.Single(original.Approvals);
        Assert.Equal(AgentPlanningReviewIntent.SourceKind, receipt.DecisionSourceKind);
        Assert.Equal(fixture.Binding, receipt.ToolAdmission);
        var replay = await fixture.ReviewAsync(request, fresh: true);
        Assert.True(replay.Replayed);
        Assert.Equal(first.Run.Revision, replay.Run.Revision);
        Assert.Equal(choice == AgentPlanningReviewChoice.Approve ? 1 : 0, fixture.Dispatches);
    }

    [Theory]
    [InlineData(ChangedIntent.Actor)]
    [InlineData(ChangedIntent.Action)]
    [InlineData(ChangedIntent.Choice)]
    [InlineData(ChangedIntent.Revision)]
    [InlineData(ChangedIntent.Approval)]
    [InlineData(ChangedIntent.Binding)]
    public async Task A_different_actor_action_or_display_cannot_adopt_the_retained_choice(ChangedIntent change) {
        await using var fixture = await AgentInheritedPlanningReviewFixture.CreateAsync();
        var request = fixture.Request(AgentPlanningReviewChoice.Reject);
        var saved = await fixture.ReviewAsync(request);
        var expected = request.Expected;
        if (change == ChangedIntent.Actor) {
            fixture.OwnerIntent = AgentToolProtocolEnvelope.ComputeDigest("different-manager");
        }
        var altered = new AgentInheritedPlanningReviewRequest(change == ChangedIntent.Action ? Guid.NewGuid() : request.ActionId,
            change == ChangedIntent.Choice ? AgentPlanningReviewChoice.Approve : request.Choice,
            change == ChangedIntent.Revision ? new(expected.ExecutionRunId, expected.RunRevision + 1, expected.JournalRevision,
                expected.DecisionId, expected.Generation) : expected,
            change == ChangedIntent.Binding ? request.Binding with { IntentId = new(Guid.NewGuid()) } : request.Binding,
            change == ChangedIntent.Approval ? Guid.NewGuid().ToString("D") : request.ApprovalId);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.ReviewAsync(altered, fresh: true));
        Assert.Equal(saved.Run.Revision, (await fixture.Source.ReadAsync()).Revision);
        Assert.Equal(0, fixture.Dispatches);
    }

    [Theory]
    [InlineData(AgentPlanningReviewChoice.Approve)]
    [InlineData(AgentPlanningReviewChoice.Reject)]
    public async Task Legacy_review_capability_without_current_manager_observation_cannot_accept_a_choice(AgentPlanningReviewChoice choice) {
        await using var fixture = await AgentInheritedPlanningReviewFixture.CreateAsync();
        var request = fixture.Request(choice);
        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.Coordinator().ReviewAsync(request,
            fixture.Authority(request, currentManager: false), fixture.OwnerIntent, fixture, fixture.Token));
        await RequireUnchangedAsync(fixture);
    }

    [Theory]
    [InlineData(AgentPlanningReviewChoice.Approve, false)]
    [InlineData(AgentPlanningReviewChoice.Reject, false)]
    [InlineData(AgentPlanningReviewChoice.Approve, true)]
    [InlineData(AgentPlanningReviewChoice.Reject, true)]
    public async Task Revocation_during_dispatch_or_publication_key_wait_refuses_intent(
        AgentPlanningReviewChoice choice, bool publicationKey) {
        await using var fixture = await AgentInheritedPlanningReviewFixture.CreateAsync();
        var request = fixture.Request(choice);
        var layout = new FileSandboxWorkspaceStorageLayout(fixture.Source.Files.WorkspaceRoot, fixture.Source.Files.StorageScope);
        await using var holder = publicationKey
            ? await new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory()).AcquireCoordinationAsync(layout.RootPath,
                FileSandboxWorkspaceWriteSet.Run(fixture.Waiting, fixture.Waiting, false).LockPaths(layout).Single(),
                TimeSpan.FromSeconds(5), false, fixture.Token)
            : await fixture.Source.Files.NewStore().AcquireToolDispatchLeaseAsync(fixture.Waiting.Id, fixture.Token);
        using var wait = new KeyWait();
        var checkedBeforeLease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.OnManagerCheck = count => {
            if (count == 2) {
                checkedBeforeLease.TrySetResult();
            }
        };
        var pending = fixture.ReviewAsync(request);
        Exception? observationFailure;
        try {
            observationFailure = await Record.ExceptionAsync(async () => {
                await (publicationKey ? wait.Entered.Task : checkedBeforeLease.Task).WaitAsync(fixture.Token);
                Assert.False(pending.IsCompleted);
                fixture.ManagerAllowed = false;
            });
        } finally {
            await holder.DisposeAsync();
        }
        var failure = await Record.ExceptionAsync(() => pending);
        Assert.Null(observationFailure);
        Assert.IsType<AgentToolReceiptAccessDeniedException>(failure);
        await RequireUnchangedAsync(fixture);
    }

    [Theory]
    [InlineData(AgentPlanningReviewChoice.Approve, false)]
    [InlineData(AgentPlanningReviewChoice.Reject, false)]
    [InlineData(AgentPlanningReviewChoice.Approve, true)]
    [InlineData(AgentPlanningReviewChoice.Reject, true)]
    public async Task Lost_approval_acknowledgement_recovers_intent_without_entering_the_original_effect(
        AgentPlanningReviewChoice choice, bool afterApproval) {
        await using var fixture = await AgentInheritedPlanningReviewFixture.CreateAsync();
        var request = fixture.Request(choice);
        fixture.Source.Fault = afterApproval ? ExistingRunDetailCommitStage.ApprovalRecordsPersisted : ExistingRunDetailCommitStage.JournalPersisted;
        await Assert.ThrowsAsync<IOException>(() => fixture.ReviewAsync(request));
        var replay = await fixture.ReviewAsync(request, fresh: true);
        Assert.True(replay.Replayed);
        Assert.Equal(0, fixture.Dispatches);
        if (choice == AgentPlanningReviewChoice.Approve) {
            Assert.Null(replay.Run.Outcome);
            Assert.NotNull(replay.Run.ToolAdmission!.PlanningDecision!.Work.Open);
            Assert.Null(AgentToolJournalTransitions.RequireProposal(replay.Run.ToolAdmission, fixture.Binding).DispatchClaimId);
        } else {
            Assert.Equal(RunOutcome.Cancelled, replay.Run.Outcome);
            Assert.Null(replay.Run.ToolAdmission!.PlanningDecision!.Work.Open);
        }
    }

    [Fact]
    public async Task Failure_before_effect_entry_keeps_truthful_open_work_and_never_redispatches_on_replay() {
        await using var fixture = await AgentInheritedPlanningReviewFixture.CreateAsync();
        fixture.StopBeforeClaim = true;
        var request = fixture.Request(AgentPlanningReviewChoice.Approve);
        await Assert.ThrowsAsync<IOException>(() => fixture.ReviewAsync(request));
        fixture.StopBeforeClaim = false;
        var replay = await fixture.ReviewAsync(request, fresh: true);
        Assert.True(replay.Replayed);
        Assert.Null(replay.Run.Outcome);
        Assert.NotNull(replay.Run.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Equal(1, fixture.Dispatches);
    }

    [Fact]
    public async Task Manager_revocation_after_effect_does_not_abandon_settlement_but_refuses_disclosure_until_reauthorized() {
        await using var fixture = await AgentInheritedPlanningReviewFixture.CreateAsync();
        var request = fixture.Request(AgentPlanningReviewChoice.Approve);
        fixture.AfterDispatch = () => fixture.ManagerAllowed = false;
        var settled = false;
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.Coordinator().ReviewWithinLifecycleAsync(
            request, fixture.Authority(request), fixture.OwnerIntent, fixture, (result, token) => {
                token.ThrowIfCancellationRequested();
                Assert.Equal(RunOutcome.Succeeded, result.Run.Outcome);
                Assert.Null(result.Run.ToolAdmission!.PlanningDecision!.Work.Open);
                Assert.False(fixture.ManagerAllowed);
                settled = true;
                return Task.FromResult(result);
            }, fixture.Token));
        Assert.True(settled);
        var completed = await fixture.Source.ReadAsync();
        Assert.Equal(RunOutcome.Succeeded, completed.Outcome);
        Assert.Null(completed.ToolAdmission!.PlanningDecision!.Work.Open);
        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.ReviewAsync(request, fresh: true));
        fixture.ManagerAllowed = true;
        var replay = await fixture.ReviewAsync(request, fresh: true);
        Assert.True(replay.Replayed);
        Assert.Equal(completed.Revision, replay.Run.Revision);
        Assert.Equal(1, fixture.Dispatches);
    }

    [Fact]
    public async Task Caller_cancellation_after_effect_does_not_cancel_original_parent_settlement() {
        await using var fixture = await AgentInheritedPlanningReviewFixture.CreateAsync();
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var request = fixture.Request(AgentPlanningReviewChoice.Approve);
        fixture.AfterDispatch = caller.Cancel;
        var settled = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Coordinator().ReviewWithinLifecycleAsync(
            request, fixture.Authority(request), fixture.OwnerIntent, fixture, (result, token) => {
                token.ThrowIfCancellationRequested();
                Assert.True(caller.IsCancellationRequested);
                Assert.Equal(RunOutcome.Succeeded, result.Run.Outcome);
                Assert.Null(result.Run.ToolAdmission!.PlanningDecision!.Work.Open);
                settled = true;
                return Task.FromResult(result);
            }, caller.Token));
        Assert.True(settled);
        var replay = await fixture.ReviewAsync(request, fresh: true);
        Assert.True(replay.Replayed);
        Assert.Equal(RunOutcome.Succeeded, replay.Run.Outcome);
        Assert.Equal(1, fixture.Dispatches);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Terminal_state_with_missing_receipt_or_changed_original_target_cannot_be_adopted_as_replay(bool changedTarget) {
        await using var fixture = await AgentInheritedPlanningReviewFixture.CreateAsync();
        var request = fixture.Request(AgentPlanningReviewChoice.Reject);
        await fixture.ReviewAsync(request);
        var terminal = (await fixture.Source.Files.NewStore().GetExecutionRunDetailAsync(fixture.Waiting.Id, fixture.Token))!;
        var original = terminal.Run.ToolAdmission!.PlanningDecision!;
        var observed = changedTarget ? terminal with {
            Run = terminal.Run with { ToolAdmission = terminal.Run.ToolAdmission with {
                PlanningDecision = original with { OriginalTarget = AgentToolProtocolEnvelope.Create(
                    original.OriginalTarget.Format, original.OriginalTarget.Version, "{\"changed\":true}") }
            } }
        } : terminal with { Approvals = [] };
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Coordinator(reader: new ObservedAggregate(observed)).ReviewAsync(
            request, fixture.Authority(request), fixture.OwnerIntent, fixture, fixture.Token));
        Assert.Equal(0, fixture.Dispatches);
        Assert.Equal(terminal.Run.Revision, (await fixture.Source.ReadAsync()).Revision);
    }

    [Fact]
    public async Task Competing_choices_cannot_both_accept_the_original_pending_proposal() {
        await using var fixture = await AgentInheritedPlanningReviewFixture.CreateAsync();
        var approve = fixture.Request(AgentPlanningReviewChoice.Approve);
        var reject = fixture.Request(AgentPlanningReviewChoice.Reject);
        var results = await Task.WhenAll(AttemptAsync(approve), AttemptAsync(reject));
        Assert.Single(results, failure => failure is null);
        Assert.IsAssignableFrom<AgentToolAdmissionException>(Assert.Single(results, failure => failure is not null));
        var detail = (await fixture.Source.Files.NewStore().GetExecutionRunDetailAsync(fixture.Waiting.Id, fixture.Token))!;
        Assert.Single(detail.Approvals);
        Assert.True(fixture.Dispatches <= 1);

        async Task<Exception?> AttemptAsync(AgentInheritedPlanningReviewRequest request)
            => await Record.ExceptionAsync(() => fixture.ReviewAsync(request));
    }

    private static async Task RequireUnchangedAsync(AgentInheritedPlanningReviewFixture fixture) {
        var current = await fixture.Source.ReadAsync();
        Assert.Equal(AgentPlanningDecisionExpectation.From(fixture.Waiting), AgentPlanningDecisionExpectation.From(current));
        Assert.Null(current.ToolAdmission!.PlanningDecision!.Work.Open);
        Assert.Equal(0, fixture.Dispatches);
    }

    private sealed class ObservedAggregate(ExecutionRunDetail observed) : ISandboxWorkspaceExecutionRunStore {
        public Task<ExecutionRunDetail?> GetExecutionRunDetailAsync(Guid executionRunId, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(observed.Run.Id, executionRunId);
            return Task.FromResult<ExecutionRunDetail?>(observed);
        }
        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Review requires the selected aggregate.");
        public Task<ExecutionRunRecord?> GetExecutionRunAsync(Guid executionRunId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Review requires the selected approval receipt.");
        public Task<ExecutionRunDetail> SaveExecutionRunDetailAsync(ExecutionRunDetail detail, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("A missing receipt must not enter a write.");
    }

    public enum ChangedIntent { Actor, Action, Choice, Revision, Approval, Binding }
}
