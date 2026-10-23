using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Processes.Application;

internal sealed class ProcessDecisionRefreshCaptureSettlement(IProcessDecisionRefreshCaptureQuery query,
    IProcessDecisionRootQuery roots, IProcessDecisionResultQuery budgets, IProcessRuntimeStateStore states,
    IProcessRuntimeUnitOfWork unitOfWork, TimeProvider clock, ILogger logger) {
    private const int MaximumAttempts = 3;

    internal async Task<ProcessDecisionRefreshCaptureResult> FinishAsync(ProcessRunId runId, RuntimeCommandContext context,
        ProcessDecisionRefreshCaptureAttempt attempt, ProcessDecisionMaterial? material, ProcessDecisionRunScopeMaterial? basis,
        DecisionBudgetSettlement work, CancellationToken cancellationToken) {
        for (var retry = 0; retry < MaximumAttempts; retry++) {
            var state = await ReloadAsync(runId, cancellationToken).ConfigureAwait(false);
            var progress = await ProgressAsync(state, attempt.Identity, cancellationToken).ConfigureAwait(false);
            var completed = attempt.IsRoot ? progress.RootCompletion : progress.Successor;
            var aborted = attempt.IsRoot ? progress.RootAbort : progress.LocalAbort;
            if (completed is not null || aborted is not null) {
                attempt.RequireReceipt(completed ?? aborted!, completed is not null ? attempt.Complete : attempt.Abort,
                    work, completed is not null ? basis?.Stamp : null);
                return new(completed is not null ? ProcessDecisionRefreshCaptureStatus.Advanced
                    : Stopped(state.Status) ? ProcessDecisionRefreshCaptureStatus.Stopped : ProcessDecisionRefreshCaptureStatus.ReviewRequired, state);
            }
            try {
                ProcessDecisionRefreshCaptureSelection? current = null;
                if (basis is not null && !Stopped(state.Status)) {
                    try {
                        current = await query.ReadAsync(state, attempt.Identity.AdmissionId, cancellationToken).ConfigureAwait(false);
                    } catch (Exception failure) when (failure is ProcessExecutionAuthorityMismatchException or
                            ProcessDecisionSourceProjectionRequiredException or ProcessAcceptedExecutionInputRequiredException) {
                        logger.LogWarning("Refresh {AdmissionId} in run {RunId} lost original source eligibility before attachment.",
                            attempt.Identity.AdmissionId.Value, runId.Value);
                    }
                }
                var publish = basis is not null && current is not null && !Stopped(current.RootStatus) &&
                    !current.Original.Budget.OverrunDetected && !current.RootBudget.OverrunDetected &&
                    work.Consumed.WorkTicks <= attempt.Grant.WorkTicks && Fresh(basis, attempt) &&
                    current.Input.InputFingerprint == attempt.Original.Input.InputFingerprint &&
                    (attempt.IsRoot || current.SharedScope is null || current.SharedScope.Material == basis);
                var request = await CompletionAsync(state, context, attempt, current, publish ? material : null,
                    publish ? basis : null, work, cancellationToken).ConfigureAwait(false);
                var result = await unitOfWork.CommitAsync(request, cancellationToken).ConfigureAwait(false);
                attempt.RequireReceipt(result, request.CommandId, work, publish ? basis!.Stamp : null);
                return new(publish ? ProcessDecisionRefreshCaptureStatus.Advanced : Stopped(result.State.Status)
                    ? ProcessDecisionRefreshCaptureStatus.Stopped : ProcessDecisionRefreshCaptureStatus.ReviewRequired, result.State);
            } catch (ProcessDecisionException failure) when (failure.Reason == ProcessDecisionRefusal.UnclosedWork) {
                material = null;
                basis = null;
                logger.LogWarning("Refresh {AdmissionId} in run {RunId} cannot advance while other work is open; settling its closed capture.",
                    attempt.Identity.AdmissionId.Value, runId.Value);
            } catch (Exception failure) when (IsStale(failure) || failure is IOException or ProcessExecutionAuthorityMismatchException) {
                logger.LogWarning("Refresh {AdmissionId} in run {RunId} requires capture settlement reconciliation after {FailureKind}.",
                    attempt.Identity.AdmissionId.Value, runId.Value, failure.GetType().Name);
            }
        }
        throw new IOException("Refresh capture closed locally but its original settlement receipt could not be reconciled.");
    }

    private async Task<ProcessRuntimeCommitRequest> CompletionAsync(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        ProcessDecisionRefreshCaptureAttempt attempt, ProcessDecisionRefreshCaptureSelection? current, ProcessDecisionMaterial? material,
        ProcessDecisionRunScopeMaterial? basis, DecisionBudgetSettlement work, CancellationToken cancellationToken) {
        var (balance, scope) = await BudgetAsync(state, attempt.JournalReservation, cancellationToken).ConfigureAwait(false);
        var request = ProcessDecisionRefreshCaptureAttempt.TimestampOnly(state, basis is not null ? attempt.Complete : attempt.Abort, clock.GetUtcNow()) with {
            RefreshCapture = new(attempt.JournalReservation, basis is not null ? ProcessDecisionRefreshCaptureDisposition.Advanced : ProcessDecisionRefreshCaptureDisposition.Aborted),
            DecisionBudget = new ProcessDecisionBudgetSettlement(new(attempt.IsRoot ? null : attempt.Original.Original.StepInstanceId,
                balance.BudgetId, balance.Revision), attempt.Allocation, work)
        };
        if (attempt.IsRoot) {
            return request with { DecisionRunScope = new(scope!.Expectation, basis is not null
                ? ProcessDecisionRunScopeCommandKind.CompleteCapture : ProcessDecisionRunScopeCommandKind.AbortCapture, Material: basis?.Stamp),
                DecisionMaterial = material };
        }
        if (basis is null) {
            return request;
        }
        ProcessDecisionRefresh command = new(attempt.Original.Original.StepInstanceId, current!.Original.Head!.Expectation, attempt.Identity.Material,
            current.SharedScope is null ? ProcessDecisionRefreshKind.ManagerRefresh : ProcessDecisionRefreshKind.ParentRefresh) {
            AdmissionCommandId = attempt.Identity.AdmissionId, Material = basis.Stamp, InheritedScope = current.SharedScope?.Expectation
        };
        return request with {
            Mutation = new ProcessDecisionRefreshSuccessor(attempt.Identity.AdmissionId).Prepare(state,
                context with { OccurredAtUtc = clock.GetUtcNow() }, attempt.Intent, command),
            Decision = command, DecisionMaterial = material
        };
    }

    internal async Task<ProcessDecisionRefreshCaptureResult> AbortAsync(ProcessDecisionRefreshCaptureState journal, CancellationToken cancellationToken) {
        journal.Validate();
        if (journal.Phase != ProcessDecisionRefreshCapturePhase.Returned) {
            throw new ProcessDecisionRefreshCaptureException(ProcessDecisionRefreshCaptureRefusal.InvalidPhase);
        }
        var original = journal.Reservation;
        ProcessDecisionRefreshCaptureIdentity identity = new(original.AdmissionId);
        var abort = original.IsRoot ? identity.RootAbort : identity.LocalAbort;
        var work = journal.Outcome!.Work;
        for (var retry = 0; retry < MaximumAttempts; retry++) {
            var state = await ReloadAsync(original.RunId, cancellationToken).ConfigureAwait(false);
            var progress = await ProgressAsync(state, identity, cancellationToken).ConfigureAwait(false);
            if ((original.IsRoot ? progress.RootCompletion : progress.Successor) is not null) {
                return new(ProcessDecisionRefreshCaptureStatus.Advanced, state);
            }
            if ((original.IsRoot ? progress.RootAbort : progress.LocalAbort) is { } retained) {
                if (retained.Budget?.Command.Expectation.BudgetId != original.Budget.Expectation.BudgetId ||
                        retained.Budget.Change.Allocation.Grant != original.Budget.Grant || retained.Budget.Change.Allocation.Settlement != work) {
                    throw new InvalidDataException("Refresh recovery abort changed its original grant or returned work.");
                }
                return new(Stopped(state.Status) ? ProcessDecisionRefreshCaptureStatus.Stopped : ProcessDecisionRefreshCaptureStatus.ReviewRequired, state);
            }
            try {
                var (balance, scope) = await BudgetAsync(state, original, cancellationToken).ConfigureAwait(false);
                var request = ProcessDecisionRefreshCaptureAttempt.TimestampOnly(state, abort, clock.GetUtcNow()) with {
                    DecisionBudget = new ProcessDecisionBudgetSettlement(new(original.Budget.Expectation.StepInstanceId, balance.BudgetId, balance.Revision),
                        original.Budget.AllocationId, work),
                    DecisionRunScope = original.IsRoot ? new(scope!.Expectation, ProcessDecisionRunScopeCommandKind.AbortCapture) : null,
                    RefreshCapture = new(original, ProcessDecisionRefreshCaptureDisposition.Aborted)
                };
                var result = await unitOfWork.CommitAsync(request, cancellationToken).ConfigureAwait(false);
                ProcessDecisionRefreshCaptureReceipts.Require(new ProcessDecisionRefreshCaptureReceipt(result.Outcome, result.Decision,
                    result.DecisionBudget, result.DecisionRunScope), original.RootRunId, identity, abort);
                return new(Stopped(result.State.Status) ? ProcessDecisionRefreshCaptureStatus.Stopped : ProcessDecisionRefreshCaptureStatus.ReviewRequired, result.State);
            } catch (Exception failure) when (IsStale(failure) || failure is IOException) {
                logger.LogWarning("Refresh {AdmissionId} in run {RunId} needs its original returned-work abort retried after {FailureKind}.",
                    original.AdmissionId.Value, original.RunId.Value, failure.GetType().Name);
            }
        }
        throw new IOException("Refresh returned work remains retained for its original settlement.");
    }

    private async Task<(DecisionBudgetBalance Balance, ProcessDecisionRunScopeState? Scope)> BudgetAsync(ProcessRuntimeStateSnapshot state,
        ProcessDecisionRefreshCaptureReservation original, CancellationToken cancellationToken) {
        DecisionBudgetBalance balance;
        ProcessDecisionRunScopeState? scope = null;
        if (original.IsRoot) {
            var root = await roots.ReadAsync(state, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("Refresh settlement lost its original root budget.");
            balance = root.Budget;
            scope = root.Scope;
            ProcessDecisionRunScopeCapture capture = new(original.Budget.AllocationId, original.Material.MaterialId, ProcessDecisionRunCaptureKind.ManagerRefresh);
            if (scope.Admission.ScopeId != original.Scope.Id || scope.Capture != capture) {
                throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.StaleExpectation);
            }
        } else {
            var step = original.Budget.Expectation.StepInstanceId!.Value;
            var selected = await budgets.ReadAsync(state.RunId, step, null, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("Refresh settlement lost its original local budget.");
            selected.RequireOriginal(state, step, null);
            balance = selected.Budget;
        }
        if (balance.BudgetId != original.Budget.Expectation.BudgetId) {
            throw new InvalidDataException("Refresh capture cannot settle a replacement budget.");
        }
        return (balance, scope);
    }

    private async Task<ProcessDecisionRefreshCaptureProgress> ProgressAsync(ProcessRuntimeStateSnapshot state,
        ProcessDecisionRefreshCaptureIdentity identity, CancellationToken cancellationToken) {
        var progress = await query.ReadProgressAsync(state.RunId, identity.AdmissionId, cancellationToken).ConfigureAwait(false);
        ProcessDecisionRefreshCaptureReceipts.Require(progress, state.RootRunId, identity);
        return progress;
    }

    private bool Fresh(ProcessDecisionRunScopeMaterial material, ProcessDecisionRefreshCaptureAttempt attempt) {
        var now = clock.GetUtcNow();
        return now >= material.CapturedAtUtc && now - material.CapturedAtUtc <= attempt.Original.Input.Assignment.DecisionDataPolicy!.Policy.Budget.MaximumBasisAge;
    }

    private async Task<ProcessRuntimeStateSnapshot> ReloadAsync(ProcessRunId runId, CancellationToken cancellationToken)
        => await states.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The original Process runtime disappeared during refresh capture.");

    private static bool Stopped(ProcessRuntimeStatus status)
        => status == ProcessRuntimeStatus.CancelRequested || ProcessRuntimeTerminalStates.IsRunTerminal(status);

    private static bool IsStale(Exception failure) => failure is ProcessRuntimeOptimisticConcurrencyException or
        ProcessDecisionException { Reason: ProcessDecisionRefusal.StaleExpectation } or
        ProcessDecisionRunScopeException { Reason: ProcessDecisionRunScopeRefusal.StaleExpectation };
}
