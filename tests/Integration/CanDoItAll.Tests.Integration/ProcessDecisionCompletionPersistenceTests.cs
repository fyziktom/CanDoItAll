using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessDecisionCompletionPersistenceTests {
    [Theory]
    [InlineData(ProcessRuntimeStepStatus.Completed, false)]
    [InlineData(ProcessRuntimeStepStatus.Completed, true)]
    [InlineData(ProcessRuntimeStepStatus.Failed, false)]
    [InlineData(ProcessRuntimeStepStatus.Failed, true)]
    [InlineData(ProcessRuntimeStepStatus.Cancelled, false)]
    [InlineData(ProcessRuntimeStepStatus.Cancelled, true)]
    public async Task Terminal_step_result_claim_child_generation_and_exact_work_commit_together(
        ProcessRuntimeStepStatus status, bool accepted) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var prepared = await PrepareAsync(context, status, accepted);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var result = await owner.CommitAsync(prepared.Request);
        Assert.Equal(status, result.State.Steps.Single().Status);
        Assert.Null(result.State.Steps.Single().ActiveClaimToken);
        Assert.DoesNotContain(result.State.Claims, claim => claim.Status is DispatchClaimStatus.Claimed or
            DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed);
        var expectedResult = prepared.Request.Mutation.State.AppliedResults.Single();
        Assert.Equal(JsonSerializer.Serialize(expectedResult), JsonSerializer.Serialize(Assert.Single(result.State.AppliedResults)));
        var command = (ProcessDecisionClosure)prepared.Request.Decision!;
        Assert.Equal(command.Expectation.Revision + 1, result.Decision!.Current.Revision);
        Assert.Equal(status == ProcessRuntimeStepStatus.Cancelled ? ProcessDecisionPhase.Cancelled : ProcessDecisionPhase.Completed,
            result.Decision.Current.Phase);
        Assert.Equal(0, result.Decision.Current.UnclosedChildCount);
        Assert.Equal(command.Closure, result.Decision.Child!.Closure);
        Assert.Equal(prepared.Child.AllocationId, result.Decision.Child.AllocationId);
        Assert.Equal(0, result.DecisionBudget!.Change.Balance.ReservedWorkTicks);
        Assert.Equal(9, result.DecisionBudget.Change.Balance.ConsumedWorkTicks);
        Assert.Equal(2, result.DecisionBudget.Change.Balance.AllocationCount);
        var replay = await owner.CommitAsync(prepared.Request);
        Assert.Equal(result.Decision, replay.Decision);
        Assert.Equal(result.DecisionBudget, replay.DecisionBudget);
        Assert.Equal(result.State.UpdatedAtUtc, replay.State.UpdatedAtUtc);
        Assert.Equal(result.Decision, (await owner.FindCompletedCommandAsync(prepared.Request.CommandId))!.Decision);
        var json = await context.IdempotencyKeys.AsNoTracking()
            .Where(item => item.CommandId == prepared.Request.CommandId.Value).Select(item => item.DecisionReceiptJson).SingleAsync();
        using var document = JsonDocument.Parse(json!);
        Assert.Equal(4, document.RootElement.GetProperty("version").GetInt32());
    }

    [Theory]
    [InlineData(Denial.NonterminalStep)]
    [InlineData(Denial.WrongTerminalStep)]
    [InlineData(Denial.StepClaim)]
    [InlineData(Denial.RetainedClaim)]
    [InlineData(Denial.UnclosedSibling)]
    [InlineData(Denial.OwnerRevision)]
    [InlineData(Denial.Allocation)]
    public async Task Incompatible_terminal_state_or_accounting_rolls_back_the_whole_outcome(Denial denial) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var prepared = await PrepareAsync(context, ProcessRuntimeStepStatus.Completed, false,
            additionalChild: denial is Denial.UnclosedSibling or Denial.Allocation);
        var request = prepared.Request;
        var target = request.Mutation.State;
        var step = target.Steps.Single();
        switch (denial) {
            case Denial.NonterminalStep:
                target = target with { Steps = [step with { Status = ProcessRuntimeStepStatus.Running }] };
                break;
            case Denial.WrongTerminalStep:
                target = target with { Steps = [step with { Status = ProcessRuntimeStepStatus.Cancelled }] };
                break;
            case Denial.StepClaim:
                target = target with { Steps = [step with { ActiveClaimToken = request.OriginalState.Steps.Single().ActiveClaimToken }] };
                break;
            case Denial.RetainedClaim:
                target = target with { Claims = request.OriginalState.Claims };
                break;
            case Denial.OwnerRevision:
                var original = (ProcessDecisionClosure)request.Decision!;
                request = request with { Decision = original with { Closure = original.Closure with { OwnerRevision = original.Closure.OwnerRevision + 1 } } };
                break;
            case Denial.Allocation:
                request = request with { DecisionBudget = (ProcessDecisionBudgetSettlement)request.DecisionBudget!
                    with { AllocationId = prepared.Sibling!.AllocationId } };
                break;
        }
        request = request with { Mutation = request.Mutation with { State = target } };
        var beforeBudget = await BudgetAsync(context, request.OriginalState);
        var beforeDecision = await DecisionAsync(context, request.OriginalState);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var failure = await Record.ExceptionAsync(() => owner.CommitAsync(request));
        if (denial == Denial.UnclosedSibling) {
            Assert.Equal(ProcessDecisionRefusal.UnclosedChildren, Assert.IsType<ProcessDecisionException>(failure).Reason);
        } else {
            var refused = Assert.IsType<InvalidOperationException>(failure);
            Assert.Contains(denial is Denial.OwnerRevision or Denial.Allocation ? "exact owner revision" : "release its dispatch claim",
                refused.Message, StringComparison.Ordinal);
        }
        Assert.Equal(beforeBudget, await BudgetAsync(context, request.OriginalState));
        Assert.Equal(beforeDecision, await DecisionAsync(context, request.OriginalState));
        var retained = (await owner.LoadAsync(request.OriginalState.RunId))!;
        Assert.Equal(request.OriginalState.UpdatedAtUtc, retained.UpdatedAtUtc);
        Assert.Empty(retained.AppliedResults);
        Assert.Null(await owner.FindCompletedCommandAsync(request.CommandId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failure_after_sql_flush_rolls_back_result_work_and_terminal_phase(bool accepted) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var prepared = await PrepareAsync(context, ProcessRuntimeStepStatus.Completed, accepted);
        var beforeBudget = await BudgetAsync(context, prepared.Request.OriginalState);
        var beforeDecision = await DecisionAsync(context, prepared.Request.OriginalState);
        var fault = new AfterFlushFault();
        await using (var broken = Context(app.Services, fault)) {
            Assert.Same(fault.Failure, await Assert.ThrowsAsync<IOException>(() =>
                new EfProcessRuntimeUnitOfWork(broken).CommitAsync(prepared.Request)));
            Assert.True(fault.Flushed);
        }
        var owner = new EfProcessRuntimeUnitOfWork(context);
        Assert.Equal(beforeBudget, await BudgetAsync(context, prepared.Request.OriginalState));
        Assert.Equal(beforeDecision, await DecisionAsync(context, prepared.Request.OriginalState));
        Assert.Empty((await owner.LoadAsync(prepared.Request.OriginalState.RunId))!.AppliedResults);
        Assert.Null(await owner.FindCompletedCommandAsync(prepared.Request.CommandId));
        var retried = await owner.CommitAsync(prepared.Request);
        Assert.Equal(ProcessDecisionPhase.Completed, retried.Decision!.Current.Phase);
        Assert.Equal(9, retried.DecisionBudget!.Change.Balance.ConsumedWorkTicks);
    }

    [Fact]
    public async Task Lost_commit_acknowledgement_replays_original_receipt_without_another_work_charge() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var prepared = await PrepareAsync(context, ProcessRuntimeStepStatus.Completed, true);
        var failure = new IOException("atomic-completion-acknowledgement-lost");
        await using (var broken = Context(app.Services, new AfterCommitFault(failure))) {
            Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() =>
                new EfProcessRuntimeUnitOfWork(broken).CommitAsync(prepared.Request)));
        }
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var recovered = await owner.CommitAsync(prepared.Request);
        Assert.Equal(((ProcessDecisionClosure)prepared.Request.Decision!).Closure, recovered.Decision!.Child!.Closure);
        Assert.Equal(ProcessDecisionPhase.Completed, recovered.Decision.Current.Phase);
        Assert.Equal(9, recovered.DecisionBudget!.Change.Balance.ConsumedWorkTicks);
        Assert.Equal(2, recovered.DecisionBudget.Change.Balance.AllocationCount);
        Assert.Single((await owner.LoadAsync(prepared.Request.OriginalState.RunId))!.AppliedResults);
        Assert.Equal(recovered.Decision, (await owner.FindCompletedCommandAsync(prepared.Request.CommandId))!.Decision);
    }

    [Fact]
    public async Task A_completed_command_cannot_replay_without_its_original_completion() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var prepared = await PrepareAsync(context, ProcessRuntimeStepStatus.Completed, true);
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var committed = await owner.CommitAsync(prepared.Request);
        var original = (ProcessDecisionClosure)prepared.Request.Decision!;
        foreach (var changed in new ProcessDecisionCompletion?[] { null, ProcessDecisionCompletion.Cancel }) {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                owner.CommitAsync(prepared.Request with { Decision = original with { Completion = changed } }));
        }
        Assert.Equal(committed.Decision!.Current, await DecisionAsync(context, prepared.Request.OriginalState));
        Assert.Equal(committed.DecisionBudget!.Change.Balance, await BudgetAsync(context, prepared.Request.OriginalState));
    }

    [Fact]
    public async Task Terminal_completion_does_not_trust_a_corrupted_open_child_counter() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var prepared = await PrepareAsync(context, ProcessRuntimeStepStatus.Completed, false, additionalChild: true);
        var generation = await context.DecisionGenerations.SingleAsync();
        generation.UnclosedChildCount = 1;
        generation.Revision++;
        var head = await context.DecisionHeads.SingleAsync();
        head.Revision = generation.Revision;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var current = await DecisionAsync(context, prepared.Request.OriginalState);
        var command = (ProcessDecisionClosure)prepared.Request.Decision!;
        var request = prepared.Request with { Decision = command with { Expectation = current.Expectation } };
        var budget = await BudgetAsync(context, request.OriginalState);
        await Assert.ThrowsAsync<InvalidDataException>(() => new EfProcessRuntimeUnitOfWork(context).CommitAsync(request));
        Assert.Equal(budget, await BudgetAsync(context, request.OriginalState));
        Assert.Equal(2, await context.DecisionChildren.CountAsync(item => item.OwnerRevision == null));
    }

    private static async Task<Prepared> PrepareAsync(ProcessPersistenceDbContext context,
        ProcessRuntimeStepStatus status, bool accepted, bool additionalChild = false) {
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var enrolled = await EnrollAsync(context, (await StartAsync(context)).State);
        var child = enrolled.Decision!.Child!;
        ProcessDecisionChild? sibling = null;
        if (additionalChild) {
            enrolled = await EnrollAsync(context, enrolled.State);
            sibling = enrolled.Decision!.Child;
        }
        var state = (await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim(enrolled.State))).State;
        if (status == ProcessRuntimeStepStatus.Cancelled) {
            var current = await DecisionAsync(context, state);
            var quiesce = Request(state, new ProcessDecisionChangePhase(Step(state), current.Expectation,
                ProcessDecisionPhaseChange.BeginReconciliation));
            quiesce = quiesce with { Mutation = quiesce.Mutation with { State = quiesce.Mutation.State with {
                Steps = [quiesce.Mutation.State.Steps.Single() with { ActiveClaimToken = null }],
                Claims = state.Claims.Select(item => item with { Status = DispatchClaimStatus.Released }).ToArray()
            } } };
            state = (await owner.CommitAsync(quiesce)).State;
        }
        var closure = new ProcessDecisionChildClosure(7, accepted ? ProcessDecisionChildOutcome.Accepted : ProcessDecisionChildOutcome.NoEffect,
            accepted ? new(Guid.NewGuid()) : null);
        var request = await ClosureRequestAsync(context, state, child, closure, 4);
        var command = (ProcessDecisionClosure)request.Decision!;
        var key = StrategyResultIdempotencyKey.New();
        var outcome = status switch {
            ProcessRuntimeStepStatus.Completed => StrategyOutcome.Succeeded,
            ProcessRuntimeStepStatus.Failed => StrategyOutcome.Failed,
            ProcessRuntimeStepStatus.Cancelled => StrategyOutcome.Canceled,
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
        var receipt = new StrategyResultReceipt(Step(state), new("atomic-completion-fixture"), key,
            outcome, status, "sha256:" + new string('a', 64)) { AppliedSequence = 1 };
        var target = request.Mutation.State with {
            Status = status switch {
                ProcessRuntimeStepStatus.Completed => ProcessRuntimeStatus.Completed,
                ProcessRuntimeStepStatus.Failed => ProcessRuntimeStatus.Failed,
                _ => ProcessRuntimeStatus.Cancelled
            },
            Steps = [state.Steps.Single() with {
                Status = status, ActiveClaimToken = null,
                CompletedResultKey = status == ProcessRuntimeStepStatus.Completed ? key : null
            }],
            Claims = state.Claims.Select(item => item with {
                Status = status == ProcessRuntimeStepStatus.Cancelled ? DispatchClaimStatus.Cancelled : DispatchClaimStatus.Completed,
                ResultIdempotencyKey = key
            }).ToArray(),
            AppliedResults = [receipt]
        };
        return new(request with {
            Decision = command with { Completion = status == ProcessRuntimeStepStatus.Cancelled
                ? ProcessDecisionCompletion.Cancel : ProcessDecisionCompletion.Complete },
            Mutation = request.Mutation with { State = target }
        }, child, sibling);
    }

    private sealed record Prepared(ProcessRuntimeCommitRequest Request, ProcessDecisionChild Child, ProcessDecisionChild? Sibling);

    private sealed class AfterFlushFault : SaveChangesInterceptor {
        internal IOException Failure { get; } = new("atomic-completion-after-flush");
        internal bool Flushed { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            Flushed = result > 0 && eventData.Context!.Database.CurrentTransaction is not null;
            throw Failure;
        }
    }

    private sealed class AfterCommitFault(IOException failure) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) => throw failure;
    }

    public enum Denial { NonterminalStep, WrongTerminalStep, StepClaim, RetainedClaim, UnclosedSibling, OwnerRevision, Allocation }
}
