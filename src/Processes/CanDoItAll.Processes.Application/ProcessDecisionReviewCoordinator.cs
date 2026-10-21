using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessDecisionReviewCoordinator(IProcessAcceptedExecutionInputReader inputs, IProcessDecisionResultQuery query,
    IEnumerable<IProcessDecisionChildOutcomeReader> readers, IProcessIdempotencyStore receipts,
    IProcessRuntimeUnitOfWork unitOfWork, ILogger<ProcessDecisionReviewCoordinator> logger) {
    private readonly ProcessDecisionChildOutcomes outcomes = new(readers, logger);

    public async Task<ProcessDecisionChildMaterialBinding> RequirePendingAsync(ProcessRuntimeStateSnapshot state,
        ProcessDecisionReviewRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        var execution = new ProcessExecutionRunId(request.Child.ChildId.Value);
        var selection = await query.ReadAsync(state.RunId, request.Child.StepInstanceId, execution, cancellationToken).ConfigureAwait(false);
        if (selection?.Child is not { } child) {
            throw new ProcessDecisionReviewAdmissionException(new("Runtime.ReviewChildMissing", "Review requires its retained original child."));
        }
        selection.RequireOriginal(state, request.Child.StepInstanceId, execution);
        if (child.Original.Child.Address != request.Child || child.Observation.Closure is not null ||
                child.Observation.Settlement is not null || !MatchesReview(selection, request)) {
            throw new ProcessDecisionReviewAdmissionException(new("Runtime.ReviewExpectationChanged",
                "The displayed decision, budget or original child work changed before review."));
        }
        var input = await ReadOriginalInputAsync(state, request, cancellationToken).ConfigureAwait(false);
        ProcessDecisionSingleActivityAdmission.Require(state, input, child.Original.ExecutionContract);
        if (ProcessDecisionReviewCompletion.ValidatePending(state, request.Child, request.StrategyId, request.WaitingResultKey) is { } refusal) {
            throw new ProcessDecisionReviewAdmissionException(refusal);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return child.Original;
    }

    public async Task<ProcessRuntimeCommitResult> CompleteAsync(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        ProcessDecisionReviewRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        context.Validate();
        request.Validate();
        var execution = new ProcessExecutionRunId(request.Child.ChildId.Value);
        var selection = await query.ReadAsync(state.RunId, request.Child.StepInstanceId, execution, cancellationToken).ConfigureAwait(false);
        if (selection?.Child is not { } child) {
            return Reject(state, "Runtime.ReviewChildMissing", "Review completion requires its retained original decision child.");
        }
        selection.RequireOriginal(state, request.Child.StepInstanceId, execution);
        if (child.Original.Child.Address != request.Child) {
            return Reject(state, "Runtime.ReviewChildChanged", "The requested review addresses a different original child.");
        }
        var input = await ReadOriginalInputAsync(state, request, cancellationToken).ConfigureAwait(false);
        context = context with { CommandId = ProcessDecisionReviewIdentity.Derive(request, input, child.Original, context.Actor) };
        if (await receipts.FindCompletedCommandAsync(context.CommandId, cancellationToken).ConfigureAwait(false) is { } replay) {
            RequireReceipt(replay, context, request, child);
            return replay with { Outcome = ProcessRuntimeTransitionOutcome.Duplicate, CommittedNow = false };
        }
        ProcessDecisionSingleActivityAdmission.Require(state, input, child.Original.ExecutionContract);
        if (!MatchesReview(selection, request)) {
            return Reject(state, "Runtime.ReviewExpectationChanged", "The displayed decision, budget or original child work changed before completion.");
        }
        if (await outcomes.ReadAsync(child, evidence: null, cancellationToken).ConfigureAwait(false) is not ProcessDecisionChildResult.Closed closed) {
            return Reject(state, "Runtime.ReviewOutcomePending", "The original child has no verified closed outcome and work; completion remains pending.");
        }
        var outcome = closed.Status switch {
            ProcessDecisionChildTerminalStatus.Succeeded => StrategyOutcome.Succeeded,
            ProcessDecisionChildTerminalStatus.Failed => StrategyOutcome.Failed,
            ProcessDecisionChildTerminalStatus.Cancelled => StrategyOutcome.Canceled,
            _ => throw new InvalidDataException("The original child has an unsupported terminal status.")
        };
        var mutation = ProcessDecisionReviewCompletion.Prepare(state, context,
            new(request.Child, request.StrategyId, request.WaitingResultKey, request.CompletionKey, outcome, closed.Closure, closed.Work));
        if (mutation.Outcome == ProcessRuntimeTransitionOutcome.Duplicate) {
            return Reject(state, "Runtime.ReviewReceiptMissing", "The completed review result has no matching original accounting command receipt.");
        }
        if (!mutation.Succeeded) {
            return ProcessRuntimeCommitResult.FromMutation(mutation);
        }
        return await unitOfWork.CommitAsync(new(context.CommandId, state, mutation) {
            Decision = new ProcessDecisionClosure(request.Child.StepInstanceId, request.Decision, request.Child.ChildId, closed.Closure) {
                Completion = outcome == StrategyOutcome.Canceled ? ProcessDecisionCompletion.Cancel : ProcessDecisionCompletion.Complete
            },
            DecisionBudget = new ProcessDecisionBudgetSettlement(request.Budget, child.Original.Child.AllocationId, closed.Work)
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessAcceptedExecutionInput> ReadOriginalInputAsync(ProcessRuntimeStateSnapshot state,
        ProcessDecisionReviewRequest request, CancellationToken cancellationToken) {
        var input = await inputs.ReadAsync(state.RunId, request.Child.StepInstanceId, cancellationToken).ConfigureAwait(false);
        if (input.RootRunId != state.RootRunId || input.AdmissionId != state.LaunchAdmissionId ||
                input.Assignment.RunId != state.RunId || input.Assignment.PlanId != state.PlanId ||
                input.Assignment.StepInstanceId != request.Child.StepInstanceId) {
            throw new InvalidDataException("The review input does not retain its original run, plan, admission and step.");
        }
        return input;
    }

    private static bool MatchesReview(ProcessDecisionResultSelection selection, ProcessDecisionReviewRequest request)
        => selection.Child is { Observation.IsCurrent: true } child &&
            selection.Head is { Phase: ProcessDecisionPhase.WaitingForReview } head &&
            head.Expectation == request.Decision && selection.Budget.BudgetId == request.Budget.BudgetId &&
            selection.Budget.Revision == request.Budget.Revision && head.Reference == request.Child.Reference &&
            !selection.OtherWorkPending && !selection.SharedScopePending &&
            head.UnclosedChildCount == (child.Observation.Closure is null ? 1 : 0);

    private static void RequireReceipt(ProcessRuntimeCommitResult replay, RuntimeCommandContext context,
        ProcessDecisionReviewRequest request, ProcessDecisionResultChild selected) {
        var original = selected.Original;
        var child = original.Child;
        if (!replay.Succeeded || replay.State.RootRunId != request.Child.RootRunId || replay.State.RunId != request.Child.RunId ||
                replay.Decision is not { Command: ProcessDecisionClosure closure } decision ||
                replay.DecisionBudget is not { Command: ProcessDecisionBudgetSettlement settlement } budget ||
                closure.Expectation != request.Decision || closure.ChildId != request.Child.ChildId ||
                closure.StepInstanceId != request.Child.StepInstanceId || settlement.Expectation != request.Budget ||
                settlement.AllocationId != child.AllocationId || decision.Current.Reference != request.Child.Reference ||
                decision.Current.MaterialId != child.MaterialId || decision.Current.BudgetId != child.BudgetId ||
                decision.Current.UnclosedChildCount != 0 || closure.Completion is not { } completion ||
                decision.Current.Phase != ProcessDecisionTransitions.CompletionPhase(completion) ||
                decision.Child is not { } retainedChild || retainedChild.Reference != request.Child.Reference ||
                retainedChild.ChildId != child.Address.ChildId || retainedChild.Owner != child.Owner ||
                retainedChild.AllocationId != child.AllocationId || retainedChild.Closure != closure.Closure ||
                budget.Change.Allocation.BudgetId != child.BudgetId || budget.Change.Allocation.Id != child.AllocationId ||
                budget.Change.Allocation.Grant != child.Grant || budget.Change.Allocation.Settlement != settlement.Settlement ||
                selected.Observation.Closure is { } retainedClosure && retainedClosure != closure.Closure ||
                selected.Observation.Settlement is { } retainedWork && retainedWork != settlement.Settlement ||
                closure.Closure.OwnerRevision != settlement.Settlement.OwnerWorkRevision) {
            throw InvalidReceipt();
        }
        closure.Closure.Validate();
        settlement.Settlement.Validate();
        decision.Current.Validate();
        budget.Change.Balance.Validate();
        budget.Change.Allocation.Validate();
        var result = replay.State.AppliedResults.SingleOrDefault(item => item.StepInstanceId == request.Child.StepInstanceId &&
            item.StrategyId == request.StrategyId && item.IdempotencyKey == request.CompletionKey);
        if (result is null || completion != (result.Outcome == StrategyOutcome.Canceled
                ? ProcessDecisionCompletion.Cancel : ProcessDecisionCompletion.Complete)) {
            throw InvalidReceipt();
        }
        var verified = ProcessDecisionReviewCompletion.Prepare(replay.State, context,
            new(request.Child, request.StrategyId, request.WaitingResultKey, request.CompletionKey, result.Outcome, closure.Closure, settlement.Settlement));
        if (verified.Outcome != ProcessRuntimeTransitionOutcome.Duplicate) {
            throw InvalidReceipt();
        }
    }

    private static InvalidDataException InvalidReceipt()
        => new("The Process review receipt does not retain its exact original result, child closure and work settlement.");

    private static ProcessRuntimeCommitResult Reject(ProcessRuntimeStateSnapshot state, string code, string message)
        => ProcessRuntimeCommitResult.FromMutation(ProcessRuntimeMutation.Rejected(state, code, message));
}
