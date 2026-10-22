using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

[Flags]
internal enum ProcessRootCancellationPending {
    None = 0, MissingRoot = 1, RuntimeWork = 2, Decisions = 4, Budget = 8, SharedScope = 16
}

internal static class EfProcessRootCancellationGuard {
    internal const string WorkAdmissionStopped = "Runtime.RootWorkAdmissionStopped";
    internal const string WorkPending = "Runtime.RootCancellationWorkPending";

    internal static async Task<(ProcessRuntimeCommitResult? Rejection, bool CheckAfterFlush)> CheckAsync(ProcessPersistenceDbContext context,
        ProcessRuntimeCommitRequest request, ProcessRuntimeStateEntity current, ProcessRuntimeWorkAdmission admission,
        CancellationToken cancellationToken) {
        var proposed = request.Mutation.State;
        if (current.Status == ProcessRuntimeStatus.CancelRequested &&
                proposed.Status is not (ProcessRuntimeStatus.CancelRequested or ProcessRuntimeStatus.Cancelled) ||
                current.Status == ProcessRuntimeStatus.Cancelled && proposed.Status != ProcessRuntimeStatus.Cancelled) {
            throw new InvalidOperationException($"Run '{request.OriginalState.RunId}' cannot leave its persisted cancellation barrier.");
        }
        var isRoot = current.RunId == current.RootRunId;
        if (admission.NewWork) {
            var rootStatus = isRoot ? current.Status : await context.RuntimeStates.AsNoTracking()
                .Where(item => item.RunId == current.RootRunId && item.RootRunId == item.RunId)
                .Select(item => (ProcessRuntimeStatus?)item.Status).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (rootStatus is null or ProcessRuntimeStatus.CancelRequested or ProcessRuntimeStatus.Cancelled ||
                    !isRoot && ProcessRuntimeTerminalStates.IsRunTerminal(rootStatus.Value) ||
                    isRoot && (proposed.Status == ProcessRuntimeStatus.CancelRequested || ProcessRuntimeTerminalStates.IsRunTerminal(proposed.Status))) {
                return (Refuse(current, WorkAdmissionStopped,
                    $"Root '{current.RootRunId:D}' cannot admit new work with persisted status '{rootStatus}' and proposed run status '{proposed.Status}'."), false);
            }
        }
        if (!isRoot || current.Status == ProcessRuntimeStatus.Cancelled || proposed.Status != ProcessRuntimeStatus.Cancelled) {
            return (null, false);
        }
        if (HasAtomicCancelledResult(request)) {
            return (null, true);
        }
        var pending = await ReadPendingAsync(context, new(current.RootRunId), cancellationToken).ConfigureAwait(false);
        if (HasLocalRuntimeWork(proposed)) {
            pending |= ProcessRootCancellationPending.RuntimeWork;
        }
        return (pending == ProcessRootCancellationPending.None ? null : Refuse(current, WorkPending,
            $"Root '{current.RootRunId:D}' cannot finish cancellation while original work remains: {pending}. Settle or reconcile that work before retrying."), false);
    }

    internal static async Task RequireAfterFlushAsync(ProcessPersistenceDbContext context,
        ProcessRuntimeStateSnapshot state, CancellationToken cancellationToken) {
        var pending = await ReadPendingAsync(context, state.RootRunId, cancellationToken).ConfigureAwait(false);
        if (HasLocalRuntimeWork(state)) {
            pending |= ProcessRootCancellationPending.RuntimeWork;
        }
        if (state.RunId != state.RootRunId || state.Status != ProcessRuntimeStatus.Cancelled || pending != ProcessRootCancellationPending.None) {
            throw new InvalidOperationException($"{WorkPending}: Root '{state.RootRunId}' cannot commit its cancelled result while original work remains: {pending}.");
        }
    }

    private static bool HasAtomicCancelledResult(ProcessRuntimeCommitRequest request)
        => request.Decision is ProcessDecisionClosure { Completion: ProcessDecisionCompletion.Cancel } closure &&
            request.DecisionBudget is ProcessDecisionBudgetSettlement &&
            request.Mutation.State.AppliedResults.Any(result => result.StepInstanceId == closure.StepInstanceId &&
                result.AppliedStepStatus == ProcessRuntimeStepStatus.Cancelled && result.Outcome == StrategyOutcome.Canceled &&
                result.ExecutionRunId?.Value == closure.ChildId.Value && !request.OriginalState.AppliedResults.Any(original =>
                    original.StepInstanceId == result.StepInstanceId && original.StrategyId == result.StrategyId &&
                    original.IdempotencyKey == result.IdempotencyKey));

    private static bool HasLocalRuntimeWork(ProcessRuntimeStateSnapshot state)
        => state.Claims.Any(claim => ProcessRuntimeWorkAdmission.IsActive(claim.Status)) ||
            state.Steps.Any(step => step.ActiveClaimToken is not null || step.Status is ProcessRuntimeStepStatus.Claimed or ProcessRuntimeStepStatus.Running);

    internal static async Task<ProcessRootCancellationPending> ReadPendingAsync(ProcessPersistenceDbContext context,
        ProcessRunId rootRunId, CancellationToken cancellationToken = default) {
        if (rootRunId.Value == Guid.Empty) {
            throw new ArgumentOutOfRangeException(nameof(rootRunId));
        }
        var row = await context.RuntimeStates.AsNoTracking().Where(root => root.RunId == rootRunId.Value && root.RootRunId == root.RunId)
            .Select(root => new {
                RuntimeWork = context.RuntimeStates.Any(run => run.RootRunId == root.RunId && run.RunId != root.RunId &&
                    (run.Status != ProcessRuntimeStatus.Completed && run.Status != ProcessRuntimeStatus.Failed && run.Status != ProcessRuntimeStatus.Cancelled ||
                    context.DispatchClaims.Any(claim => claim.RunId == run.RunId &&
                        (claim.Status == DispatchClaimStatus.Claimed || claim.Status == DispatchClaimStatus.LeaseRenewed || claim.Status == DispatchClaimStatus.Reclaimed)) ||
                    context.RuntimeSteps.Any(step => step.RunId == run.RunId && (step.ActiveClaimToken != null ||
                        step.Status == ProcessRuntimeStepStatus.Claimed || step.Status == ProcessRuntimeStepStatus.Running)))),
                Decisions = context.DecisionHeads.Any(head => head.RootRunId == root.RunId &&
                    (!context.DecisionGenerations.Any(generation => generation.DecisionId == head.DecisionId && generation.Generation == head.CurrentGeneration &&
                        (generation.Phase == ProcessDecisionPhase.Completed || generation.Phase == ProcessDecisionPhase.Cancelled)) ||
                    context.DecisionGenerations.Any(generation => generation.DecisionId == head.DecisionId &&
                        (generation.UnclosedChildCount != 0 || generation.Phase != ProcessDecisionPhase.Completed &&
                        generation.Phase != ProcessDecisionPhase.Cancelled && generation.Phase != ProcessDecisionPhase.Superseded)) ||
                    context.DecisionChildren.Any(child => child.DecisionId == head.DecisionId && child.OwnerRevision == null))),
                Budget = context.DecisionBudgets.Any(budget => budget.RootRunId == root.RunId &&
                    (budget.ReservedWorkTicks != 0 || budget.ReservedReplans != 0 ||
                    context.DecisionBudgetAllocations.Any(allocation => allocation.BudgetId == budget.BudgetId && allocation.OwnerWorkRevision == null))),
                SharedScope = context.DecisionRunScopes.Any(scope =>
                    context.DecisionBudgets.Any(budget => budget.BudgetId == scope.BudgetId && budget.RootRunId == root.RunId) &&
                    (scope.Phase != ProcessDecisionRunScopePhase.Completed && scope.Phase != ProcessDecisionRunScopePhase.Cancelled ||
                    scope.CaptureAllocationId != null || scope.CaptureMaterialId != null || scope.CaptureKind != null))
            }).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null) {
            return ProcessRootCancellationPending.MissingRoot;
        }
        return (row.RuntimeWork ? ProcessRootCancellationPending.RuntimeWork : ProcessRootCancellationPending.None) |
            (row.Decisions ? ProcessRootCancellationPending.Decisions : ProcessRootCancellationPending.None) |
            (row.Budget ? ProcessRootCancellationPending.Budget : ProcessRootCancellationPending.None) |
            (row.SharedScope ? ProcessRootCancellationPending.SharedScope : ProcessRootCancellationPending.None);
    }

    private static ProcessRuntimeCommitResult Refuse(ProcessRuntimeStateEntity current, string code, string message)
        => ProcessRuntimeCommitResult.FromMutation(ProcessRuntimeMutation.Rejected(ProcessPersistenceMappers.ToSnapshot(current), code, message));
}
