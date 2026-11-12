using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessCancellationScopeRecoveryStep(IProcessDecisionRootQuery scopes, IProcessRootCancellationBarrierReader roots,
    IProcessRuntimeStateStore states, IProcessIdempotencyStore receipts, IProcessRuntimeUnitOfWork unitOfWork, TimeProvider clock)
    : IProcessCancellationRecoveryStep {
    public async Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync(ProcessCancellationRecoveryClaim claim, CancellationToken cancellationToken) {
        claim.Validate();
        if (claim.Position.Stage != ProcessCancellationRecoveryStage.SharedScope || claim.Intent is not (null or ProcessCancellationChangeScope) ||
                !await roots.IsStoppedAsync(claim.RootRunId, cancellationToken).ConfigureAwait(false)) {
            throw new InvalidDataException("Shared-scope recovery requires its original position and durable root cancellation barrier.");
        }
        if (claim.Intent is ProcessCancellationChangeScope intent) {
            return await ResolveAsync(claim, intent, cancellationToken).ConfigureAwait(false);
        }
        var state = await LoadRootAsync(claim, cancellationToken).ConfigureAwait(false);
        ProcessDecisionRootSelection? selected;
        try {
            selected = await scopes.ReadAsync(state, cancellationToken).ConfigureAwait(false);
        } catch (ProcessDecisionSourceProjectionRequiredException) {
            return Advance(claim, ProcessCancellationRecoveryPending.InvalidEvidence);
        }
        if (selected is null) {
            return Advance(claim);
        }
        selected.Scope.Validate();
        selected.Root.RequireRoot(state, selected.Root.DefinitionVersionId);
        if (selected.Root.MaterialScope != selected.Scope.Admission || selected.Budget.BudgetId != selected.Root.BudgetId ||
                selected.RootStatus is not (ProcessRuntimeStatus.CancelRequested or ProcessRuntimeStatus.Cancelled)) {
            throw new InvalidDataException("Shared-scope recovery lost its original root, budget or material admission.");
        }
        var kind = selected.Scope.Phase switch {
            ProcessDecisionRunScopePhase.Active => ProcessDecisionRunScopeCommandKind.BeginReconciliation,
            ProcessDecisionRunScopePhase.AwaitingCapture or ProcessDecisionRunScopePhase.Reconciling => ProcessDecisionRunScopeCommandKind.Cancel,
            _ => (ProcessDecisionRunScopeCommandKind?)null
        };
        return kind is { } command ? new(claim.Position, new ProcessCancellationChangeScope(claim.RootRunId,
            ProcessCancellationRecoveryIntent.CreateContext(claim.RootRunId, clock.GetUtcNow()), new(selected.Scope.Expectation, command)), RetainedPending(claim))
            : Advance(claim, selected.Scope.Phase == ProcessDecisionRunScopePhase.Capturing
                ? ProcessCancellationRecoveryPending.CapturePending : ProcessCancellationRecoveryPending.None);
    }

    private async Task<ProcessCancellationRecoveryCheckpoint> ResolveAsync(ProcessCancellationRecoveryClaim claim,
        ProcessCancellationChangeScope intent, CancellationToken cancellationToken) {
        if (await receipts.FindCompletedCommandAsync(intent.Context.CommandId, cancellationToken).ConfigureAwait(false) is { } replay) {
            RequireReceipt(intent, replay);
            return Resolved(claim, intent);
        }
        var state = await LoadRootAsync(claim, cancellationToken).ConfigureAwait(false);
        var now = intent.Context.OccurredAtUtc.UtcTicks;
        var updated = Math.Max(now - now % 10, checked(state.UpdatedAtUtc.UtcTicks + 10));
        ProcessRuntimeMutation mutation = new(ProcessRuntimeTransitionOutcome.Applied,
            state with { UpdatedAtUtc = new(updated, TimeSpan.Zero) }, [], [], [], []);
        ProcessRuntimeCommitResult result;
        try {
            result = await unitOfWork.CommitAsync(new(intent.Context.CommandId, state, mutation) { DecisionRunScope = intent.Command }, cancellationToken).ConfigureAwait(false);
        } catch (ProcessRuntimeOptimisticConcurrencyException) {
            return Refused(claim, intent, ProcessCancellationRecoveryPending.ConflictingState);
        } catch (ProcessDecisionRunScopeException exception) when (exception.Reason is ProcessDecisionRunScopeRefusal.StaleExpectation or
                ProcessDecisionRunScopeRefusal.UnclosedWork) {
            return Refused(claim, intent, exception.Reason == ProcessDecisionRunScopeRefusal.StaleExpectation
                ? ProcessCancellationRecoveryPending.ConflictingState : ProcessCancellationRecoveryPending.ScopeWork);
        }
        if (result.Succeeded) {
            RequireReceipt(intent, result);
            return Resolved(claim, intent);
        }
        if (result.Outcome != ProcessRuntimeTransitionOutcome.Rejected) {
            throw new InvalidDataException("Shared-scope recovery received an unsupported owner outcome.");
        }
        return Refused(claim, intent, ProcessCancellationRecoveryPending.ScopeWork);
    }

    private async Task<ProcessRuntimeStateSnapshot> LoadRootAsync(ProcessCancellationRecoveryClaim claim, CancellationToken cancellationToken) {
        var state = await states.LoadAsync(claim.RootRunId, cancellationToken).ConfigureAwait(false);
        return state is not null && state.RunId == claim.RootRunId && state.RootRunId == claim.RootRunId &&
            state.Status is ProcessRuntimeStatus.CancelRequested or ProcessRuntimeStatus.Cancelled ? state
            : throw new InvalidDataException("Shared-scope recovery lost its original stopped root.");
    }

    private static void RequireReceipt(ProcessCancellationChangeScope intent, ProcessRuntimeCommitResult result) {
        if (!result.Succeeded || result.State.RunId != intent.RunId || result.State.RootRunId != intent.RunId ||
                result.State.Status is not (ProcessRuntimeStatus.CancelRequested or ProcessRuntimeStatus.Cancelled) ||
                result.Events.Count != 0 || result.OutboxMessages.Count != 0 || result.ArtifactLedgerEvents.Count != 0 || result.Diagnostics.Count != 0 ||
                result.Decision is not null || result.DecisionBudget is not null || result.DecisionRunScope is not { } scope ||
                scope.Command != intent.Command || scope.State.Admission.Funding.RootRunId != intent.RunId ||
                scope.State.Admission.ScopeId != intent.Command.Expectation.ScopeId || scope.State.Revision != checked(intent.Command.Expectation.Revision + 1) ||
                scope.State.Phase != (intent.Command.Kind == ProcessDecisionRunScopeCommandKind.Cancel
                    ? ProcessDecisionRunScopePhase.Cancelled : ProcessDecisionRunScopePhase.Reconciling)) {
            throw new InvalidDataException("The shared-scope receipt differs from its original root, command or expected transition.");
        }
        scope.State.Validate();
    }

    private static ProcessCancellationRecoveryCheckpoint Resolved(ProcessCancellationRecoveryClaim claim, ProcessCancellationChangeScope intent) {
        ProcessCancellationIntentResolution resolution = new(intent.Context.CommandId, ProcessCancellationIntentOutcome.Committed);
        return intent.Command.Kind == ProcessDecisionRunScopeCommandKind.Cancel ? Advance(claim) with { Resolution = resolution }
            : new(claim.Position, Pending: RetainedPending(claim), Resolution: resolution);
    }

    private static ProcessCancellationRecoveryCheckpoint Refused(ProcessCancellationRecoveryClaim claim, ProcessCancellationChangeScope intent,
        ProcessCancellationRecoveryPending pending) => Advance(claim, pending) with {
            Resolution = new(intent.Context.CommandId, ProcessCancellationIntentOutcome.RefusedWithoutCommit)
        };

    private static ProcessCancellationRecoveryPending RetainedPending(ProcessCancellationRecoveryClaim claim)
        => claim.Position.CycleHadPending ? claim.Pending : ProcessCancellationRecoveryPending.None;

    private static ProcessCancellationRecoveryCheckpoint Advance(ProcessCancellationRecoveryClaim claim,
        ProcessCancellationRecoveryPending pending = ProcessCancellationRecoveryPending.None)
        => new(new(ProcessCancellationRecoveryStage.Quiescence, CycleHadPending: claim.Position.CycleHadPending || pending != ProcessCancellationRecoveryPending.None),
            Pending: pending == ProcessCancellationRecoveryPending.None ? RetainedPending(claim) : pending);
}
