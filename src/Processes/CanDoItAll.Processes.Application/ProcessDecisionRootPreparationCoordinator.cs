using System.Runtime.ExceptionServices;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessDecisionRootPreparationCoordinator(IProcessDecisionRootQuery query,
    IEnumerable<IProcessDecisionRootCapturer> capturers, IProcessRuntimeStateStore states, IProcessIdempotencyStore receipts,
    IProcessRuntimeUnitOfWork unitOfWork, TimeProvider clock, ILogger<ProcessDecisionRootPreparationCoordinator> logger) {
    private const int MaximumAttempts = 3;

    public async Task<ProcessDecisionRootPreparationResult> PrepareAsync(ProcessRuntimeStateSnapshot state, CancellationToken cancellationToken = default) {
        for (var retry = 0; retry < MaximumAttempts; retry++) {
            ProcessDecisionRootSelection? selected;
            try {
                selected = await query.ReadAsync(state, cancellationToken).ConfigureAwait(false);
            } catch (ProcessDecisionSourceProjectionRequiredException) {
                return new(ProcessDecisionRootPreparationStatus.SourceProjectionRequired, state);
            }
            if (selected is null) {
                return new(ProcessDecisionRootPreparationStatus.NotRequired, state);
            }
            if (Readiness(state, selected) is { } ready) {
                return new(ready, state);
            }
            var capturer = capturers.SingleOrDefault(item => item.ContentContract == selected.Scope.Admission.ContentContract)
                ?? throw new InvalidOperationException("The original root content contract has no registered capturer.");
            ProcessDecisionRootCaptureAttempt attempt;
            try {
                attempt = ProcessDecisionRootCaptureAttempt.Create(selected);
            } catch (DecisionBudgetException) {
                return new(ProcessDecisionRootPreparationStatus.ReviewRequired, state);
            }
            if (await receipts.FindCompletedCommandAsync(attempt.ReserveId, cancellationToken).ConfigureAwait(false) is { } replay) {
                attempt.RequireReceipt(replay, ProcessDecisionRunScopeCommandKind.BeginCapture, selected);
                return new(ProcessDecisionRootPreparationStatus.CapturePending, state);
            }
            ProcessRuntimeCommitResult reserved;
            try {
                reserved = await unitOfWork.CommitAsync(Request(state, attempt.ReserveId,
                    new(attempt.OriginalScope, ProcessDecisionRunScopeCommandKind.BeginCapture, attempt.Capture),
                    new ProcessDecisionBudgetReservation(new(null, selected.Budget.BudgetId, selected.Budget.Revision),
                        attempt.Capture.AllocationId, attempt.Grant)), cancellationToken).ConfigureAwait(false);
            } catch (Exception failure) when (IsStale(failure)) {
                state = await ReloadAsync(state.RunId, cancellationToken).ConfigureAwait(false);
                continue;
            }
            attempt.RequireReceipt(reserved, ProcessDecisionRunScopeCommandKind.BeginCapture, selected);
            if (!reserved.CommittedNow) {
                return new(ProcessDecisionRootPreparationStatus.CapturePending, reserved.State);
            }
            return await CaptureAsync(reserved.State, selected, attempt, capturer, cancellationToken).ConfigureAwait(false);
        }
        return new(ProcessDecisionRootPreparationStatus.CapturePending, state);
    }

    private ProcessDecisionRootPreparationStatus? Readiness(ProcessRuntimeStateSnapshot state, ProcessDecisionRootSelection selected) {
        if (Stopped(state.Status) || Stopped(selected.RootStatus)) {
            return ProcessDecisionRootPreparationStatus.Stopped;
        }
        if (selected.Budget.OverrunDetected) {
            return ProcessDecisionRootPreparationStatus.ReviewRequired;
        }
        switch (selected.Scope.Phase) {
            case ProcessDecisionRunScopePhase.Active:
                var captured = selected.Scope.Material!.CapturedAtUtc;
                var now = clock.GetUtcNow();
                return now < captured || now - captured > selected.Root.Policy.Budget.MaximumBasisAge
                    ? ProcessDecisionRootPreparationStatus.ReviewRequired : ProcessDecisionRootPreparationStatus.Ready;
            case ProcessDecisionRunScopePhase.AwaitingCapture:
                return state.RunId == selected.Root.RunId ? null : ProcessDecisionRootPreparationStatus.CapturePending;
            case ProcessDecisionRunScopePhase.Capturing:
                return ProcessDecisionRootPreparationStatus.CapturePending;
            case ProcessDecisionRunScopePhase.Reconciling:
                return ProcessDecisionRootPreparationStatus.ReviewRequired;
            case ProcessDecisionRunScopePhase.Completed or ProcessDecisionRunScopePhase.Cancelled:
                return ProcessDecisionRootPreparationStatus.Stopped;
            default:
                throw new InvalidDataException("The original root capture phase is unsupported.");
        }
    }

    private async Task<ProcessDecisionRootPreparationResult> CaptureAsync(ProcessRuntimeStateSnapshot state,
        ProcessDecisionRootSelection selected, ProcessDecisionRootCaptureAttempt attempt, IProcessDecisionRootCapturer capturer,
        CancellationToken cancellationToken) {
        ProcessDecisionMaterial? material = null;
        Exception? failure = null;
        using var deadline = new CancellationTokenSource(TimeSpan.FromTicks(attempt.Grant.WorkTicks), clock);
        using var captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var started = clock.GetTimestamp();
        try {
            var reference = new ProcessDecisionMaterialReference(selected.Scope.Admission.ScopeId, 1, attempt.Capture.MaterialId);
            var outcome = await capturer.CaptureAsync(new(selected.Source, selected.Scope.Admission, reference),
                captureCancellation.Token).ConfigureAwait(false);
            switch (outcome) {
                case ProcessDecisionRootCaptureResult.Captured captured:
                    material = captured.Material ?? throw new InvalidDataException("The root capturer returned no material.");
                    if (material.Scope != selected.Scope.Admission.MaterialScope || material.Reference != reference ||
                            material.ContentContract != capturer.ContentContract) {
                        throw new InvalidDataException("The root capturer changed the original scope, material identity or content contract.");
                    }
                    material.RequireFresh(clock.GetUtcNow(), selected.Root.Policy.Budget.MaximumBasisAge);
                    break;
                case ProcessDecisionRootCaptureResult.Refused refused when Enum.IsDefined(refused.Reason):
                    logger.LogWarning("Root capture for run {RunId}, scope {ScopeId} was refused: {Reason}.",
                        state.RunId, selected.Scope.Admission.ScopeId, refused.Reason);
                    break;
                default:
                    throw new InvalidDataException("The root capturer returned an unsupported result.");
            }
        } catch (Exception exception) {
            failure = exception;
            material = null;
            logger.LogWarning("Root capture for run {RunId}, scope {ScopeId} ended with {FailureKind}.",
                state.RunId, selected.Scope.Admission.ScopeId, exception.GetType().Name);
        }
        var elapsed = clock.GetElapsedTime(started);
        if (elapsed < TimeSpan.Zero) {
            throw new InvalidDataException("Root capture returned a negative monotonic work duration.");
        }
        if (elapsed.Ticks > attempt.Grant.WorkTicks || captureCancellation.IsCancellationRequested) {
            material = null;
        }
        using var accountingDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5), clock);
        var result = await FinishAsync(state.RunId, selected, attempt, material, new(1, new(elapsed.Ticks, 0)),
            accountingDeadline.Token).ConfigureAwait(false);
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private async Task<ProcessDecisionRootPreparationResult> FinishAsync(ProcessRunId runId, ProcessDecisionRootSelection original,
        ProcessDecisionRootCaptureAttempt attempt, ProcessDecisionMaterial? material, DecisionBudgetSettlement work, CancellationToken cancellationToken) {
        for (var retry = 0; retry < MaximumAttempts; retry++) {
            foreach (var (id, kind) in new[] {
                (attempt.CompleteId, ProcessDecisionRunScopeCommandKind.CompleteCapture), (attempt.AbortId, ProcessDecisionRunScopeCommandKind.AbortCapture)
            }) {
                if (await receipts.FindCompletedCommandAsync(id, cancellationToken).ConfigureAwait(false) is { } replay) {
                    attempt.RequireReceipt(replay, kind, original, work);
                    var observed = await ReloadAsync(runId, cancellationToken).ConfigureAwait(false);
                    var current = await query.ReadAsync(observed, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidDataException("The original root admission disappeared after capture.");
                    return new(kind == ProcessDecisionRunScopeCommandKind.AbortCapture
                        ? Stopped(observed.Status) ? ProcessDecisionRootPreparationStatus.Stopped : ProcessDecisionRootPreparationStatus.ReviewRequired
                        : Readiness(observed, current) ?? ProcessDecisionRootPreparationStatus.ReviewRequired, observed);
                }
            }
            var state = await ReloadAsync(runId, cancellationToken).ConfigureAwait(false);
            var selected = await query.ReadAsync(state, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("The original root admission disappeared during capture.");
            if (selected.Scope.Capture != attempt.Capture || selected.Scope.Admission != original.Scope.Admission) {
                throw new InvalidDataException("The original root capture ownership changed before settlement.");
            }
            var publish = material is not null && !Stopped(state.Status) && !Stopped(selected.RootStatus) &&
                !selected.Budget.OverrunDetected && work.Consumed.WorkTicks <= attempt.Grant.WorkTicks;
            var kindToCommit = publish ? ProcessDecisionRunScopeCommandKind.CompleteCapture : ProcessDecisionRunScopeCommandKind.AbortCapture;
            var request = Request(state, publish ? attempt.CompleteId : attempt.AbortId,
                new(selected.Scope.Expectation, kindToCommit, Material: publish ? material!.Stamp : null),
                new ProcessDecisionBudgetSettlement(new(null, selected.Budget.BudgetId, selected.Budget.Revision), attempt.Capture.AllocationId, work))
                with { DecisionMaterial = publish ? material : null };
            try {
                var committed = await unitOfWork.CommitAsync(request, cancellationToken).ConfigureAwait(false);
                attempt.RequireReceipt(committed, kindToCommit, original, work);
                return new(Stopped(committed.State.Status) ? ProcessDecisionRootPreparationStatus.Stopped
                    : publish ? ProcessDecisionRootPreparationStatus.Ready : ProcessDecisionRootPreparationStatus.ReviewRequired, committed.State);
            } catch (Exception failure) when (IsStale(failure) || failure is IOException) {
                logger.LogWarning("Root capture settlement for run {RunId}, allocation {AllocationId} requires receipt reconciliation after {FailureKind}.",
                    runId, attempt.Capture.AllocationId, failure.GetType().Name);
            }
        }
        throw new IOException("Root capture closed locally but its original accounting receipt could not be reconciled.");
    }

    private ProcessRuntimeCommitRequest Request(ProcessRuntimeStateSnapshot state, RuntimeCommandId commandId,
        ProcessDecisionRunScopeCommand scope, ProcessDecisionBudgetCommand budget) {
        var now = clock.GetUtcNow();
        var ticks = Math.Max(now.UtcTicks - now.UtcTicks % 10, checked(state.UpdatedAtUtc.UtcTicks + 10));
        return new(commandId, state, new(ProcessRuntimeTransitionOutcome.Applied, state with {
            UpdatedAtUtc = new(ticks, TimeSpan.Zero)
        }, [], [], [], [])) { DecisionRunScope = scope, DecisionBudget = budget };
    }

    private async Task<ProcessRuntimeStateSnapshot> ReloadAsync(ProcessRunId runId, CancellationToken cancellationToken)
        => await states.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The original Process runtime disappeared during root capture.");

    private static bool Stopped(ProcessRuntimeStatus status)
        => status == ProcessRuntimeStatus.CancelRequested || ProcessRuntimeTerminalStates.IsRunTerminal(status);

    private static bool IsStale(Exception failure) => failure is ProcessRuntimeOptimisticConcurrencyException or
        ProcessDecisionException { Reason: ProcessDecisionRefusal.StaleExpectation } or
        ProcessDecisionRunScopeException { Reason: ProcessDecisionRunScopeRefusal.StaleExpectation };
}
