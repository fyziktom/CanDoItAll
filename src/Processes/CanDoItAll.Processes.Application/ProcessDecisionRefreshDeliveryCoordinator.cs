using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Processes.Application;

public enum ProcessDecisionRefreshDeliveryCapturePending { Unsettled, UnknownInvocation, LegacyUnknown }

public abstract record ProcessDecisionRefreshDeliveryResult {
    private protected ProcessDecisionRefreshDeliveryResult() { }
    public sealed record Advanced : ProcessDecisionRefreshDeliveryResult;
    public sealed record CompletedOriginal : ProcessDecisionRefreshDeliveryResult;
    public sealed record Stopped : ProcessDecisionRefreshDeliveryResult;
    public sealed record ReviewRequired : ProcessDecisionRefreshDeliveryResult;
    public sealed record SourceProjectionRequired : ProcessDecisionRefreshDeliveryResult;
    public sealed record Contended : ProcessDecisionRefreshDeliveryResult;
    public sealed record PendingStop(ProcessDecisionChildStopResult Reason) : ProcessDecisionRefreshDeliveryResult;
    public sealed record PendingChild(ProcessDecisionChildPendingReason Reason) : ProcessDecisionRefreshDeliveryResult;
    public sealed record PendingCapture(ProcessDecisionRefreshDeliveryCapturePending Reason) : ProcessDecisionRefreshDeliveryResult;
}

public sealed class ProcessDecisionRefreshDeliveryCoordinator(IProcessRuntimeStateStore states,
    IProcessDecisionRefreshReceiptReader admissions, IProcessDecisionResultQuery children,
    IProcessRootCancellationBarrierReader roots,
    IEnumerable<IProcessDecisionRefreshChildStopper> stoppers, ProcessDecisionRefreshAccountingCoordinator accounting,
    ProcessDecisionRefreshCaptureCoordinator captures, IProcessDecisionRefreshCaptureQuery progress,
    IProcessDecisionRefreshCaptureStore journal, TimeProvider clock, ILogger<ProcessDecisionRefreshDeliveryCoordinator> logger) {
    private readonly IReadOnlyDictionary<ProcessDecisionExecutionContract, IProcessDecisionRefreshChildStopper> owners = stoppers.ToDictionary(item => item.Contract);
    private static readonly ProcessCorrelationId Correlation = new("process-refresh-delivery");

    public async Task<ProcessDecisionRefreshDeliveryResult> AdvanceAsync(ProcessRunId runId, RuntimeCommandId admissionId,
        CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfEqual(runId.Value, Guid.Empty);
        ProcessDecisionRefreshCaptureIdentity identity = new(admissionId);
        var state = await LoadAsync(runId, cancellationToken).ConfigureAwait(false);
        var admission = await admissions.ReadAsync(runId, admissionId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Refresh delivery requires its retained original admission.");
        if (admission.Decision.Command is not ProcessDecisionBeginRefresh begin || begin.Intent.Request.CommandId != admissionId ||
                begin.Intent.Request.Child.RunId != runId || begin.Intent.Request.Child.RootRunId != state.RootRunId) {
            throw new InvalidDataException("Refresh delivery received another action, run or root.");
        }
        var intent = admission.RequireOriginal(begin.Intent.Request, begin.Intent.Actor);
        RuntimeCommandContext context = new(admissionId, intent.Actor, Correlation, clock.GetUtcNow());
        try {
            var saved = await progress.ReadProgressAsync(runId, admissionId, cancellationToken).ConfigureAwait(false);
            ProcessDecisionRefreshCaptureReceipts.Require(saved, state.RootRunId, identity);
            if (saved.Successor is not null) {
                return new ProcessDecisionRefreshDeliveryResult.Advanced();
            }
            if (saved.RootAbort is not null || saved.LocalAbort is not null) {
                return await StoppedAsync(state, cancellationToken).ConfigureAwait(false)
                    ? new ProcessDecisionRefreshDeliveryResult.Stopped() : new ProcessDecisionRefreshDeliveryResult.ReviewRequired();
            }
            var accounted = await accounting.ReadCompletedAsync(state, admissionId, cancellationToken).ConfigureAwait(false);
            if (accounted is ProcessDecisionRefreshAccountingResult.CompletedOriginal) {
                return new ProcessDecisionRefreshDeliveryResult.CompletedOriginal();
            }
            if (await StoppedAsync(state, cancellationToken).ConfigureAwait(false)) {
                if (PendingReservation(saved, identity) is null) {
                    return new ProcessDecisionRefreshDeliveryResult.Stopped();
                }
                if (state.Status != ProcessRuntimeStatus.CancelRequested && !ProcessRuntimeTerminalStates.IsRunTerminal(state.Status)) {
                    return new ProcessDecisionRefreshDeliveryResult.Contended();
                }
                return await CaptureAsync(state, context, identity, cancellationToken).ConfigureAwait(false);
            }
            if (accounted is null) {
                var address = intent.Request.Child;
                var selected = await children.ReadAsync(runId, address.StepInstanceId, new(address.ChildId.Value), cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidDataException("Refresh delivery lost its original child.");
                selected.RequireOriginal(state, address.StepInstanceId, new(address.ChildId.Value));
                var original = selected.Child?.Original ?? throw new InvalidDataException("Refresh delivery lost its original child binding.");
                if (original.Child.Address != address || ProcessDecisionChildMaterialBindingCodec.Fingerprint(original) != intent.ChildFingerprint) {
                    throw new InvalidDataException("Refresh delivery changed its original child binding.");
                }
                if (!owners.TryGetValue(original.ExecutionContract, out var owner)) {
                    throw new InvalidOperationException("The original refresh execution contract has no registered stop owner.");
                }
                var stopped = await owner.StopAsync(original, admissionId, cancellationToken).ConfigureAwait(false);
                if (!Enum.IsDefined(stopped)) {
                    throw new InvalidDataException("The original refresh stop owner returned an unknown outcome.");
                }
                if (stopped != ProcessDecisionChildStopResult.ReadyForAccounting) {
                    return new ProcessDecisionRefreshDeliveryResult.PendingStop(stopped);
                }
                state = await LoadAsync(runId, cancellationToken).ConfigureAwait(false);
                if (await StoppedAsync(state, cancellationToken).ConfigureAwait(false)) {
                    return new ProcessDecisionRefreshDeliveryResult.Stopped();
                }
                switch (await accounting.ReconcileAsync(state, context, admissionId, cancellationToken).ConfigureAwait(false)) {
                    case ProcessDecisionRefreshAccountingResult.CompletedOriginal:
                        return new ProcessDecisionRefreshDeliveryResult.CompletedOriginal();
                    case ProcessDecisionRefreshAccountingResult.Pending pending:
                        return new ProcessDecisionRefreshDeliveryResult.PendingChild(pending.Reason);
                    case ProcessDecisionRefreshAccountingResult.Reconciled:
                        break;
                    default:
                        throw new InvalidDataException("Refresh delivery received an unsupported accounting outcome.");
                }
            }
            return await CaptureAsync(state, context, identity, cancellationToken).ConfigureAwait(false);
        } catch (Exception failure) when (failure is ProcessRuntimeOptimisticConcurrencyException or
                ProcessDecisionException { Reason: ProcessDecisionRefusal.StaleExpectation } or
                ProcessDecisionRunScopeException { Reason: ProcessDecisionRunScopeRefusal.StaleExpectation }) {
            logger.LogInformation("Refresh delivery {AdmissionId} in run {RunId} must reconcile original receipts after {FailureKind}.",
                admissionId.Value, runId.Value, failure.GetType().Name);
            return new ProcessDecisionRefreshDeliveryResult.Contended();
        } catch (Exception failure) when (failure is ProcessDecisionSourceProjectionRequiredException or ProcessAcceptedExecutionInputRequiredException) {
            return new ProcessDecisionRefreshDeliveryResult.SourceProjectionRequired();
        } catch (ProcessExecutionAuthorityMismatchException) {
            return await StoppedAsync(await LoadAsync(runId, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false)
                ? new ProcessDecisionRefreshDeliveryResult.Contended() : new ProcessDecisionRefreshDeliveryResult.SourceProjectionRequired();
        } catch (InvalidOperationException) {
            if (await StoppedAsync(await LoadAsync(runId, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false)) {
                return new ProcessDecisionRefreshDeliveryResult.Contended();
            }
            throw;
        }
    }

    private async Task<ProcessDecisionRefreshDeliveryResult> CaptureAsync(ProcessRuntimeStateSnapshot state, RuntimeCommandContext context,
        ProcessDecisionRefreshCaptureIdentity identity, CancellationToken cancellationToken) {
        var result = await captures.PrepareAsync(state, context, identity.AdmissionId, cancellationToken).ConfigureAwait(false);
        if (result.Status != ProcessDecisionRefreshCaptureStatus.Pending) {
            return result.Status switch {
                ProcessDecisionRefreshCaptureStatus.Advanced => new ProcessDecisionRefreshDeliveryResult.Advanced(),
                ProcessDecisionRefreshCaptureStatus.Stopped => new ProcessDecisionRefreshDeliveryResult.Stopped(),
                ProcessDecisionRefreshCaptureStatus.ReviewRequired => new ProcessDecisionRefreshDeliveryResult.ReviewRequired(),
                ProcessDecisionRefreshCaptureStatus.SourceProjectionRequired => new ProcessDecisionRefreshDeliveryResult.SourceProjectionRequired(),
                _ => throw new InvalidDataException("Refresh delivery received an unsupported capture outcome.")
            };
        }
        var saved = await progress.ReadProgressAsync(state.RunId, identity.AdmissionId, cancellationToken).ConfigureAwait(false);
        ProcessDecisionRefreshCaptureReceipts.Require(saved, state.RootRunId, identity);
        if (PendingReservation(saved, identity) is not { } reservation) {
            return new ProcessDecisionRefreshDeliveryResult.PendingCapture(ProcessDecisionRefreshDeliveryCapturePending.Unsettled);
        }
        var capture = await journal.ReadAsync(state.RunId, reservation, cancellationToken).ConfigureAwait(false);
        return new ProcessDecisionRefreshDeliveryResult.PendingCapture(capture?.Phase switch {
            null => ProcessDecisionRefreshDeliveryCapturePending.LegacyUnknown,
            ProcessDecisionRefreshCapturePhase.Invoking => ProcessDecisionRefreshDeliveryCapturePending.UnknownInvocation,
            _ => ProcessDecisionRefreshDeliveryCapturePending.Unsettled
        });
    }

    private async Task<bool> StoppedAsync(ProcessRuntimeStateSnapshot state, CancellationToken cancellationToken)
        => state.Status == ProcessRuntimeStatus.CancelRequested || ProcessRuntimeTerminalStates.IsRunTerminal(state.Status) ||
            await roots.IsStoppedAsync(state.RootRunId, cancellationToken).ConfigureAwait(false);

    private async Task<ProcessRuntimeStateSnapshot> LoadAsync(ProcessRunId runId, CancellationToken cancellationToken)
        => await states.LoadAsync(runId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The original refresh runtime is missing.");

    private static RuntimeCommandId? PendingReservation(ProcessDecisionRefreshCaptureProgress saved, ProcessDecisionRefreshCaptureIdentity identity)
        => saved.LocalReservation is not null ? identity.LocalReserve
            : saved.RootReservation is not null && saved.RootCompletion is null ? identity.RootReserve : null;
}
