using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessDecisionTerminalAccountingCoordinator(IProcessAcceptedExecutionInputReader inputs,
    IProcessDecisionResultQuery query, IEnumerable<IProcessDecisionChildOutcomeReader> readers,
    IProcessIdempotencyStore receipts, IProcessRuntimeUnitOfWork unitOfWork,
    ILogger<ProcessDecisionTerminalAccountingCoordinator> logger) {
    private readonly ProcessDecisionChildOutcomes outcomes = new(readers, logger);
    private const string ChildChanged = "Runtime.TerminalAccountingChildChanged";
    private const string StepChanged = "Runtime.TerminalAccountingStepChanged";
    private const string ExpectationChanged = "Runtime.TerminalAccountingExpectationChanged";
    private const string OutcomePending = "Runtime.TerminalAccountingOutcomePending";

    public async Task<ProcessRuntimeCommitResult> CompleteAsync(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        ProcessDecisionTerminalAccountingRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Actor);
        ArgumentNullException.ThrowIfNull(request);
        context.Validate();
        request.Validate();
        if (!Enum.IsDefined(context.Actor.Kind) || context.Actor.Kind == ProcessEventActorKind.Unknown ||
                string.IsNullOrWhiteSpace(context.Actor.Id.Value)) {
            throw new InvalidDataException("Terminal accounting requires an explicit original action actor.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Child.RunId != state.RunId || request.Child.RootRunId != state.RootRunId) {
            return Reject(state, ChildChanged, "Terminal accounting requires the original run and child.");
        }
        var selected = await query.ReadAsync(state.RunId, request.Child.StepInstanceId,
            new(request.Child.ChildId.Value), cancellationToken).ConfigureAwait(false);
        if (selected?.Child is not { } child) {
            return Reject(state, ChildChanged, "Terminal accounting requires its retained original child.");
        }
        selected.RequireOriginal(state, request.Child.StepInstanceId, new(request.Child.ChildId.Value));
        if (child.Original.Child.Address != request.Child) {
            return Reject(state, ChildChanged, "The requested accounting addresses a different original child.");
        }
        var input = await inputs.ReadAsync(state.RunId, request.Child.StepInstanceId, cancellationToken).ConfigureAwait(false);
        RequireInput(state, input, child.Original);
        var command = Identity(request, input, child.Original, context.Actor);
        if (await receipts.FindCompletedCommandAsync(command, cancellationToken).ConfigureAwait(false) is { } replay) {
            RequireReceipt(replay, request, child);
            return replay with { Outcome = ProcessRuntimeTransitionOutcome.Duplicate, CommittedNow = false };
        }
        var step = state.Steps.Single(item => item.StepInstanceId == request.Child.StepInstanceId);
        if (step.Status != request.StepStatus || step.ActiveClaimToken is not null || state.Claims.Any(claim =>
                claim.StepInstanceId == step.StepInstanceId &&
                claim.Status is DispatchClaimStatus.Claimed or DispatchClaimStatus.LeaseRenewed or DispatchClaimStatus.Reclaimed)) {
            return Reject(state, StepChanged, "Accounting requires the displayed terminal step without live dispatch work.");
        }
        if (selected.Head is not { Phase: ProcessDecisionPhase.Active or ProcessDecisionPhase.WaitingForReview or ProcessDecisionPhase.Reconciling } head ||
                head.Expectation != request.Decision || head.Reference != request.Child.Reference ||
                selected.Budget.BudgetId != request.Budget.BudgetId || selected.Budget.Revision != request.Budget.Revision) {
            return Reject(state, ExpectationChanged, "The displayed decision or budget changed before terminal accounting.");
        }
        if (await outcomes.ReadAsync(child, evidence: null, cancellationToken).ConfigureAwait(false) is not ProcessDecisionChildResult.Closed closed) {
            return Reject(state, OutcomePending, "The original child has no verified closed outcome and work; accounting remains pending.");
        }
        var allClosed = !selected.OtherWorkPending && head.UnclosedChildCount == (child.Observation.Closure is null ? 1 : 0);
        var completion = allClosed ? request.StepStatus == ProcessRuntimeStepStatus.Cancelled
            ? ProcessDecisionCompletion.Cancel : ProcessDecisionCompletion.Complete : (ProcessDecisionCompletion?)null;
        var now = context.OccurredAtUtc.UtcTicks;
        var updated = Math.Max(now - now % 10, checked(state.UpdatedAtUtc.UtcTicks + 10));
        ProcessRuntimeMutation mutation = new(ProcessRuntimeTransitionOutcome.Applied,
            state with { UpdatedAtUtc = new(updated, TimeSpan.Zero) }, [], [], [], []);
        return await unitOfWork.CommitAsync(new(command, state, mutation) {
            Decision = new ProcessDecisionClosure(step.StepInstanceId, request.Decision, request.Child.ChildId, closed.Closure) {
                Completion = completion
            },
            DecisionBudget = new ProcessDecisionBudgetSettlement(request.Budget, child.Original.Child.AllocationId, closed.Work)
        }, cancellationToken).ConfigureAwait(false);
    }

    private static void RequireInput(ProcessRuntimeStateSnapshot state, ProcessAcceptedExecutionInput input,
        ProcessDecisionChildMaterialBinding original) {
        var assignment = input.Assignment;
        var step = state.Steps.SingleOrDefault(item => item.StepInstanceId == original.Child.Address.StepInstanceId);
        if (step is null || input.RootRunId != state.RootRunId || input.AdmissionId != state.LaunchAdmissionId ||
                assignment.RunId != state.RunId || assignment.PlanId != state.PlanId ||
                assignment.StepInstanceId != step.StepInstanceId || input.RequireRequirements().StepDefinitionId != step.StepDefinitionId ||
                assignment.DecisionDataPolicy is not { } policy || policy.StepInstanceId != step.StepInstanceId) {
            throw new InvalidDataException("Terminal accounting lost its original accepted run, plan, admission, policy or step.");
        }
        policy.RequireExecutionContract(original.ExecutionContract);
    }

    private static RuntimeCommandId Identity(ProcessDecisionTerminalAccountingRequest request, ProcessAcceptedExecutionInput input,
        ProcessDecisionChildMaterialBinding original, ProcessEventActor actor) {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new {
            Domain = "process-decision-terminal-accounting/v1", request, input.InputFingerprint,
            OriginalChild = ProcessDecisionChildMaterialBindingCodec.Serialize(original), Actor = actor
        });
        return new(new Guid(SHA256.HashData(bytes).AsSpan(0, 16), bigEndian: true));
    }

    private static void RequireReceipt(ProcessRuntimeCommitResult replay, ProcessDecisionTerminalAccountingRequest request,
        ProcessDecisionResultChild selected) {
        var original = selected.Original.Child;
        if (!replay.Succeeded || replay.State.RootRunId != request.Child.RootRunId || replay.State.RunId != request.Child.RunId ||
                replay.State.Steps.SingleOrDefault(step => step.StepInstanceId == request.Child.StepInstanceId)?.Status != request.StepStatus ||
                replay.Events.Count != 0 || replay.OutboxMessages.Count != 0 || replay.ArtifactLedgerEvents.Count != 0 || replay.DecisionRunScope is not null ||
                replay.Decision is not { Command: ProcessDecisionClosure closure } decision ||
                replay.DecisionBudget is not { Command: ProcessDecisionBudgetSettlement settlement } budget ||
                closure.StepInstanceId != request.Child.StepInstanceId || closure.Expectation != request.Decision || closure.ChildId != request.Child.ChildId ||
                settlement.Expectation != request.Budget || settlement.AllocationId != original.AllocationId ||
                decision.Current.Reference != request.Child.Reference || decision.Current.MaterialId != original.MaterialId ||
                decision.Current.BudgetId != original.BudgetId || decision.Previous is not null ||
                closure.Completion is { } completion && (completion != (request.StepStatus == ProcessRuntimeStepStatus.Cancelled
                    ? ProcessDecisionCompletion.Cancel : ProcessDecisionCompletion.Complete) ||
                    decision.Current.Phase != ProcessDecisionTransitions.CompletionPhase(completion) || decision.Current.UnclosedChildCount != 0) ||
                closure.Completion is null && decision.Current.Phase is not (ProcessDecisionPhase.Active or ProcessDecisionPhase.WaitingForReview or ProcessDecisionPhase.Reconciling) ||
                decision.Child is not { } child || child.Reference != request.Child.Reference || child.ChildId != request.Child.ChildId ||
                child.Owner != original.Owner || child.AllocationId != original.AllocationId || child.Closure != closure.Closure ||
                budget.Change.Balance.BudgetId != original.BudgetId || budget.Change.Allocation.BudgetId != original.BudgetId ||
                budget.Change.Allocation.Id != original.AllocationId || budget.Change.Allocation.Grant != original.Grant ||
                budget.Change.Allocation.Settlement != settlement.Settlement || closure.Closure.OwnerRevision != settlement.Settlement.OwnerWorkRevision ||
                selected.Observation.Closure is { } retainedClosure && retainedClosure != closure.Closure ||
                selected.Observation.Settlement is { } retainedWork && retainedWork != settlement.Settlement) {
            throw new InvalidDataException("The terminal accounting receipt changed its original child, outcome or measured work.");
        }
        closure.Closure.Validate();
        settlement.Settlement.Validate();
        decision.Current.Validate();
        budget.Change.Balance.Validate();
        budget.Change.Allocation.Validate();
    }

    private static ProcessRuntimeCommitResult Reject(ProcessRuntimeStateSnapshot state, string code, string message)
        => ProcessRuntimeCommitResult.FromMutation(ProcessRuntimeMutation.Rejected(state, code, message));
}
