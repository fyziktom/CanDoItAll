using System.Runtime.ExceptionServices;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Processes.Application;

public enum ProcessDecisionRefreshCaptureStatus { Advanced, Pending, ReviewRequired, Stopped, SourceProjectionRequired }

public sealed record ProcessDecisionRefreshCaptureResult(ProcessDecisionRefreshCaptureStatus Status, ProcessRuntimeStateSnapshot State);

public sealed class ProcessDecisionRefreshCaptureCoordinator(IProcessDecisionRefreshCaptureQuery query, IProcessDecisionRefreshCaptureStore journal,
    IProcessDecisionRootQuery roots, IProcessDecisionResultQuery budgets, IEnumerable<IProcessDecisionMaterialCapturer> capturers,
    IProcessRuntimeStateStore states, IProcessRuntimeUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<ProcessDecisionRefreshCaptureCoordinator> logger) {
    private const int MaximumAttempts = 3;
    private readonly ProcessDecisionRefreshCaptureSettlement settlement = new(query, roots, budgets, states, unitOfWork, clock, logger);

    public async Task<ProcessDecisionRefreshCaptureResult> PrepareAsync(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        RuntimeCommandId admissionId, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        context.Validate();
        ProcessDecisionRefreshCaptureIdentity identity = new(admissionId);
        for (var retry = 0; retry < MaximumAttempts; retry++) {
            state = await ReloadAsync(state.RunId, cancellationToken).ConfigureAwait(false);
            var progress = await ProgressAsync(state, identity, cancellationToken).ConfigureAwait(false);
            if (progress.Successor is not null) {
                return new(ProcessDecisionRefreshCaptureStatus.Advanced, state);
            }
            if (progress.RootAbort is not null || progress.LocalAbort is not null) {
                return new(Stopped(state.Status) ? ProcessDecisionRefreshCaptureStatus.Stopped : ProcessDecisionRefreshCaptureStatus.ReviewRequired, state);
            }
            var pending = progress.LocalReservation is not null ? identity.LocalReserve
                : progress.RootReservation is not null && progress.RootCompletion is null ? identity.RootReserve : (RuntimeCommandId?)null;
            if (pending is { } reservationId) {
                var resumed = await ResumeAsync(state, context, identity, reservationId, progress, cancellationToken).ConfigureAwait(false);
                if (reservationId == identity.RootReserve && resumed.Status == ProcessDecisionRefreshCaptureStatus.Advanced) {
                    continue;
                }
                return resumed;
            }
            var captureInvoked = false;
            try {
                var selected = await query.ReadAsync(state, admissionId, cancellationToken).ConfigureAwait(false);
                if (Stopped(state.Status) || Stopped(selected.RootStatus)) {
                    return new(ProcessDecisionRefreshCaptureStatus.Stopped, state);
                }
                if (selected.Original.OtherWorkPending || selected.SharedScope is not null && selected.RootWorkPending) {
                    return new(ProcessDecisionRefreshCaptureStatus.Pending, state);
                }
                var root = selected.SharedScope is not null && progress.RootCompletion is null;
                if (root && (progress.RootReservation is not null || selected.SharedScope!.Phase != ProcessDecisionRunScopePhase.Reconciling)) {
                    return new(ProcessDecisionRefreshCaptureStatus.Pending, state);
                }
                if (selected.SharedScope is not null && !root &&
                        (selected.SharedScope.Phase != ProcessDecisionRunScopePhase.Active ||
                            selected.SharedScope.Material?.Stamp != progress.RootCompletion!.Scope!.State.Material?.Stamp ||
                            selected.SharedScope.Expectation != progress.RootCompletion.Scope.State.Expectation)) {
                    return new(ProcessDecisionRefreshCaptureStatus.ReviewRequired, state);
                }
                var attempt = ProcessDecisionRefreshCaptureAttempt.Create(identity, selected, root);
                if (ProcessDecisionReviewCompletion.ValidatePending(state, attempt.Intent.Request.Child,
                        attempt.Intent.Request.StrategyId, attempt.Intent.Request.WaitingResultKey) is not null) {
                    return new(ProcessDecisionRefreshCaptureStatus.ReviewRequired, state);
                }
                var capturer = root || selected.SharedScope is null
                    ? capturers.SingleOrDefault(item => item.ContentContract == attempt.CaptureRequest.ContentContract)
                        ?? throw new InvalidOperationException("The original refresh content contract has no registered capturer.")
                    : null;
                var reserved = await unitOfWork.CommitAsync(attempt.Reservation(state, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
                attempt.RequireReceipt(reserved, attempt.Reserve);
                if (!reserved.CommittedNow) {
                    return new(ProcessDecisionRefreshCaptureStatus.Pending, reserved.State);
                }
                captureInvoked = true;
                var result = await InvokeAsync(reserved.State, context, attempt, capturer, cancellationToken).ConfigureAwait(false);
                if (!root || result.Status != ProcessDecisionRefreshCaptureStatus.Advanced) {
                    return result;
                }
                state = result.State;
            } catch (Exception failure) when (!captureInvoked && (IsStale(failure) || failure is IOException)) {
                logger.LogWarning("Refresh {AdmissionId} in run {RunId} requires progress reconciliation after {FailureKind}.",
                    admissionId.Value, state.RunId.Value, failure.GetType().Name);
            } catch (Exception failure) when (!captureInvoked && failure is (DecisionBudgetException or
                    ProcessDecisionException { Reason: ProcessDecisionRefusal.GenerationCapacity } or
                    ProcessDecisionRunScopeException { Reason: ProcessDecisionRunScopeRefusal.MembershipCapacity })) {
                return new(ProcessDecisionRefreshCaptureStatus.ReviewRequired, state);
            } catch (Exception failure) when (!captureInvoked && failure is (ProcessDecisionSourceProjectionRequiredException or ProcessAcceptedExecutionInputRequiredException)) {
                return new(ProcessDecisionRefreshCaptureStatus.SourceProjectionRequired, state);
            }
        }
        return new(ProcessDecisionRefreshCaptureStatus.Pending, state);
    }

    private async Task<ProcessDecisionRefreshCaptureResult> CaptureAsync(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        ProcessDecisionRefreshCaptureAttempt attempt, ProcessDecisionCaptureInvocationId invocation,
        IProcessDecisionMaterialCapturer? capturer, CancellationToken cancellationToken) {
        ProcessDecisionMaterial? material = null;
        ProcessDecisionRunScopeMaterial? basis = null;
        Exception? failure = null;
        var kind = ProcessDecisionRefreshCaptureOutcomeKind.Refused;
        using var deadline = new CancellationTokenSource(TimeSpan.FromTicks(attempt.Grant.WorkTicks), clock);
        using var captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var started = clock.GetTimestamp();
        try {
            if (capturer is not null) {
                var request = attempt.CaptureRequest;
                switch (await capturer.CaptureAsync(request, captureCancellation.Token).ConfigureAwait(false)) {
                    case ProcessDecisionMaterialCaptureResult.Captured captured:
                        material = captured.Material ?? throw new InvalidDataException("Refresh capture returned no material.");
                        if (material.Scope != request.Scope || material.Reference != request.Material || material.ContentContract != request.ContentContract) {
                            throw new InvalidDataException("Refresh capture changed its original scope, material identity or content contract.");
                        }
                        basis = ProcessDecisionRunScopeMaterial.From(material);
                        kind = ProcessDecisionRefreshCaptureOutcomeKind.Captured;
                        break;
                    case ProcessDecisionMaterialCaptureResult.Refused refused when Enum.IsDefined(refused.Reason):
                        logger.LogWarning("Refresh {AdmissionId} in run {RunId} was refused by its content owner: {Reason}.",
                            attempt.Identity.AdmissionId.Value, state.RunId.Value, refused.Reason);
                        break;
                    default:
                        throw new InvalidDataException("Refresh capture returned an unsupported content outcome.");
                }
            } else {
                var shared = await roots.ReadAsync(state, captureCancellation.Token).ConfigureAwait(false)
                    ?? throw new InvalidDataException("Refresh inheritance lost its original root scope.");
                if (shared.Scope.Expectation != attempt.Original.SharedScope!.Expectation ||
                        shared.Scope.Phase != ProcessDecisionRunScopePhase.Active || shared.Scope.Material != attempt.Original.SharedScope.Material) {
                    throw new ProcessDecisionRunScopeException(ProcessDecisionRunScopeRefusal.StaleExpectation);
                }
                basis = shared.Scope.Material;
                kind = ProcessDecisionRefreshCaptureOutcomeKind.Captured;
            }
            if (basis is not null && !Fresh(basis, attempt)) {
                basis = null;
                material = null;
                logger.LogWarning("Refresh {AdmissionId} in run {RunId} returned expired material.", attempt.Identity.AdmissionId.Value, state.RunId.Value);
                kind = ProcessDecisionRefreshCaptureOutcomeKind.Expired;
            }
        } catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
            material = null;
            basis = null;
            kind = ProcessDecisionRefreshCaptureOutcomeKind.DeadlineExceeded;
        } catch (Exception exception) {
            failure = exception;
            kind = exception is OperationCanceledException && cancellationToken.IsCancellationRequested
                ? ProcessDecisionRefreshCaptureOutcomeKind.Cancelled : ProcessDecisionRefreshCaptureOutcomeKind.Faulted;
            material = null;
            basis = null;
            logger.LogWarning("Refresh {AdmissionId} in run {RunId} ended capture with {FailureKind}.",
                attempt.Identity.AdmissionId.Value, state.RunId.Value, exception.GetType().Name);
        }
        var elapsed = clock.GetElapsedTime(started);
        if (elapsed < TimeSpan.Zero) {
            throw new InvalidDataException("Refresh capture returned a negative monotonic work duration.");
        }
        if (elapsed.Ticks > attempt.Grant.WorkTicks || captureCancellation.IsCancellationRequested) {
            material = null;
            basis = null;
            kind = elapsed.Ticks > attempt.Grant.WorkTicks || deadline.IsCancellationRequested
                ? ProcessDecisionRefreshCaptureOutcomeKind.DeadlineExceeded : ProcessDecisionRefreshCaptureOutcomeKind.Cancelled;
        }
        using var accountingDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5), clock);
        ProcessDecisionRefreshCaptureReturned returned = new(new(kind, new(1, new(elapsed.Ticks, 0)), basis), material);
        await journal.ReturnAsync(attempt.JournalReservation, invocation, returned, accountingDeadline.Token).ConfigureAwait(false);
        var result = await settlement.FinishAsync(state.RunId, context, attempt, material, basis,
            returned.Outcome.Work, accountingDeadline.Token).ConfigureAwait(false);
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private async Task<ProcessDecisionRefreshCaptureResult> InvokeAsync(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        ProcessDecisionRefreshCaptureAttempt attempt, IProcessDecisionMaterialCapturer? capturer, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested) {
            using var draining = new CancellationTokenSource(TimeSpan.FromSeconds(5), clock);
            var neverInvoked = await journal.CloseNeverInvokedAsync(attempt.JournalReservation, draining.Token).ConfigureAwait(false);
            await settlement.AbortAsync(neverInvoked, draining.Token).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        ProcessDecisionCaptureInvocationId invocation = new(Guid.NewGuid());
        var admission = await journal.BeginAsync(attempt.JournalReservation, invocation, cancellationToken).ConfigureAwait(false);
        if (admission.Status == ProcessDecisionRefreshCaptureInvocationStatus.PreviouslyAdmitted) {
            return new(ProcessDecisionRefreshCaptureStatus.Pending, state);
        }
        if (admission.Status == ProcessDecisionRefreshCaptureInvocationStatus.Stopped) {
            var neverInvoked = await journal.CloseNeverInvokedAsync(attempt.JournalReservation, cancellationToken).ConfigureAwait(false);
            return await settlement.AbortAsync(neverInvoked, cancellationToken).ConfigureAwait(false);
        }
        return await CaptureAsync(state, context, attempt, invocation, capturer, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProcessDecisionRefreshCaptureResult> ResumeAsync(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        ProcessDecisionRefreshCaptureIdentity identity, RuntimeCommandId reservationId, ProcessDecisionRefreshCaptureProgress progress, CancellationToken cancellationToken) {
        var saved = await journal.ReadAsync(state.RunId, reservationId, cancellationToken).ConfigureAwait(false);
        if (saved is null) {
            return new(ProcessDecisionRefreshCaptureStatus.Pending, state);
        }
        var original = saved.Reservation;
        var receipt = original.IsRoot ? progress.RootReservation : progress.LocalReservation;
        if (original.RunId != state.RunId || original.RootRunId != state.RootRunId || original.ReservationId != reservationId ||
                original.AdmissionId != identity.AdmissionId || receipt?.Budget?.Command != original.Budget) {
            throw new InvalidDataException("Refresh recovery journal differs from its original reservation receipt.");
        }
        if (saved.Phase is ProcessDecisionRefreshCapturePhase.Invoking or ProcessDecisionRefreshCapturePhase.Settled) {
            return new(ProcessDecisionRefreshCaptureStatus.Pending, state);
        }
        ProcessDecisionRefreshCaptureSelection? selected = null;
        try {
            if (!Stopped(state.Status)) {
                selected = await query.ReadAsync(state, identity.AdmissionId, cancellationToken).ConfigureAwait(false);
            }
        } catch (Exception failure) when (failure is ProcessExecutionAuthorityMismatchException or
                ProcessDecisionSourceProjectionRequiredException or ProcessAcceptedExecutionInputRequiredException) {
            logger.LogWarning("Refresh {AdmissionId} in run {RunId} lost source eligibility while recovering known work: {FailureKind}.",
                identity.AdmissionId.Value, state.RunId.Value, failure.GetType().Name);
        }
        if (selected is null || Stopped(state.Status) || Stopped(selected.RootStatus) ||
                saved.Outcome is { Kind: not ProcessDecisionRefreshCaptureOutcomeKind.Captured }) {
            if (saved.Phase == ProcessDecisionRefreshCapturePhase.Reserved) {
                saved = await journal.CloseNeverInvokedAsync(original, cancellationToken).ConfigureAwait(false);
            }
            return await settlement.AbortAsync(saved, cancellationToken).ConfigureAwait(false);
        }
        var attempt = ProcessDecisionRefreshCaptureAttempt.Restore(selected, original);
        if (saved.Phase == ProcessDecisionRefreshCapturePhase.Returned) {
            var returned = await journal.ReadReturnedAsync(state.RunId, reservationId, cancellationToken).ConfigureAwait(false);
            if (returned is null) {
                return new(ProcessDecisionRefreshCaptureStatus.Pending, state);
            }
            return await settlement.FinishAsync(state.RunId, context, attempt, returned.Payload, returned.Outcome.Material,
                returned.Outcome.Work, cancellationToken).ConfigureAwait(false);
        }
        var capturer = original.InheritsMaterial ? null : capturers.SingleOrDefault(item => item.ContentContract == original.ContentContract)
            ?? throw new InvalidOperationException("The original refresh content contract has no registered capturer.");
        return await InvokeAsync(state, context, attempt, capturer, cancellationToken).ConfigureAwait(false);
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
