using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Unit.ProcessDecisionReviewTestData;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDecisionRefreshAccountingTests {
    [Theory]
    [InlineData(ProcessDecisionChildTerminalStatus.Succeeded, ProcessRuntimeStatus.Completed)]
    [InlineData(ProcessDecisionChildTerminalStatus.Failed, ProcessRuntimeStatus.Failed)]
    [InlineData(ProcessDecisionChildTerminalStatus.Cancelled, ProcessRuntimeStatus.Cancelled)]
    public async Task An_accepted_effect_completes_its_original_result_and_exact_work(ProcessDecisionChildTerminalStatus status,
        ProcessRuntimeStatus expected) {
        var fixture = new Fixture();
        fixture.Data.Outcome = fixture.Data.Closed with { Status = status };
        var result = Assert.IsType<ProcessDecisionRefreshAccountingResult.CompletedOriginal>(await fixture.ReconcileAsync()).Commit;
        Assert.Equal(expected, result.State.Status);
        Assert.Equal(fixture.Data.Closed.Work, result.DecisionBudget!.Change.Allocation.Settlement);
        Assert.Equal(fixture.Data.Closed.Closure, result.Decision!.Child!.Closure);
        Assert.Equal(fixture.AdmissionId, result.Decision.Current.RefreshCommandId);
        Assert.Equal(fixture.Data.State.Claims, result.State.Claims);
        Assert.Equal(fixture.Data.State.AppliedResults[0], result.State.AppliedResults[0]);
        Assert.All(result.Events, item => Assert.Equal(fixture.Intent.Actor, item.Actor));
        Assert.Equal(2, result.OutboxMessages.Count);
        Assert.Equal(1, fixture.Data.OutcomeReads);
        Assert.Equal(1, fixture.Data.Commits);
    }

    [Fact]
    public async Task No_effect_settles_actual_work_without_completing_or_reopening_the_Process() {
        var fixture = new Fixture();
        fixture.NoEffect();
        var result = Assert.IsType<ProcessDecisionRefreshAccountingResult.Reconciled>(await fixture.ReconcileAsync()).Commit;
        Assert.Equal(fixture.Data.State, result.State with { UpdatedAtUtc = fixture.Data.State.UpdatedAtUtc });
        Assert.Equal(ProcessDecisionPhase.Reconciling, result.Decision!.Current.Phase);
        Assert.Equal(0, result.Decision.Current.UnclosedChildCount);
        Assert.Equal(fixture.Data.Closed.Work, result.DecisionBudget!.Change.Allocation.Settlement);
        Assert.Equal(fixture.Data.Closed.Work.Consumed.WorkTicks, result.DecisionBudget.Change.Balance.ConsumedWorkTicks);
        Assert.Equal(0, result.DecisionBudget.Change.Balance.ReservedWorkTicks);
        Assert.Empty(result.Events);
        Assert.Null(result.DecisionRunScope);
    }

    [Theory]
    [InlineData(ProcessDecisionChildPendingReason.MissingJournal)]
    [InlineData(ProcessDecisionChildPendingReason.ActiveWork)]
    [InlineData(ProcessDecisionChildPendingReason.ApprovalRequired)]
    [InlineData(ProcessDecisionChildPendingReason.BasisChanged)]
    [InlineData(ProcessDecisionChildPendingReason.UnresolvedEffects)]
    [InlineData(ProcessDecisionChildPendingReason.ExecutionIncomplete)]
    public async Task Pending_owner_work_preserves_its_reason_and_grant(ProcessDecisionChildPendingReason reason) {
        var fixture = new Fixture();
        fixture.Data.Outcome = new ProcessDecisionChildResult.Pending(reason);
        var result = Assert.IsType<ProcessDecisionRefreshAccountingResult.Pending>(await fixture.ReconcileAsync());
        Assert.Equal(reason, result.Reason);
        Assert.Equal(0, fixture.Data.Commits);
        Assert.Equal(1, fixture.Data.OutcomeReads);
        Assert.Null(fixture.Data.Selection.Child!.Observation.Settlement);
    }

    [Fact]
    public async Task Unreadable_owner_evidence_stays_pending_without_accounting() {
        var fixture = new Fixture();
        fixture.Data.OutcomeFailure = new IOException("Original receipt unavailable.");
        Assert.Equal(ProcessDecisionChildPendingReason.UnresolvedEffects,
            Assert.IsType<ProcessDecisionRefreshAccountingResult.Pending>(await fixture.ReconcileAsync()).Reason);
        Assert.Equal(0, fixture.Data.Commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retained_accounting_replays_before_pending_checks_or_another_owner_read(bool accepted) {
        var fixture = new Fixture();
        if (!accepted) {
            fixture.NoEffect();
        }
        await fixture.ReconcileAsync();
        fixture.Data.State = fixture.Data.State with { Status = ProcessRuntimeStatus.Cancelled };
        fixture.Data.Selection = fixture.Data.Selection with { OtherWorkPending = true };
        fixture.Data.OutcomeFailure = new IOException("A replay must not read this owner again.");
        var replay = await fixture.ReconcileAsync();
        var commit = accepted ? Assert.IsType<ProcessDecisionRefreshAccountingResult.CompletedOriginal>(replay).Commit :
            Assert.IsType<ProcessDecisionRefreshAccountingResult.Reconciled>(replay).Commit;
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, commit.Outcome);
        Assert.False(commit.CommittedNow);
        Assert.Equal(1, fixture.Data.Commits);
        Assert.Equal(1, fixture.Data.OutcomeReads);
    }

    public enum ChangedOriginal { Input, Binding, Source, Admission }

    [Theory]
    [InlineData(ChangedOriginal.Input)]
    [InlineData(ChangedOriginal.Binding)]
    [InlineData(ChangedOriginal.Source)]
    [InlineData(ChangedOriginal.Admission)]
    public async Task Changed_original_authority_is_refused_before_reading_the_owner(ChangedOriginal change) {
        var fixture = new Fixture();
        switch (change) {
            case ChangedOriginal.Input:
                fixture.Data.Input = fixture.Data.Input with { InputFingerprint = Hash('f') };
                break;
            case ChangedOriginal.Binding:
                var child = fixture.Data.Selection.Child!;
                fixture.Data.Selection = fixture.Data.Selection with { Child = child with {
                    Original = child.Original with { Material = child.Original.Material with { Fingerprint = new string('B', 64) } }
                } };
                break;
            case ChangedOriginal.Source:
                fixture.Data.Input = fixture.Data.Input with { Source = fixture.Data.Input.Source with { DatabaseProfileId = Id(80) } };
                break;
            case ChangedOriginal.Admission:
                fixture.AdmissionId = new(Id(81));
                break;
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.ReconcileAsync());
        Assert.Equal(0, fixture.Data.OutcomeReads);
        Assert.Equal(0, fixture.Data.Commits);
    }

    public enum ChangedPending { Generation, Budget, OtherWork, Root, Claim }

    [Theory]
    [InlineData(ChangedPending.Generation)]
    [InlineData(ChangedPending.Budget)]
    [InlineData(ChangedPending.OtherWork)]
    [InlineData(ChangedPending.Root)]
    [InlineData(ChangedPending.Claim)]
    public async Task Changed_pending_state_cannot_charge_the_original_grant(ChangedPending change) {
        var fixture = new Fixture();
        switch (change) {
            case ChangedPending.Generation:
                fixture.Data.Selection = fixture.Data.Selection with { Head = fixture.Data.Selection.Head! with { Revision = 8 } };
                break;
            case ChangedPending.Budget:
                fixture.Data.Selection = fixture.Data.Selection with { Budget = fixture.Data.Selection.Budget with { Revision = 8 } };
                break;
            case ChangedPending.OtherWork:
                fixture.Data.Selection = fixture.Data.Selection with { OtherWorkPending = true };
                break;
            case ChangedPending.Root:
                fixture.Data.State = fixture.Data.State with { Status = ProcessRuntimeStatus.CancelRequested };
                break;
            case ChangedPending.Claim:
                fixture.Data.State = fixture.Data.State with { Claims = [fixture.Data.State.Claims[0] with { Status = DispatchClaimStatus.Claimed }] };
                break;
        }
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => fixture.ReconcileAsync());
        Assert.Equal(0, fixture.Data.OutcomeReads);
        Assert.Equal(0, fixture.Data.Commits);
    }

    public enum ChangedMutation { Plan, Claim, History, Result, EventActor, EventPayload, Step, ExtraCommand, ProjectionPayload, ProjectionSubscriber }

    [Theory]
    [InlineData(ChangedMutation.Plan)]
    [InlineData(ChangedMutation.Claim)]
    [InlineData(ChangedMutation.History)]
    [InlineData(ChangedMutation.Result)]
    [InlineData(ChangedMutation.EventActor)]
    [InlineData(ChangedMutation.EventPayload)]
    [InlineData(ChangedMutation.Step)]
    [InlineData(ChangedMutation.ExtraCommand)]
    [InlineData(ChangedMutation.ProjectionPayload)]
    [InlineData(ChangedMutation.ProjectionSubscriber)]
    public void Accepted_completion_cannot_smuggle_other_runtime_or_event_changes(ChangedMutation change) {
        var fixture = new Fixture();
        var request = fixture.Prepare();
        var mutation = request.Mutation;
        var state = mutation.State;
        request = change switch {
            ChangedMutation.Plan => request with { Mutation = mutation with { State = state with { PlanHash = Hash('f') } } },
            ChangedMutation.Claim => request with { Mutation = mutation with { State = state with {
                Claims = [state.Claims[0] with { AttemptNumber = 9 }]
            } } },
            ChangedMutation.History => request with { Mutation = mutation with { State = state with {
                AppliedResults = [state.AppliedResults[0] with { ResultHash = Hash('f') }, state.AppliedResults[1]]
            } } },
            ChangedMutation.Result => request with { Mutation = mutation with { State = state with {
                AppliedResults = [state.AppliedResults[0], state.AppliedResults[1] with { ResultHash = Hash('f') }]
            } } },
            ChangedMutation.EventActor => request with { Mutation = mutation with { Events = [mutation.Events[0] with {
                Actor = new(ProcessEventActorKind.System, new("substituted"))
            }, mutation.Events[1]] } },
            ChangedMutation.EventPayload => request with { Mutation = mutation with { Events = [mutation.Events[0] with {
                PayloadHash = Hash('f')
            }, mutation.Events[1]] } },
            ChangedMutation.Step => request with { Mutation = mutation with { State = state with {
                Steps = [state.Steps[0] with { Status = ProcessRuntimeStepStatus.Ready }]
            } } },
            ChangedMutation.ExtraCommand => request with { ConfirmRootCancellation = true },
            ChangedMutation.ProjectionPayload => request with { Mutation = mutation with { OutboxMessages = [
                mutation.OutboxMessages[0] with { PayloadHash = Hash('f') }, mutation.OutboxMessages[1]
            ] } },
            ChangedMutation.ProjectionSubscriber => request with { Mutation = mutation with { OutboxMessages = [
                mutation.OutboxMessages[0] with { SubscriberKind = ProcessOutboxSubscriberKind.ExternalAdapter }, mutation.OutboxMessages[1]
            ] } },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Assert.Throws<InvalidOperationException>(() => request.RefreshAccounting!.RequireMutation(request, fixture.Intent));
    }

    [Fact]
    public void Accounting_identity_is_stable_and_distinct_from_admission_and_other_actions() {
        ProcessDecisionRefreshAccounting first = new(new(Id(80)));
        Assert.Equal(first.CommandId, new ProcessDecisionRefreshAccounting(first.AdmissionCommandId).CommandId);
        Assert.NotEqual(first.AdmissionCommandId, first.CommandId);
        Assert.NotEqual(first.CommandId, new ProcessDecisionRefreshAccounting(new(Id(81))).CommandId);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProcessDecisionRefreshAccounting(default).CommandId);
    }

    private sealed class Fixture : IProcessDecisionRefreshReceiptReader {
        internal Fixture() {
            var review = Data.Request;
            ProcessDecisionRefreshRequest request = new(new(Id(30)), review.Child, review.StrategyId, review.WaitingResultKey,
                review.Decision, review.Budget, null, ProcessDecisionRefreshKind.ManagerRefresh);
            var command = ProcessDecisionRefreshPreparation.Create(Data.State, Data.Input, Data.Selection, request, Data.Context.Actor);
            var head = ProcessDecisionRefreshAdmission.Begin(Data.State, Data.Selection.Head!, command, Data.Input.Assignment.DecisionDataPolicy!.Policy);
            Admission = new(new(command, true, head), null);
            AdmissionId = request.CommandId;
            Data.Selection = Data.Selection with { Head = head,
                Child = Data.Selection.Child! with { Observation = Data.Selection.Child!.Observation with { Generation = head } } };
        }

        internal ProcessDecisionReviewCoordinatorFixture Data { get; } = new();
        internal ProcessDecisionRefreshReceipt Admission { get; }
        internal ProcessDecisionRefreshIntent Intent => ((ProcessDecisionBeginRefresh)Admission.Decision.Command).Intent;
        internal RuntimeCommandId AdmissionId { get; set; }
        internal void NoEffect()
            => Data.Outcome = Data.Closed with { Status = ProcessDecisionChildTerminalStatus.Cancelled,
                Closure = Data.Closed.Closure with { Outcome = ProcessDecisionChildOutcome.NoEffect, AcceptedReceipt = null } };
        internal Task<ProcessDecisionRefreshAccountingResult> ReconcileAsync()
            => new ProcessDecisionRefreshAccountingCoordinator(Data, Data, this, [Data], Data, Data,
                NullLogger<ProcessDecisionRefreshAccountingCoordinator>.Instance).ReconcileAsync(Data.State, Data.Context, AdmissionId);
        internal ProcessRuntimeCommitRequest Prepare()
            => ProcessDecisionRefreshAccountingPreparation.Create(Data.State, Data.Context, Admission, Data.Selection.Child!,
                (ProcessDecisionChildResult.Closed)Data.Outcome);
        public Task<ProcessDecisionRefreshReceipt?> ReadAsync(ProcessRunId runId, RuntimeCommandId commandId, CancellationToken cancellationToken)
            => Task.FromResult<ProcessDecisionRefreshReceipt?>(Admission);
        public Task<ProcessDecisionRefreshReceipt?> ReadCurrentAsync(ProcessRunId runId, ProcessStepInstanceId stepId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
