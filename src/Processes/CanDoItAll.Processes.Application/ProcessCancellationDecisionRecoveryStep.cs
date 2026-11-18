using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessCancellationDecisionRecoveryStep(IProcessCancellationDecisionReader decisions,
    IProcessRootCancellationBarrierReader roots, IProcessRuntimeStateStore states, IProcessDecisionResultQuery results,
    IEnumerable<IProcessDecisionChildStopper> stoppers, ProcessDecisionTerminalAccountingCoordinator accounting,
    ProcessDecisionFinalizationCoordinator finalization, TimeProvider clock, ILogger<ProcessCancellationDecisionRecoveryStep> logger)
    : IProcessCancellationRecoveryStep {
    private readonly IReadOnlyDictionary<ProcessDecisionExecutionContract, IProcessDecisionChildStopper> owners = stoppers.ToDictionary(item => item.Contract);

    public async Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync(ProcessCancellationRecoveryClaim claim, CancellationToken cancellationToken) {
        claim.Validate();
        if (claim.Position.Stage != ProcessCancellationRecoveryStage.Decisions ||
                claim.Intent is not (null or ProcessCancellationAccountChild or ProcessCancellationFinalizeDecision) ||
                !await roots.IsStoppedAsync(claim.RootRunId, cancellationToken).ConfigureAwait(false)) {
            throw new InvalidDataException("Decision recovery requires its original position and durable root cancellation barrier.");
        }
        if (claim.Intent is { } intent) {
            return await ResolveAsync(claim, intent, cancellationToken).ConfigureAwait(false);
        }
        if (claim.Position.CurrentDecision is not { } reference) {
            var selected = await decisions.NextAsync(claim.RootRunId, claim.Position.DecisionAfter, cancellationToken).ConfigureAwait(false);
            selected?.Validate();
            if (selected is not null && claim.Position.DecisionAfter is { } after && selected.DecisionId.Value.CompareTo(after.Value) <= 0) {
                throw new InvalidDataException("Decision selection returned a repeated head.");
            }
            return new(selected is not null ? claim.Position with { CurrentDecision = selected }
                : new(ProcessCancellationRecoveryStage.SharedScope, CycleHadPending: claim.Position.CycleHadPending), Pending: RetainedPending(claim));
        }
        var metadata = await decisions.ReadAsync(claim.RootRunId, reference, cancellationToken).ConfigureAwait(false);
        if (metadata is null || metadata.Reference != reference || metadata.RunId.Value == Guid.Empty || metadata.StepInstanceId.Value == Guid.Empty) {
            return Advance(claim, NextDecision(claim.Position), ProcessCancellationRecoveryPending.InvalidEvidence);
        }
        if (metadata.HistoricalWorkPending) {
            claim = claim with { Position = claim.Position with { CycleHadPending = true }, Pending = ProcessCancellationRecoveryPending.InvalidEvidence };
        }
        var child = await decisions.NextChildAsync(claim.RootRunId, reference, claim.Position.ChildAfter, cancellationToken).ConfigureAwait(false);
        if (child?.Value == Guid.Empty || child is { } candidate && claim.Position.ChildAfter is { } previous && candidate.Value.CompareTo(previous.Value) <= 0) {
            throw new InvalidDataException("Decision selection returned an invalid or repeated child.");
        }
        var state = await LoadAsync(claim, metadata.RunId, cancellationToken).ConfigureAwait(false);
        var selectedResult = await results.ReadAsync(metadata.RunId, metadata.StepInstanceId,
            child is { } execution ? new(execution.Value) : null, cancellationToken).ConfigureAwait(false);
        selectedResult?.RequireOriginal(state, metadata.StepInstanceId, child is { } selectedChild ? new(selectedChild.Value) : null);
        if (selectedResult?.Head is not { } head || head.Reference != reference) {
            return Advance(claim, NextDecision(claim.Position), ProcessCancellationRecoveryPending.InvalidEvidence);
        }
        return child is { } childId ? await InspectChildAsync(claim, state, selectedResult, childId, cancellationToken).ConfigureAwait(false)
            : FinishDecision(claim, state, selectedResult);
    }

    private async Task<ProcessCancellationRecoveryCheckpoint> InspectChildAsync(ProcessCancellationRecoveryClaim claim,
        ProcessRuntimeStateSnapshot state, ProcessDecisionResultSelection selected, ProcessDecisionChildId childId, CancellationToken cancellationToken) {
        var next = claim.Position with { ChildAfter = childId };
        if (selected.Child is not { } child || child.Original.Child.Address.Reference != claim.Position.CurrentDecision) {
            return Advance(claim, next, ProcessCancellationRecoveryPending.InvalidEvidence);
        }
        if (child.Observation.Closure is not null) {
            return Advance(claim, next);
        }
        var step = state.Steps.SingleOrDefault(item => item.StepInstanceId == selected.StepInstanceId);
        if (step is null || !CanAccount(step.Status) || step.ActiveClaimToken is not null) {
            return Advance(claim, next, ProcessCancellationRecoveryPending.RuntimeWork);
        }
        if (!owners.TryGetValue(child.Original.ExecutionContract, out var owner)) {
            return Advance(claim, next, ProcessCancellationRecoveryPending.InvalidEvidence);
        }
        ProcessDecisionChildStopResult stopped;
        try {
            stopped = await owner.StopAsync(child.Original, cancellationToken).ConfigureAwait(false);
        } catch (IOException exception) {
            logger.LogWarning("Cancellation recovery could not observe child {ChildId} for run {RunId} and root {RootRunId}; retaining pending work ({FailureType}).",
                childId, state.RunId, state.RootRunId, exception.GetType().Name);
            return Advance(claim, next, ProcessCancellationRecoveryPending.UnresolvedEffects);
        }
        var pending = stopped switch {
            ProcessDecisionChildStopResult.ReadyForAccounting => ProcessCancellationRecoveryPending.None,
            ProcessDecisionChildStopResult.ActiveWork => ProcessCancellationRecoveryPending.ActiveWork,
            ProcessDecisionChildStopResult.LeaseBusy => ProcessCancellationRecoveryPending.LeaseBusy,
            ProcessDecisionChildStopResult.MissingJournal => ProcessCancellationRecoveryPending.MissingJournal,
            ProcessDecisionChildStopResult.UnresolvedEffects => ProcessCancellationRecoveryPending.UnresolvedEffects,
            ProcessDecisionChildStopResult.CompletionPending => ProcessCancellationRecoveryPending.CompletionPending,
            _ => throw new InvalidDataException("The original child stop owner returned an unknown result.")
        };
        if (pending != ProcessCancellationRecoveryPending.None) {
            return Advance(claim, next, pending);
        }
        var context = ProcessCancellationRecoveryIntent.CreateContext(claim.RootRunId, clock.GetUtcNow());
        return new(claim.Position, new ProcessCancellationAccountChild(state.RunId, context,
            new(child.Original.Child.Address, selected.Head!.Expectation, new(step.StepInstanceId, selected.Budget.BudgetId, selected.Budget.Revision),
                step.Status, context.CommandId)), RetainedPending(claim));
    }

    private ProcessCancellationRecoveryCheckpoint FinishDecision(ProcessCancellationRecoveryClaim claim,
        ProcessRuntimeStateSnapshot state, ProcessDecisionResultSelection selected) {
        var next = NextDecision(claim.Position);
        var head = selected.Head!;
        if (head.UnclosedChildCount != 0 || selected.OtherWorkPending) {
            return Advance(claim, next, ProcessCancellationRecoveryPending.DecisionWork);
        }
        if (selected.Budget.ReservedWorkTicks != 0 || selected.Budget.ReservedReplans != 0) {
            return Advance(claim, next, ProcessCancellationRecoveryPending.BudgetWork);
        }
        if (head.Phase is ProcessDecisionPhase.Completed or ProcessDecisionPhase.Cancelled) {
            return Advance(claim, next);
        }
        var step = state.Steps.SingleOrDefault(item => item.StepInstanceId == selected.StepInstanceId);
        if (head.Phase is not (ProcessDecisionPhase.Active or ProcessDecisionPhase.WaitingForReview or ProcessDecisionPhase.Reconciling) ||
                step is null || !CanAccount(step.Status) || step.ActiveClaimToken is not null) {
            return Advance(claim, next, ProcessCancellationRecoveryPending.InvalidEvidence);
        }
        var context = ProcessCancellationRecoveryIntent.CreateContext(claim.RootRunId, clock.GetUtcNow());
        return new(claim.Position, new ProcessCancellationFinalizeDecision(state.RunId, context,
            new(step.StepInstanceId, head.Expectation, new(step.StepInstanceId, selected.Budget.BudgetId, selected.Budget.Revision),
                step.Status, context.CommandId)), RetainedPending(claim));
    }

    private async Task<ProcessCancellationRecoveryCheckpoint> ResolveAsync(ProcessCancellationRecoveryClaim claim,
        ProcessCancellationRecoveryIntent intent, CancellationToken cancellationToken) {
        var reference = intent switch {
            ProcessCancellationAccountChild child => child.Request.Child.Reference,
            ProcessCancellationFinalizeDecision decision => decision.Request.Decision.Reference,
            _ => throw new InvalidDataException("Decision recovery has an unsupported retained intent.")
        };
        if (reference != claim.Position.CurrentDecision || intent is ProcessCancellationAccountChild selected &&
                claim.Position.ChildAfter is { } after && selected.Request.Child.ChildId.Value.CompareTo(after.Value) <= 0) {
            throw new InvalidDataException("The retained action differs from its original selected decision generation.");
        }
        var state = await LoadAsync(claim, intent.RunId, cancellationToken).ConfigureAwait(false);
        var next = intent is ProcessCancellationAccountChild account ? claim.Position with { ChildAfter = account.Request.Child.ChildId }
            : NextDecision(claim.Position);
        ProcessRuntimeCommitResult result;
        try {
            result = intent switch {
                ProcessCancellationAccountChild child => await accounting.CompleteAsync(state, intent.Context, child.Request, cancellationToken).ConfigureAwait(false),
                ProcessCancellationFinalizeDecision decision => await finalization.CompleteAsync(state, intent.Context, decision.Request, cancellationToken).ConfigureAwait(false),
                _ => throw new InvalidDataException("Decision recovery has an unsupported retained intent.")
            };
        } catch (ProcessRuntimeOptimisticConcurrencyException) {
            return Refused(claim, intent, next, ProcessCancellationRecoveryPending.ConflictingState);
        } catch (ProcessDecisionException exception) when (exception.Reason is ProcessDecisionRefusal.StaleExpectation or ProcessDecisionRefusal.UnclosedChildren) {
            return Refused(claim, intent, next, exception.Reason == ProcessDecisionRefusal.StaleExpectation
                ? ProcessCancellationRecoveryPending.ConflictingState : ProcessCancellationRecoveryPending.DecisionWork);
        }
        if (result.Succeeded) {
            if (result.State.RunId != intent.RunId || result.State.RootRunId != claim.RootRunId || result.Events.Count != 0 ||
                    result.OutboxMessages.Count != 0 || result.ArtifactLedgerEvents.Count != 0 || result.DecisionRunScope is not null) {
                throw new InvalidDataException("Decision recovery received a receipt for another run or with unexpected business effects.");
            }
            return Advance(claim, next) with { Resolution = new(intent.Context.CommandId, ProcessCancellationIntentOutcome.Committed) };
        }
        if (result.Outcome != ProcessRuntimeTransitionOutcome.Rejected) {
            throw new InvalidDataException("Decision recovery received an unsupported owner outcome.");
        }
        return Refused(claim, intent, next, ProcessCancellationRecoveryPending.DecisionWork);
    }

    private async Task<ProcessRuntimeStateSnapshot> LoadAsync(ProcessCancellationRecoveryClaim claim, ProcessRunId runId, CancellationToken cancellationToken) {
        var state = await states.LoadAsync(runId, cancellationToken).ConfigureAwait(false);
        return state is not null && state.RunId == runId && state.RootRunId == claim.RootRunId ? state
            : throw new InvalidDataException("Decision recovery lost its original selected run.");
    }

    private static bool CanAccount(ProcessRuntimeStepStatus status)
        => status is ProcessRuntimeStepStatus.Completed or ProcessRuntimeStepStatus.Failed or ProcessRuntimeStepStatus.Cancelled;

    private static ProcessCancellationRecoveryCheckpoint Refused(ProcessCancellationRecoveryClaim claim, ProcessCancellationRecoveryIntent intent,
        ProcessCancellationRecoveryPosition next, ProcessCancellationRecoveryPending pending)
        => Advance(claim, next, pending) with { Resolution = new(intent.Context.CommandId, ProcessCancellationIntentOutcome.RefusedWithoutCommit) };

    private static ProcessCancellationRecoveryPosition NextDecision(ProcessCancellationRecoveryPosition position)
        => position with { DecisionAfter = position.CurrentDecision!.DecisionId, CurrentDecision = null, ChildAfter = null };

    private static ProcessCancellationRecoveryPending RetainedPending(ProcessCancellationRecoveryClaim claim)
        => claim.Position.CycleHadPending ? claim.Pending : ProcessCancellationRecoveryPending.None;

    private static ProcessCancellationRecoveryCheckpoint Advance(ProcessCancellationRecoveryClaim claim, ProcessCancellationRecoveryPosition position,
        ProcessCancellationRecoveryPending pending = ProcessCancellationRecoveryPending.None)
        => new(position with { CycleHadPending = position.CycleHadPending || pending != ProcessCancellationRecoveryPending.None },
            Pending: pending == ProcessCancellationRecoveryPending.None ? RetainedPending(claim) : pending);
}
