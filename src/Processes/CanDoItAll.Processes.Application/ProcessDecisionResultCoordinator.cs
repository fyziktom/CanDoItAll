using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessDecisionResultCoordinator(IProcessDecisionResultQuery query,
    IEnumerable<IProcessDecisionChildOutcomeReader> readers, IProcessIdempotencyStore receipts,
    IProcessRuntimeUnitOfWork unitOfWork, ILogger<ProcessDecisionResultCoordinator> logger) {
    private readonly ProcessDecisionChildOutcomes outcomes = new(readers, logger);
    private const string ReviewCode = "process.runtime.decision_result_requires_review";
    private const string ReviewSummary = "The original decision work requires reconciliation or manager review before this step can continue.";

    public async Task<ProcessRuntimeCommitResult> SubmitAsync(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        SubmitStrategyResultCommand command, CancellationToken cancellationToken = default) {
        if (!ProcessStrategyResultSnapshot.TryCapture(command.Result, out var result, out var refusal)) {
            return Reject(state, refusal == ProcessStrategyResultRefusal.InvalidHostCapabilities
                ? "Runtime.HostCapabilityEvidenceInvalid" : "Runtime.StrategyResultReceiptInvalid",
                "The strategy result is invalid or exceeds its bounded receipt contract.");
        }
        if (command.IdempotencyKey.Value != result.IdempotencyKey) {
            return Reject(state, "Runtime.StrategyResultKeyMismatch", "The command and its result must use the same idempotency key.");
        }
        command = command with { Result = result };
        var selection = await query.ReadAsync(state.RunId, command.StepInstanceId, result.ExecutionRunId, cancellationToken).ConfigureAwait(false);
        if (selection is null) {
            if (result.OwnerEvidence is not null) {
                return Reject(state, "Runtime.DecisionResultEvidenceUnbound", "Live owner evidence requires its original decision child.");
            }
            return await new ProcessRuntimeEngine(unitOfWork).SubmitStrategyResultAsync(state, context, command, cancellationToken).ConfigureAwait(false);
        }
        selection.RequireOriginal(state, command.StepInstanceId, result.ExecutionRunId);
        context = context with { CommandId = ProcessDecisionResultIdentity.Derive(state, command, selection) };
        if (await receipts.FindCompletedCommandAsync(context.CommandId, cancellationToken).ConfigureAwait(false) is { } replay) {
            RequireOriginalReceipt(replay, command, selection);
            return replay with { Outcome = ProcessRuntimeTransitionOutcome.Duplicate };
        }
        if (result.OwnerEvidence is not null && selection.Child is null) {
            return Reject(state, "Runtime.DecisionResultEvidenceUnbound", "Live owner evidence requires its original decision child.");
        }
        var mutation = ProcessRuntimeEngine.PrepareStrategyResult(state, context, command);
        if (mutation.Outcome == ProcessRuntimeTransitionOutcome.Duplicate) {
            return Reject(state, "Runtime.DecisionResultReceiptMissing", "The strategy result has no matching original accounting command receipt.");
        }
        if (!mutation.Succeeded) {
            return ProcessRuntimeCommitResult.FromMutation(mutation);
        }
        if (mutation.State.AppliedResults.Single(item => item.StepInstanceId == command.StepInstanceId &&
                item.IdempotencyKey == command.IdempotencyKey).ExecutionRunId != result.ExecutionRunId) {
            return Reject(state, "Runtime.DecisionResultExecutionMismatch", "The result evidence does not retain its exact original execution identity.");
        }
        if (selection.Child is { } selected && selection.Head?.Reference != selected.Original.Child.Address.Reference) {
            return Reject(state, "Runtime.DecisionResultGenerationChanged", "A result cannot use a replacement decision generation.");
        }
        if (selection.Head is { Phase: ProcessDecisionPhase.Completed or ProcessDecisionPhase.Cancelled or ProcessDecisionPhase.Superseded }) {
            return Reject(state, "Runtime.DecisionResultClosed", "A new result cannot reopen a terminal decision.");
        }
        var outcome = selection.Child is { } child
            ? await outcomes.ReadAsync(child, result.OwnerEvidence, cancellationToken).ConfigureAwait(false)
            : new ProcessDecisionChildResult.Pending(ProcessDecisionChildPendingReason.MissingJournal);
        var completion = ChooseCompletion(selection, outcome, mutation, command);
        if (completion is ProcessDecisionCompletion.Review or ProcessDecisionCompletion.Reconcile &&
                !(outcome is ProcessDecisionChildResult.Closed { Status: ProcessDecisionChildTerminalStatus.Succeeded } &&
                    mutation.State.Steps.Single(step => step.StepInstanceId == command.StepInstanceId).Status == ProcessRuntimeStepStatus.Blocked)) {
            var verified = outcome is ProcessDecisionChildResult.Closed { Status: ProcessDecisionChildTerminalStatus.Succeeded };
            mutation = ProcessRuntimeEngine.PrepareStrategyResult(state, context, command with { Result = RequireReview(result, verified) });
            if (!mutation.Succeeded) {
                return ProcessRuntimeCommitResult.FromMutation(mutation);
            }
        }
        var decision = BuildDecision(selection, command, outcome, completion);
        ProcessDecisionBudgetSettlement? settlement = outcome is ProcessDecisionChildResult.Closed terminal
            ? new(new(command.StepInstanceId, selection.Budget.BudgetId, selection.Budget.Revision),
                selection.Child!.Original.Child.AllocationId, terminal.Work)
            : null;
        return await unitOfWork.CommitAsync(new(context.CommandId, state, mutation) {
            Decision = decision, DecisionBudget = settlement,
            DecisionRunScope = ProcessDecisionRunScopeCompletion.Prepare(selection.SharedScope, state, mutation)
        }, cancellationToken).ConfigureAwait(false);
    }

    private static ProcessDecisionCompletion ChooseCompletion(ProcessDecisionResultSelection selection, ProcessDecisionChildResult outcome,
        ProcessRuntimeMutation mutation, SubmitStrategyResultCommand command) {
        var status = mutation.State.Steps.Single(step => step.StepInstanceId == command.StepInstanceId).Status;
        var terminal = status is ProcessRuntimeStepStatus.Completed or ProcessRuntimeStepStatus.Failed or ProcessRuntimeStepStatus.Cancelled;
        var noExecutionFailure = selection.Head is null && selection.Child is null && !selection.OtherWorkPending && !selection.SharedScopePending &&
            command.Result.ExecutionRunId is null && command.Result.Outcome is StrategyOutcome.Failed or StrategyOutcome.Canceled && terminal;
        if (noExecutionFailure) {
            return status == ProcessRuntimeStepStatus.Cancelled ? ProcessDecisionCompletion.Cancel : ProcessDecisionCompletion.Complete;
        }
        if (selection.OtherWorkPending || selection.SharedScopePending ||
                selection.Head is { } head && head.UnclosedChildCount > (selection.Child?.Observation.Closure is null && selection.Child is not null ? 1 : 0)) {
            return ProcessDecisionCompletion.Reconcile;
        }
        if (outcome is ProcessDecisionChildResult.Closed closed) {
            if (terminal && (command.Result.Outcome != StrategyOutcome.Succeeded || closed.Status == ProcessDecisionChildTerminalStatus.Succeeded)) {
                return status == ProcessRuntimeStepStatus.Cancelled ? ProcessDecisionCompletion.Cancel : ProcessDecisionCompletion.Complete;
            }
            return selection.Head?.Phase == ProcessDecisionPhase.Reconciling ? ProcessDecisionCompletion.Reconcile : ProcessDecisionCompletion.Review;
        }
        return outcome is ProcessDecisionChildResult.Pending { Reason: ProcessDecisionChildPendingReason.ApprovalRequired or ProcessDecisionChildPendingReason.BasisChanged } &&
            selection.Head?.Phase != ProcessDecisionPhase.Reconciling ? ProcessDecisionCompletion.Review : ProcessDecisionCompletion.Reconcile;
    }

    private static ProcessDecisionCommand? BuildDecision(ProcessDecisionResultSelection selection, SubmitStrategyResultCommand command,
        ProcessDecisionChildResult outcome, ProcessDecisionCompletion completion) {
        if (selection.Head is not { } head) {
            return null;
        }
        if (outcome is ProcessDecisionChildResult.Closed closed) {
            return new ProcessDecisionClosure(command.StepInstanceId, head.Expectation, selection.Child!.Original.Child.Address.ChildId, closed.Closure) {
                Completion = completion
            };
        }
        var phase = ProcessDecisionTransitions.CompletionPhase(completion);
        if (head.Phase == phase) {
            return null;
        }
        return new ProcessDecisionChangePhase(command.StepInstanceId, head.Expectation, completion == ProcessDecisionCompletion.Review
            ? ProcessDecisionPhaseChange.WaitForReview : ProcessDecisionPhaseChange.BeginReconciliation);
    }

    private static StrategyResultEnvelope RequireReview(StrategyResultEnvelope result, bool verified) {
        var digest = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ReviewCode + ":" + result.ResultHash))).ToLowerInvariant();
        return result with {
            Outcome = StrategyOutcome.NeedsManager, ProducedArtifacts = verified ? result.ProducedArtifacts : [],
            Diagnostics = [new(new(ReviewCode), StrategyDiagnosticSensitivity.Normal, digest, ReviewSummary, null,
                ProcessDiagnosticRetrySafety.UnsafeToRetry, ProcessDiagnosticIdempotencyClassification.Unknown)],
            ManagerSignals = [], UserSafeSummary = ReviewSummary, ResultHash = digest
        };
    }

    private static void RequireOriginalReceipt(ProcessRuntimeCommitResult replay, SubmitStrategyResultCommand command, ProcessDecisionResultSelection selection) {
        ProcessDecisionRunScopeCompletion.RequireReceipt(replay, selection.SharedScope?.ScopeId);
        var result = replay.State.AppliedResults.SingleOrDefault(item => item.StepInstanceId == command.StepInstanceId &&
            item.StrategyId == command.Result.StrategyId && item.IdempotencyKey == command.IdempotencyKey);
        if (replay.State.RunId != selection.RunId || replay.State.RootRunId != selection.RootRunId || result is null ||
                result.ExecutionRunId != command.Result.ExecutionRunId || !replay.Succeeded ||
                replay.Decision is { } decision && (decision.Command.StepInstanceId != command.StepInstanceId || decision.Current.BudgetId != selection.Budget.BudgetId)) {
            throw new InvalidDataException("The Process result command receipt does not match its original result and budget.");
        }
        if (replay.Decision is { Command: ProcessDecisionClosure closure } receipt) {
            var child = selection.Child?.Original.Child
                ?? throw new InvalidDataException("The Process result command lost its original child.");
            if (closure.ChildId != child.Address.ChildId || closure.Expectation.Reference != child.Address.Reference ||
                    receipt.Current.MaterialId != child.MaterialId || replay.DecisionBudget is not { Command: ProcessDecisionBudgetSettlement settlement } ||
                    settlement.Expectation.StepInstanceId != command.StepInstanceId || settlement.Expectation.BudgetId != child.BudgetId ||
                    settlement.AllocationId != child.AllocationId || settlement.Settlement.OwnerWorkRevision != closure.Closure.OwnerRevision) {
                throw new InvalidDataException("The Process result command receipt changed its original child settlement.");
            }
        } else if (replay.DecisionBudget is not null) {
            throw new InvalidDataException("A Process result settlement must retain its exact child closure.");
        }
    }

    private static ProcessRuntimeCommitResult Reject(ProcessRuntimeStateSnapshot state, string code, string message)
        => ProcessRuntimeCommitResult.FromMutation(ProcessRuntimeMutation.Rejected(state, code, message));
}
