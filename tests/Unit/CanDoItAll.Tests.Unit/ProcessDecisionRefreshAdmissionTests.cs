using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Unit.ProcessDecisionReviewTestData;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDecisionRefreshAdmissionTests {
    [Theory]
    [InlineData(ProcessEventActorKind.Manager)]
    [InlineData(ProcessEventActorKind.User)]
    public async Task Admission_freezes_only_the_original_generation_without_spending_or_calling_the_child_owner(ProcessEventActorKind actor) {
        var fixture = new Fixture();
        fixture.Data.Context = fixture.Data.Context with { Actor = new(actor, fixture.Data.Context.Actor.Id) };
        var accepted = await fixture.AdmitAsync();
        var request = Assert.IsType<ProcessRuntimeCommitRequest>(fixture.LastCommit);
        Assert.True(accepted.CommittedNow);
        Assert.Equal(fixture.Request.CommandId, request.CommandId);
        Assert.Equal(fixture.Data.State, request.Mutation.State with { UpdatedAtUtc = fixture.Data.State.UpdatedAtUtc });
        Assert.Equal(ProcessDecisionPhase.Reconciling, accepted.Receipt.Decision.Current.Phase);
        Assert.Equal(fixture.Request.CommandId, accepted.Receipt.Decision.Current.RefreshCommandId);
        Assert.Equal(fixture.Data.Selection.Head!.MaterialId, accepted.Receipt.Decision.Current.MaterialId);
        Assert.Equal(1, accepted.Receipt.Decision.Current.UnclosedChildCount);
        Assert.Null(request.DecisionBudget);
        Assert.Null(request.DecisionMaterial);
        Assert.Null(request.DecisionRunScope);
        Assert.Empty(request.Mutation.Events);
        Assert.Empty(request.Mutation.OutboxMessages);
        Assert.Empty(request.Mutation.ArtifactLedgerEvents);
        Assert.Equal(0, fixture.Data.OutcomeReads);
        Assert.Equal(1, fixture.Data.InputReads);
        Assert.Equal(1, fixture.Data.Queries);
    }

    [Fact]
    public async Task Replay_returns_historical_acceptance_before_reading_later_runtime_or_business_state() {
        var fixture = new Fixture();
        var accepted = await fixture.AdmitAsync();
        fixture.Data.State = fixture.Data.State with { Status = ProcessRuntimeStatus.Cancelled };
        fixture.Data.Selection = fixture.Data.Selection with { Child = null, Head = null };
        var replay = await fixture.AdmitAsync();
        Assert.False(replay.CommittedNow);
        Assert.Equal(accepted.Receipt, replay.Receipt);
        Assert.Equal(1, fixture.Commits);
        Assert.Equal(1, fixture.Data.InputReads);
        Assert.Equal(1, fixture.Data.Queries);
        Assert.Equal(0, fixture.Data.OutcomeReads);
    }

    [Theory]
    [InlineData(ChangedIntent.Actor)]
    [InlineData(ChangedIntent.Budget)]
    [InlineData(ChangedIntent.Child)]
    [InlineData(ChangedIntent.Strategy)]
    public async Task Same_action_cannot_adopt_a_changed_body_or_actor(ChangedIntent changed) {
        var fixture = new Fixture();
        await fixture.AdmitAsync();
        var identity = fixture.Request.CommandId;
        switch (changed) {
            case ChangedIntent.Actor:
                fixture.Data.Context = fixture.Data.Context with { Actor = new(ProcessEventActorKind.Manager, new("another-manager")) };
                break;
            case ChangedIntent.Budget:
                fixture.Request = fixture.Request with { Budget = fixture.Request.Budget with { Revision = fixture.Request.Budget.Revision + 1 } };
                break;
            case ChangedIntent.Child:
                fixture.Request = fixture.Request with { Child = fixture.Request.Child with { ChildId = new(Id(41)) } };
                break;
            case ChangedIntent.Strategy:
                fixture.Request = fixture.Request with { StrategyId = new("different-strategy") };
                break;
        }
        Assert.Equal(identity, fixture.Request.CommandId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.AdmitAsync());
        Assert.Equal(1, fixture.Commits);
        Assert.Equal(1, fixture.Data.InputReads);
    }

    [Fact]
    public void Action_identity_includes_root_run_and_action_but_not_expectations() {
        var request = new Fixture().Request;
        Assert.Equal(request.CommandId, (request with { Decision = request.Decision with { Revision = request.Decision.Revision + 1 } }).CommandId);
        Assert.NotEqual(request.CommandId, (request with { ActionId = new(Id(40)) }).CommandId);
        Assert.NotEqual(request.CommandId, (request with { Child = request.Child with { RootRunId = new(Id(40)) } }).CommandId);
        Assert.NotEqual(request.CommandId, (request with { Child = request.Child with { RunId = new(Id(40)) } }).CommandId);
    }

    [Theory]
    [InlineData(ProcessEventActorKind.Unknown)]
    [InlineData(ProcessEventActorKind.System)]
    [InlineData(ProcessEventActorKind.Strategy)]
    [InlineData(ProcessEventActorKind.Driver)]
    [InlineData((ProcessEventActorKind)999)]
    public async Task Only_an_explicit_user_or_manager_can_admit_manual_refresh(ProcessEventActorKind kind) {
        var fixture = new Fixture();
        fixture.Data.Context = fixture.Data.Context with { Actor = new(kind, new("refresh-test")) };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AdmitAsync());
        Assert.Equal(0, fixture.Commits);
    }

    [Theory]
    [InlineData(ProcessDecisionRefreshKind.AutomaticReplan)]
    [InlineData(ProcessDecisionRefreshKind.ParentRefresh)]
    [InlineData((ProcessDecisionRefreshKind)999)]
    public async Task Unqualified_automatic_or_parent_actions_are_rejected_before_queries(ProcessDecisionRefreshKind kind) {
        var fixture = new Fixture();
        fixture.Request = fixture.Request with { Kind = kind };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AdmitAsync());
        Assert.Equal(0, fixture.Data.Queries);
        Assert.Equal(0, fixture.Data.InputReads);
        Assert.Equal(0, fixture.Commits);
    }

    [Theory]
    [InlineData(ChangedSelection.Decision)]
    [InlineData(ChangedSelection.Budget)]
    [InlineData(ChangedSelection.HistoricalChild)]
    [InlineData(ChangedSelection.OtherWork)]
    [InlineData(ChangedSelection.SharedScope)]
    public async Task Stale_or_incomplete_selection_cannot_freeze_work(ChangedSelection changed) {
        var fixture = new Fixture();
        var selection = fixture.Data.Selection;
        fixture.Data.Selection = changed switch {
            ChangedSelection.Decision => selection with { Head = selection.Head! with { Revision = selection.Head!.Revision + 1 } },
            ChangedSelection.Budget => selection with { Budget = selection.Budget with { Revision = selection.Budget.Revision + 1 } },
            ChangedSelection.HistoricalChild => selection with { Child = selection.Child! with { Observation = selection.Child!.Observation with { IsCurrent = false } } },
            ChangedSelection.OtherWork => selection with { OtherWorkPending = true },
            ChangedSelection.SharedScope => selection with { SharedScopePending = true },
            _ => throw new ArgumentOutOfRangeException(nameof(changed))
        };
        await Assert.ThrowsAsync<ProcessDecisionException>(() => fixture.AdmitAsync());
        Assert.Equal(0, fixture.Commits);
    }

    [Theory]
    [InlineData(ProcessRuntimeStatus.CancelRequested)]
    [InlineData(ProcessRuntimeStatus.Cancelled)]
    [InlineData(ProcessRuntimeStatus.Completed)]
    [InlineData(ProcessRuntimeStatus.Failed)]
    public async Task Stopped_runtime_cannot_acquire_new_refresh_authority(ProcessRuntimeStatus status) {
        var fixture = new Fixture();
        fixture.Data.State = fixture.Data.State with { Status = status };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.AdmitAsync());
        Assert.Equal(0, fixture.Commits);
    }

    [Fact]
    public async Task Admission_requires_the_original_completed_claim() {
        var fixture = new Fixture();
        fixture.Data.State = fixture.Data.State with { Claims = [] };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.AdmitAsync());
        Assert.Equal(0, fixture.Commits);
    }

    [Fact]
    public async Task Multiple_activities_require_their_own_refresh_contract() {
        var fixture = new Fixture();
        fixture.Data.State = fixture.Data.State with {
            Steps = [fixture.Data.State.Steps[0], fixture.Data.State.Steps[0] with { StepInstanceId = new(Id(43)) }]
        };
        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.AdmitAsync());
        Assert.Equal(0, fixture.Commits);
    }

    [Fact]
    public void Failure_policy_cannot_be_converted_into_manual_review() {
        var fixture = new Fixture();
        var command = fixture.Prepare();
        var policy = fixture.Data.Input.Assignment.DecisionDataPolicy!.Policy;
        var failure = new DecisionDataPolicy(policy.ReadBasisMode, policy.PublicationFreshness, OnBasisChange.Fail, policy.Budget);
        Assert.Throws<ProcessDecisionException>(() => ProcessDecisionRefreshAdmission.Begin(fixture.Data.State,
            fixture.Data.Selection.Head!, command, failure));
    }

    [Fact]
    public void Successor_drops_admission_reference_while_retired_and_cancelled_history_keeps_it() {
        var fixture = new Fixture();
        var begun = fixture.Begin(fixture.Prepare());
        var child = fixture.Data.Selection.Child!.Original.Child;
        var closed = ProcessDecisionTransitions.CloseChild(begun, begun.Expectation,
            new(child.Address.Reference, child.Address.ChildId, child.Owner, child.AllocationId),
            new(2, ProcessDecisionChildOutcome.NoEffect, null));
        var next = ProcessDecisionTransitions.Advance(closed.Generation, closed.Generation.Expectation, new(Id(44)));
        Assert.Equal(fixture.Request.CommandId, next.Previous.RefreshCommandId);
        Assert.Null(next.Current.RefreshCommandId);
        Assert.Equal(fixture.Request.CommandId, ProcessDecisionTransitions.Cancel(closed.Generation, closed.Generation.Expectation).RefreshCommandId);
        Assert.Throws<InvalidDataException>(() => (begun with { Phase = ProcessDecisionPhase.WaitingForReview }).Validate());
    }

    [Fact]
    public async Task Forged_retained_receipt_cannot_turn_an_unfenced_generation_into_acceptance() {
        var fixture = new Fixture();
        await fixture.AdmitAsync();
        fixture.Saved = fixture.Saved! with { Decision = fixture.Saved.Decision with {
            Current = fixture.Saved.Decision.Current with { RefreshCommandId = null }
        } };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.AdmitAsync());
        Assert.Equal(1, fixture.Commits);
    }

    public enum ChangedIntent { Actor, Budget, Child, Strategy }
    public enum ChangedSelection { Decision, Budget, HistoricalChild, OtherWork, SharedScope }

    private sealed class Fixture : IProcessDecisionRefreshReceiptReader, IProcessRuntimeUnitOfWork {
        internal Fixture() {
            var review = Data.Request;
            Request = new(new(Id(30)), review.Child, review.StrategyId, review.WaitingResultKey,
                review.Decision, review.Budget, null, ProcessDecisionRefreshKind.ManagerRefresh);
        }

        internal ProcessDecisionReviewCoordinatorFixture Data { get; } = new();
        internal ProcessDecisionRefreshRequest Request { get; set; }
        internal ProcessDecisionRefreshReceipt? Saved { get; set; }
        internal ProcessRuntimeCommitRequest? LastCommit { get; private set; }
        internal int Commits { get; private set; }
        internal Task<ProcessDecisionRefreshAdmissionResult> AdmitAsync()
            => new ProcessDecisionRefreshCoordinator(Data, Data, this, this, NullLogger<ProcessDecisionRefreshCoordinator>.Instance)
                .AdmitAsync(Data.State, Data.Context, Request);
        internal ProcessDecisionBeginRefresh Prepare()
            => ProcessDecisionRefreshPreparation.Create(Data.State, Data.Input, Data.Selection, Request, Data.Context.Actor);
        internal ProcessDecisionGeneration Begin(ProcessDecisionBeginRefresh command)
            => ProcessDecisionRefreshAdmission.Begin(Data.State, Data.Selection.Head!, command, Data.Input.Assignment.DecisionDataPolicy!.Policy);

        public Task<ProcessDecisionRefreshReceipt?> ReadAsync(ProcessRunId runId, RuntimeCommandId commandId, CancellationToken cancellationToken) {
            Assert.Equal(Data.State.RunId, runId);
            Assert.Equal(Request.CommandId, commandId);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Saved);
        }

        public Task<ProcessDecisionRefreshReceipt?> ReadCurrentAsync(ProcessRunId runId, ProcessStepInstanceId stepId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            Commits++;
            LastCommit = request;
            var command = Assert.IsType<ProcessDecisionBeginRefresh>(request.Decision);
            Saved = new(new(command, true, Begin(command)), null);
            return Task.FromResult(ProcessRuntimeCommitResult.FromMutation(request.Mutation) with {
                CommittedNow = true, Decision = Saved.Decision
            });
        }
    }
}
