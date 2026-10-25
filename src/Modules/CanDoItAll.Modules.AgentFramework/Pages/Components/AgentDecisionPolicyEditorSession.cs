using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Catalog;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

internal sealed class AgentDecisionPolicyEditorSession(AgentDecisionPolicyEditorState state,
    IAgentFrameworkWorkspaceService workspace, IAgentDecisionPolicyPendingStore pending,
    ILogger<AgentDecisionPolicyEditorSession> logger, CancellationToken ownerLifetime) {
    public async Task ReadAsync(CancellationToken cancellationToken) {
        if (!state.BeginRead()) {
            return;
        }
        var initialize = !state.RecoveryReady;
        try {
            AgentDecisionPolicyPendingRecord? retained = null;
            if (initialize) {
                await pending.InitializeAsync(cancellationToken);
            }
            var snapshot = await workspace.ReadAgentDecisionDataPolicyAsync(state.AgentId, cancellationToken);
            if (initialize) {
                retained = await pending.ReadAsync(AgentDecisionPolicyPendingAddress.From(snapshot.Expectation), cancellationToken);
            }
            state.CompleteRead(snapshot);
            if (initialize) {
                state.CompleteRecovery(retained, restoreSubmittedValues: true);
            }
        } catch (Exception failure) when (failure is not OutOfMemoryException) {
            if (initialize) {
                state.RecoveryFailed(false);
            } else {
                state.ReadFailed();
            }
            Log(failure);
        }
    }

    public async Task<AgentDecisionDataPolicySaveOutcome?> SaveAsync() {
        ReplaceAgentDecisionDataPolicyCommand? command;
        try {
            command = state.BeginSave();
        } catch (Exception failure) when (failure is ArgumentException or JsonException) {
            state.ValidationFailed();
            return null;
        }
        if (command is null) {
            return null;
        }
        try {
            var prepared = await pending.PrepareAsync(command, ownerLifetime);
            if (!prepared.Changed) {
                state.CompleteRecovery(prepared.Record ?? throw new InvalidOperationException("The existing retained submission is missing."));
                return null;
            }
            state.RetainPending(prepared.Record!);
            var dispatch = await pending.BeginDispatchAsync(command, ownerLifetime);
            if (!dispatch.Changed) {
                state.CompleteRecovery(dispatch.Record ?? throw new InvalidOperationException("The original dispatch state is unavailable."));
                return null;
            }
            state.RetainPending(dispatch.Record!);
        } catch (Exception failure) when (failure is not OutOfMemoryException) {
            state.RecoveryFailed(false);
            Log(failure);
            return null;
        }
        AgentDecisionDataPolicySaveOutcome outcome;
        try {
            outcome = await workspace.ReplaceAgentDecisionDataPolicyAsync(command, ownerLifetime);
        } catch (Exception failure) when (failure is not OutOfMemoryException) {
            outcome = new AgentDecisionDataPolicySaveOutcome.Unconfirmed(command);
            Log(failure);
        }
        var recoveryFailed = false;
        if (outcome is AgentDecisionDataPolicySaveOutcome.Accepted or AgentDecisionDataPolicySaveOutcome.Rejected) {
            try {
                outcome = await FinishRetentionAsync(command, outcome, CancellationToken.None);
                state.ClearPending(command);
            } catch (Exception failure) when (failure is not OutOfMemoryException) {
                recoveryFailed = true;
                state.RetainPending(new(command, outcome is AgentDecisionDataPolicySaveOutcome.Accepted
                    ? AgentDecisionPolicyPendingStage.Accepted : AgentDecisionPolicyPendingStage.Rejected, outcome));
                Log(failure);
            }
        }
        state.CompleteSave(command, outcome);
        if (recoveryFailed) {
            state.RecoveryFailed(true);
        }
        if (outcome.Failure is { } diagnostic) {
            logger.LogWarning("Agent policy operation {OperationId} returned {OutcomeType}, stage {FailureStage}, diagnostic {FailureTypes}.",
                command.OperationId, outcome.GetType().Name, diagnostic.Stage, string.Join(", ", diagnostic.ExceptionTypes));
        }
        return outcome;
    }

    public async Task RecoverAsync(CancellationToken cancellationToken) {
        if (!state.RecoveryReady) {
            await ReadAsync(cancellationToken);
            return;
        }
        if (!state.BeginRecovery()) {
            return;
        }
        try {
            var earlier = state.Pending;
            var expectation = earlier?.Command.Expectation ?? state.Unconfirmed?.Expectation ?? state.Original?.Expectation
                ?? throw new InvalidOperationException("The original recovery address is unavailable.");
            var retained = await pending.ReadAsync(AgentDecisionPolicyPendingAddress.From(expectation), cancellationToken);
            var command = retained?.Command ?? earlier?.Command ?? state.Unconfirmed;
            var needsResolution = retained?.Stage != AgentDecisionPolicyPendingStage.Prepared && command is not null;
            state.CompleteRecovery(retained, keepBusy: needsResolution);
            if (!needsResolution) {
                return;
            }
            retained ??= earlier;
            var outcome = retained?.Outcome ?? (earlier?.Command == command ? earlier.Outcome : null);
            if (outcome is null) {
                var lookup = await workspace.LookupAgentDecisionDataPolicyAsync(command!, cancellationToken);
                if (lookup is not AgentDecisionDataPolicyLookup.Accepted accepted || accepted.Receipt.Command != command) {
                    state.RecoveryUnresolved(lookup is AgentDecisionDataPolicyLookup.Accepted ? new AgentDecisionDataPolicyLookup.Unavailable() : lookup);
                    return;
                }
                outcome = new AgentDecisionDataPolicySaveOutcome.Accepted(command!, accepted.Receipt.Snapshot);
            }
            outcome = await FinishRetentionAsync(command!, outcome, cancellationToken);
            state.ClearPending(command!);
            if (outcome is AgentDecisionDataPolicySaveOutcome.Accepted historical) {
                state.CompleteHistorical(command!, historical.Snapshot, historical.FollowupRequired);
            } else {
                state.CompleteSave(command!, outcome);
            }
        } catch (Exception failure) when (failure is not OutOfMemoryException) {
            state.RecoveryFailed(true);
            Log(failure);
        }
    }

    public async Task DiscardPreparedAsync(CancellationToken cancellationToken) {
        if (!state.CanDiscardPrepared || !state.BeginRecovery()) {
            return;
        }
        var command = state.Pending!.Command;
        try {
            var result = await pending.CancelPreparedAsync(command, cancellationToken);
            if (!result.Changed) {
                state.CompleteRecovery(result.Record ?? throw new InvalidOperationException("The unsent submission state is unavailable."));
                return;
            }
            state.ClearPending(command);
            state.CompletePreparedCancellation(command);
        } catch (Exception failure) when (failure is not OutOfMemoryException) {
            state.RecoveryFailed(false);
            Log(failure);
        }
    }

    private async Task<AgentDecisionDataPolicySaveOutcome> FinishRetentionAsync(ReplaceAgentDecisionDataPolicyCommand command, AgentDecisionDataPolicySaveOutcome outcome,
        CancellationToken cancellationToken) {
        var recorded = await pending.RecordOutcomeAsync(command, outcome, cancellationToken);
        if (recorded.Record is not null && (recorded.Record.Command != command || recorded.Record.Outcome is null)) {
            throw new InvalidOperationException("A different retained operation must be recovered separately.");
        }
        var cleared = await pending.ClearResolvedAsync(command, cancellationToken);
        if (cleared.Record is not null) {
            throw new InvalidOperationException("The resolved policy record could not be cleared without replacing another operation.");
        }
        return recorded.Record?.Outcome ?? outcome;
    }

    private void Log(Exception failure) => logger.LogWarning(
        "Agent policy editor for Agent {AgentId}, operation {OperationId}, failed with {FailureType}.",
        state.AgentId, state.Submitted?.OperationId, failure.GetType().FullName);
}
