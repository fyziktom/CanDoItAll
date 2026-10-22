using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessCancellationQuiescenceRecoveryStep(IProcessCancellationQuiescenceReader quiescence,
    IProcessRootCancellationBarrierReader roots, IProcessRuntimeStateStore states, IProcessIdempotencyStore receipts,
    IProcessRuntimeUnitOfWork unitOfWork, TimeProvider clock) : IProcessCancellationRecoveryStep {
    public async Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync(ProcessCancellationRecoveryClaim claim, CancellationToken cancellationToken) {
        claim.Validate();
        if (claim.Position.Stage != ProcessCancellationRecoveryStage.Quiescence || claim.Intent is not (null or ProcessCancellationFinalizeRoot) ||
                !await roots.IsStoppedAsync(claim.RootRunId, cancellationToken).ConfigureAwait(false)) {
            throw new InvalidDataException("Final recovery requires its original quiescence position and durable root cancellation barrier.");
        }
        if (claim.Intent is ProcessCancellationFinalizeRoot intent) {
            return await ResolveAsync(claim, intent, cancellationToken).ConfigureAwait(false);
        }
        if (claim.Position.CycleHadPending) {
            return Restart(claim.Pending == ProcessCancellationRecoveryPending.None ? ProcessCancellationRecoveryPending.ActiveWork : claim.Pending);
        }
        var pending = await quiescence.ReadAsync(claim.RootRunId, cancellationToken).ConfigureAwait(false);
        if (!Enum.IsDefined(pending)) {
            throw new InvalidDataException("Quiescence returned an unknown pending-work result.");
        }
        return pending != ProcessCancellationRecoveryPending.None ? Restart(pending)
            : new(claim.Position, new ProcessCancellationFinalizeRoot(claim.RootRunId,
                ProcessCancellationRecoveryIntent.CreateContext(claim.RootRunId, clock.GetUtcNow())));
    }

    private async Task<ProcessCancellationRecoveryCheckpoint> ResolveAsync(ProcessCancellationRecoveryClaim claim,
        ProcessCancellationFinalizeRoot intent, CancellationToken cancellationToken) {
        if (await receipts.FindCompletedCommandAsync(intent.Context.CommandId, cancellationToken).ConfigureAwait(false) is { } replay) {
            ProcessCancellationRuntimeReceipt.RequireCancellation(claim.RootRunId, intent, replay);
            return Resolved(claim, intent);
        }
        var state = await states.LoadAsync(claim.RootRunId, cancellationToken).ConfigureAwait(false);
        if (state is null || state.RunId != claim.RootRunId || state.RootRunId != claim.RootRunId ||
                state.Status is not (ProcessRuntimeStatus.CancelRequested or ProcessRuntimeStatus.Cancelled)) {
            throw new InvalidDataException("The retained finalization intent lost its original stopped root.");
        }
        var confirmation = state.Status == ProcessRuntimeStatus.Cancelled;
        ProcessRuntimeCommitResult result;
        try {
            if (confirmation) {
                var now = intent.Context.OccurredAtUtc.UtcTicks;
                var updated = Math.Max(now - now % 10, checked(state.UpdatedAtUtc.UtcTicks + 10));
                ProcessRuntimeMutation mutation = new(ProcessRuntimeTransitionOutcome.Applied,
                    state with { UpdatedAtUtc = new(updated, TimeSpan.Zero) }, [], [], [], []);
                result = await unitOfWork.CommitAsync(new(intent.Context.CommandId, state, mutation) { ConfirmRootCancellation = true }, cancellationToken).ConfigureAwait(false);
            } else {
                result = await new ProcessRuntimeEngine(unitOfWork).FinalizeRootCancellationAsync(state, intent.Context, cancellationToken).ConfigureAwait(false);
            }
        } catch (ProcessRuntimeOptimisticConcurrencyException) {
            return Restart(ProcessCancellationRecoveryPending.ConflictingState,
                new(intent.Context.CommandId, ProcessCancellationIntentOutcome.RefusedWithoutCommit));
        }
        if (result.Succeeded) {
            ProcessCancellationRuntimeReceipt.RequireCancellation(claim.RootRunId, intent, result, confirmation);
            return Resolved(claim, intent);
        }
        if (result.Outcome != ProcessRuntimeTransitionOutcome.Rejected) {
            throw new InvalidDataException("Root finalization returned an unsupported owner outcome.");
        }
        return Restart(ProcessCancellationRecoveryPending.ActiveWork,
            new(intent.Context.CommandId, ProcessCancellationIntentOutcome.RefusedWithoutCommit));
    }

    private static ProcessCancellationRecoveryCheckpoint Resolved(ProcessCancellationRecoveryClaim claim, ProcessCancellationFinalizeRoot intent) {
        ProcessCancellationIntentResolution resolution = new(intent.Context.CommandId, ProcessCancellationIntentOutcome.Committed);
        return claim.Position.CycleHadPending
            ? Restart(claim.Pending == ProcessCancellationRecoveryPending.None ? ProcessCancellationRecoveryPending.ActiveWork : claim.Pending, resolution)
            : new(new(ProcessCancellationRecoveryStage.Completed), Resolution: resolution);
    }

    private static ProcessCancellationRecoveryCheckpoint Restart(ProcessCancellationRecoveryPending pending, ProcessCancellationIntentResolution? resolution = null)
        => new(ProcessCancellationRecoveryPosition.Initial, Pending: pending, Resolution: resolution, RetryAfter: TimeSpan.FromSeconds(2));
}
