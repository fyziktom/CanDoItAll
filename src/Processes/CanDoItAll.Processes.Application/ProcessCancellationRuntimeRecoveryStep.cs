using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessCancellationRuntimeRecoveryStep(IProcessCancellationRuntimeReader runs,
    IProcessRootCancellationBarrierReader roots, IProcessExecutionRecovery executions, IProcessRuntimeStateStore states,
    IProcessIdempotencyStore receipts, IProcessRuntimeUnitOfWork unitOfWork, TimeProvider clock) : IProcessCancellationRecoveryStep {
    public async Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync(ProcessCancellationRecoveryClaim claim, CancellationToken cancellationToken) {
        claim.Validate();
        if (claim.Position.Stage != ProcessCancellationRecoveryStage.Runtime ||
                claim.Intent is not (null or ProcessCancellationCancelRun)) {
            throw new InvalidDataException("Runtime recovery requires its original runtime position and cancellation intent.");
        }
        if (!await roots.IsStoppedAsync(claim.RootRunId, cancellationToken).ConfigureAwait(false)) {
            throw new InvalidDataException("Runtime recovery requires its durable original root cancellation barrier.");
        }
        if (claim.Intent is ProcessCancellationCancelRun intent) {
            return await ResolveAsync(claim, intent, cancellationToken).ConfigureAwait(false);
        }
        if (claim.Position.CurrentRun is not { } runId) {
            var selected = await runs.NextAsync(claim.RootRunId, claim.Position.RunAfter, cancellationToken).ConfigureAwait(false);
            if (selected?.Value == Guid.Empty || selected is { } selectedRun && claim.Position.RunAfter is { } afterRun &&
                    selectedRun.Value.CompareTo(afterRun.Value) <= 0) {
                throw new InvalidDataException("Runtime selection returned an invalid or repeated run.");
            }
            return new(selected is { } next ? claim.Position with { CurrentRun = next }
                : new(ProcessCancellationRecoveryStage.Decisions, CycleHadPending: claim.Position.CycleHadPending), Pending: RetainedPending(claim));
        }
        var run = await runs.ReadAsync(claim.RootRunId, runId, cancellationToken).ConfigureAwait(false);
        if (run is null || run.RunId != runId || !Enum.IsDefined(run.Status)) {
            throw new InvalidDataException("The selected runtime run no longer belongs to its original root.");
        }
        if (runId != claim.RootRunId && !ProcessRuntimeTerminalStates.IsRunTerminal(run.Status)) {
            return new(claim.Position, new ProcessCancellationCancelRun(runId,
                ProcessCancellationRecoveryIntent.CreateContext(claim.RootRunId, clock.GetUtcNow())), RetainedPending(claim));
        }
        var progress = await executions.InspectNextAsync(claim.RootRunId, runId, claim.Position.ExecutionAfter, cancellationToken).ConfigureAwait(false);
        if (!Enum.IsDefined(progress.Pending) || progress.After?.Value == Guid.Empty ||
                progress.After is { } after && claim.Position.ExecutionAfter is { } previous && after.Value.CompareTo(previous.Value) < 0 ||
                progress.After is null && claim.Position.ExecutionAfter is not null ||
                !progress.Exhausted && progress.After == claim.Position.ExecutionAfter && progress.Pending == ProcessCancellationRecoveryPending.None) {
            throw new InvalidDataException("Execution recovery returned invalid or non-progressing selection evidence.");
        }
        var position = progress.Exhausted || progress.After == claim.Position.ExecutionAfter
            ? NextRun(claim.Position) : claim.Position with { ExecutionAfter = progress.After };
        var pending = progress.Pending == ProcessCancellationRecoveryPending.None ? RetainedPending(claim) : progress.Pending;
        return new(position with { CycleHadPending = position.CycleHadPending || pending != ProcessCancellationRecoveryPending.None }, Pending: pending);
    }

    private async Task<ProcessCancellationRecoveryCheckpoint> ResolveAsync(ProcessCancellationRecoveryClaim claim,
        ProcessCancellationCancelRun intent, CancellationToken cancellationToken) {
        if (intent.RunId == claim.RootRunId || intent.RunId != claim.Position.CurrentRun) {
            throw new InvalidDataException("Runtime cancellation intent differs from the selected original descendant.");
        }
        if (await receipts.FindCompletedCommandAsync(intent.Context.CommandId, cancellationToken).ConfigureAwait(false) is { } replay) {
            RequireReceipt(claim.RootRunId, intent, replay);
            return Resolved(claim, intent, ProcessCancellationIntentOutcome.Committed);
        }
        var state = await states.LoadAsync(intent.RunId, cancellationToken).ConfigureAwait(false);
        if (state is null || state.RunId != intent.RunId || state.RootRunId != claim.RootRunId) {
            throw new InvalidDataException("The retained cancellation intent lost its original runtime state.");
        }
        if (ProcessRuntimeTerminalStates.IsRunTerminal(state.Status)) {
            return Resolved(claim, intent, ProcessCancellationIntentOutcome.RefusedWithoutCommit);
        }
        ProcessRuntimeCommitResult result;
        try {
            result = await new ProcessRuntimeEngine(unitOfWork).RequestCancellationAsync(state, intent.Context, cancellationToken).ConfigureAwait(false);
        } catch (ProcessRuntimeOptimisticConcurrencyException) {
            return Resolved(claim, intent, ProcessCancellationIntentOutcome.RefusedWithoutCommit, ProcessCancellationRecoveryPending.ConflictingState);
        }
        if (result.Succeeded) {
            RequireReceipt(claim.RootRunId, intent, result);
            return Resolved(claim, intent, ProcessCancellationIntentOutcome.Committed);
        }
        if (result.Outcome != ProcessRuntimeTransitionOutcome.Rejected) {
            throw new InvalidDataException("Runtime cancellation returned an unsupported owner outcome.");
        }
        return Resolved(claim, intent, ProcessCancellationIntentOutcome.RefusedWithoutCommit, ProcessCancellationRecoveryPending.RuntimeWork);
    }

    private static ProcessCancellationRecoveryCheckpoint Resolved(ProcessCancellationRecoveryClaim claim, ProcessCancellationCancelRun intent,
        ProcessCancellationIntentOutcome outcome, ProcessCancellationRecoveryPending pending = ProcessCancellationRecoveryPending.None)
        => new(pending == ProcessCancellationRecoveryPending.None ? claim.Position : NextRun(claim.Position) with { CycleHadPending = true },
            Pending: pending == ProcessCancellationRecoveryPending.None ? RetainedPending(claim) : pending,
            Resolution: new(intent.Context.CommandId, outcome));

    private static ProcessCancellationRecoveryPosition NextRun(ProcessCancellationRecoveryPosition position)
        => position with { RunAfter = position.CurrentRun, CurrentRun = null, ExecutionAfter = null };

    private static ProcessCancellationRecoveryPending RetainedPending(ProcessCancellationRecoveryClaim claim)
        => claim.Position.CycleHadPending ? claim.Pending : ProcessCancellationRecoveryPending.None;

    private static void RequireReceipt(ProcessRunId rootRunId, ProcessCancellationCancelRun intent, ProcessRuntimeCommitResult result) {
        if (!result.Succeeded || result.State.RunId != intent.RunId || result.State.RootRunId != rootRunId ||
                result.State.Status != ProcessRuntimeStatus.Cancelled || result.Decision is not null || result.DecisionBudget is not null ||
                result.DecisionRunScope is not null || result.ArtifactLedgerEvents.Count != 0) {
            throw new InvalidDataException("The cancellation receipt differs from the original selected descendant.");
        }
        if (result.Events.Count == 0 && !result.CommittedNow && result.OutboxMessages.Count == 0) {
            return;
        }
        if (result.Events.Count != 1 || result.Events[0] is not { } applied || applied.EventType != ProcessRuntimeEventTypes.ProcessRunCancelled ||
                applied.RootRunId != rootRunId || applied.RunId != intent.RunId || applied.Actor != intent.Context.Actor ||
                applied.CorrelationId != intent.Context.CorrelationId || applied.OccurredAtUtc != intent.Context.OccurredAtUtc ||
                result.OutboxMessages.Count != 1 || result.OutboxMessages[0] is not { } notification ||
                notification.EventId != applied.EventId || notification.PayloadHash != applied.PayloadHash ||
                notification.SubscriberKind != ProcessOutboxSubscriberKind.RuntimeProjection) {
            throw new InvalidDataException("The cancellation receipt differs from the original selected descendant and command context.");
        }
    }
}
