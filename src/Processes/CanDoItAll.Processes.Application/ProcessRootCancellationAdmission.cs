using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public enum ProcessRootCancellationAdmissionStatus {
    Accepted, Completed, Unavailable, NotFound, NotRoot, Terminal, ReconciliationRequired, Contended, Rejected, TimedOut, Unconfirmed
}

public sealed record ProcessRootCancellationAvailability(bool Enabled);

public sealed record ProcessRootCancellationRequester(ProcessEventActor Actor, Func<CancellationToken, ValueTask> RequireCurrentAsync);

public sealed record ProcessRootCancellationAdmissionResult(ProcessRunId RunId, ProcessRootCancellationAdmissionStatus Status,
    RuntimeCommandId? BarrierCommandId = null, ProcessCancellationProgress? Progress = null);

public sealed class ProcessRootCancellationAdmissionService(IProcessRuntimeStateStore states, IProcessRuntimeUnitOfWork unitOfWork,
    IProcessCancellationProgressReader progress, ProcessRootCancellationAvailability availability, TimeProvider clock) {
    public static readonly TimeSpan MaximumDuration = TimeSpan.FromSeconds(5);
    private const int MaximumAttempts = 3;

    public async Task<ProcessRootCancellationAdmissionResult> AdmitAsync(ProcessRunId runId, ProcessRootCancellationRequester requester,
        CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfEqual(runId.Value, Guid.Empty);
        ArgumentNullException.ThrowIfNull(requester);
        ArgumentNullException.ThrowIfNull(requester.Actor);
        ArgumentNullException.ThrowIfNull(requester.RequireCurrentAsync);
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = new CancellationTokenSource(MaximumDuration, clock);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var submitted = false;
        try {
            await requester.RequireCurrentAsync(bounded.Token).ConfigureAwait(false);
            if (!availability.Enabled) {
                return new(runId, ProcessRootCancellationAdmissionStatus.Unavailable);
            }
            RuntimeCommandContext? original = null;
            var engine = new ProcessRuntimeEngine(unitOfWork);
            for (var attempt = 0; attempt < MaximumAttempts; attempt++) {
                var observed = await progress.ReadAsync(runId, bounded.Token).ConfigureAwait(false);
                if (observed is null) {
                    return new(runId, ProcessRootCancellationAdmissionStatus.NotFound);
                }
                if (observed.RunId != runId) {
                    throw new InvalidDataException("Cancellation observation returned a different selected identity.");
                }
                if (observed.RootRunId != runId) {
                    return new(runId, ProcessRootCancellationAdmissionStatus.NotRoot, Progress: observed);
                }
                var existing = ObserveExisting(observed);
                if (existing is not null) {
                    return existing;
                }
                var state = await states.LoadAsync(runId, bounded.Token).ConfigureAwait(false);
                if (state is null) {
                    return new(runId, ProcessRootCancellationAdmissionStatus.NotFound);
                }
                if (state.RunId != runId || state.RootRunId != runId) {
                    throw new InvalidDataException("Cancellation admission loaded a different root identity.");
                }
                if (state.Status is ProcessRuntimeStatus.CancelRequested or ProcessRuntimeStatus.Cancelled) {
                    continue;
                }
                if (ProcessRuntimeTerminalStates.IsRunTerminal(state.Status)) {
                    return new(runId, ProcessRootCancellationAdmissionStatus.Terminal, Progress: observed);
                }
                original ??= new(RuntimeCommandId.New(), requester.Actor, new($"process-cancellation:{runId.Value:D}"), clock.GetUtcNow());
                await requester.RequireCurrentAsync(bounded.Token).ConfigureAwait(false);
                try {
                    submitted = true;
                    var result = await engine.BeginRootCancellationAsync(state, original, bounded.Token).ConfigureAwait(false);
                    submitted = false;
                    if (result.Succeeded && result.State.RunId == runId && result.State.Status == ProcessRuntimeStatus.CancelRequested) {
                        return new(runId, ProcessRootCancellationAdmissionStatus.Accepted, original.CommandId);
                    }
                    return new(runId, ProcessRootCancellationAdmissionStatus.Rejected);
                } catch (ProcessRuntimeOptimisticConcurrencyException) {
                    submitted = false;
                }
            }
            return new(runId, ProcessRootCancellationAdmissionStatus.Contended);
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested) {
            return new(runId, submitted ? ProcessRootCancellationAdmissionStatus.Unconfirmed : ProcessRootCancellationAdmissionStatus.TimedOut);
        }
    }

    private static ProcessRootCancellationAdmissionResult? ObserveExisting(ProcessCancellationProgress observed) {
        var status = observed.Tracking switch {
            ProcessCancellationTracking.Pending => ProcessRootCancellationAdmissionStatus.Accepted,
            ProcessCancellationTracking.Completed => ProcessRootCancellationAdmissionStatus.Completed,
            ProcessCancellationTracking.LegacyUntracked or ProcessCancellationTracking.ReconciliationRequired => ProcessRootCancellationAdmissionStatus.ReconciliationRequired,
            ProcessCancellationTracking.NotRequested when ProcessRuntimeTerminalStates.IsRunTerminal(observed.RuntimeStatus) => ProcessRootCancellationAdmissionStatus.Terminal,
            ProcessCancellationTracking.NotRequested => (ProcessRootCancellationAdmissionStatus?)null,
            _ => throw new InvalidDataException("A root has an unsupported cancellation tracking state.")
        };
        return status is { } value ? new(observed.RunId, value, observed.Recovery?.BarrierCommandId, observed) : null;
    }
}
