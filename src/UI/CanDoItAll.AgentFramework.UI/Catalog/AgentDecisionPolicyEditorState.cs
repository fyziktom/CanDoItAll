using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.UI.Catalog;

public enum AgentDecisionPolicyIntent { Save, ReadCurrent, UseCurrentValues, KeepDraft, Close, Recover, DiscardPrepared }

public sealed class AgentDecisionPolicyEditorState {
    public AgentDecisionPolicyEditorState(Guid agentId) {
        if (agentId == Guid.Empty) {
            throw new ArgumentException("An Agent identity is required.", nameof(agentId));
        }
        AgentId = agentId;
    }

    public Guid AgentId { get; }
    public event Action? Changed;
    public AgentDecisionPolicyDraft Draft { get; } = new();
    public AgentDecisionDataPolicySnapshot? Original { get; private set; }
    public AgentDecisionDataPolicySnapshot? Current { get; private set; }
    public ReplaceAgentDecisionDataPolicyCommand? Submitted { get; private set; }
    public ReplaceAgentDecisionDataPolicyCommand? Unconfirmed { get; private set; }
    public AgentDecisionPolicyPendingRecord? Pending { get; private set; }
    public AgentDecisionDataPolicySnapshot? Historical { get; private set; }
    public bool RecoveryReady { get; private set; }
    public bool RecoveryBlocked { get; private set; }
    public bool Busy { get; private set; }
    public bool NeedsReview { get; private set; }
    public bool Retired { get; private set; }
    public string? Message { get; private set; }
    public bool CanSave => Original is not null && RecoveryReady && !RecoveryBlocked && Pending is null && !Busy && !NeedsReview && !Retired && Unconfirmed is null;
    public bool CanAdopt => Current is not null && RecoveryReady && !RecoveryBlocked && Pending is null && !Busy && !Retired && Unconfirmed is null;
    public bool CanRecover => !Busy && !Retired && (!RecoveryReady || RecoveryBlocked || Pending is not null || Unconfirmed is not null);
    public bool CanDiscardPrepared => !Busy && !Retired && Pending?.Stage == AgentDecisionPolicyPendingStage.Prepared;

    public bool BeginRead() {
        if (Busy || Retired) {
            return false;
        }
        Busy = true;
        NeedsReview = true;
        Current = null;
        Message = "Reading current policy…";
        Changed?.Invoke();
        return true;
    }

    public void CompleteRead(AgentDecisionDataPolicySnapshot snapshot) {
        RequireIdentity(snapshot);
        if (Retired) {
            return;
        }
        Busy = false;
        if (Original is null && Submitted is null) {
            Original = snapshot;
            Draft.Load(snapshot.Policy);
            NeedsReview = false;
            Message = null;
        } else {
            Current = snapshot;
            Message = Unconfirmed is null
                ? "Current policy is ready for comparison. Choose which values to keep before saving again."
                : "The current policy is shown for comparison. It does not confirm the earlier save; another save remains blocked in this editor.";
        }
        Changed?.Invoke();
    }

    public void ReadFailed() {
        if (Retired) {
            return;
        }
        Busy = false;
        NeedsReview = true;
        Current = null;
        Message = "The policy could not be read. Your draft and its original version are preserved.";
        Changed?.Invoke();
    }

    public void CompleteRecovery(AgentDecisionPolicyPendingRecord? pending, bool restoreSubmittedValues = false, bool keepBusy = false) {
        if (Retired) {
            return;
        }
        if (pending is not null) {
            RequirePendingIdentity(pending.Command);
            Submitted = pending.Command;
            Unconfirmed = pending.Stage == AgentDecisionPolicyPendingStage.Dispatching ? pending.Command : null;
            if (restoreSubmittedValues) {
                Draft.Load(pending.Command.Policy);
            }
            NeedsReview = true;
            Current = null;
            Message = pending.Stage switch {
                AgentDecisionPolicyPendingStage.Prepared => "This submission was retained but has not been sent. Discard it explicitly before preparing another save.",
                AgentDecisionPolicyPendingStage.Dispatching => "An earlier save remains unconfirmed. Look up its original receipt without resending it.",
                _ => "A known result is retained. Finish recovery before comparing current policy."
            };
        }
        Pending = pending;
        RecoveryReady = true;
        RecoveryBlocked = false;
        Busy = keepBusy;
        Changed?.Invoke();
    }

    public void RetainPending(AgentDecisionPolicyPendingRecord pending) {
        RequirePendingIdentity(pending.Command);
        if (Submitted != pending.Command) {
            throw new InvalidOperationException("The retained state belongs to another policy submission.");
        }
        Pending = pending;
        Changed?.Invoke();
    }

    public void ClearPending(ReplaceAgentDecisionDataPolicyCommand command) {
        if (Pending is not null && Pending.Command != command) {
            throw new InvalidOperationException("Another retained policy operation cannot be cleared.");
        }
        Pending = null;
        RecoveryBlocked = false;
        Changed?.Invoke();
    }

    public bool BeginRecovery() {
        if (!CanRecover) {
            return false;
        }
        Busy = true;
        Message = "Checking the retained save…";
        Changed?.Invoke();
        return true;
    }

    public void RecoveryFailed(bool ownerWasCalled) {
        Busy = false;
        RecoveryBlocked = true;
        NeedsReview = true;
        Message = ownerWasCalled
            ? "The original result and draft are retained, but recovery could not finish. Check the retained save; do not resend it."
            : "No policy write was sent. Recovery storage could not confirm its state; check the retained save before trying again.";
        Changed?.Invoke();
    }

    public void RecoveryUnresolved(AgentDecisionDataPolicyLookup lookup) {
        Busy = false;
        NeedsReview = true;
        Message = lookup switch {
            AgentDecisionDataPolicyLookup.NotObserved => "No retained receipt was observed. The save remains unresolved; this does not prove that it had no effect.",
            AgentDecisionDataPolicyLookup.Conflict => "The receipt does not match the retained submission. The original intent remains preserved for investigation.",
            AgentDecisionDataPolicyLookup.ContextChanged => "The original workspace is not active. Return to it before looking up this save.",
            _ => "The receipt could not be read. The original submission remains retained and will not be resent."
        };
        Changed?.Invoke();
    }

    public void CompleteHistorical(ReplaceAgentDecisionDataPolicyCommand command, AgentDecisionDataPolicySnapshot snapshot, bool followupRequired = false) {
        RequirePendingIdentity(command);
        _ = new AgentDecisionDataPolicySaveOutcome.Accepted(command, snapshot);
        if (Submitted != command || Pending is not null) {
            throw new InvalidOperationException("Historical recovery must finish the original retained operation first.");
        }
        Historical = snapshot;
        Unconfirmed = null;
        Busy = false;
        RecoveryBlocked = false;
        NeedsReview = true;
        Current = null;
        Message = followupRequired
            ? "The original save was accepted with a follow-up failure. Read current policy and reload the catalog without repeating that save; your draft is retained."
            : "The original save was accepted. This is its historical result. Read current policy and compare before saving again; your draft is retained.";
        Changed?.Invoke();
    }

    public void CompletePreparedCancellation(ReplaceAgentDecisionDataPolicyCommand command) {
        if (Submitted != command || Pending is not null) {
            throw new InvalidOperationException("Only the original unsent submission can be discarded.");
        }
        Unconfirmed = null;
        Busy = false;
        RecoveryBlocked = false;
        NeedsReview = true;
        Current = null;
        Message = "The unsent submission was discarded. Your draft remains. Read current policy before preparing a new save.";
        Changed?.Invoke();
    }

    public ReplaceAgentDecisionDataPolicyCommand? BeginSave() {
        if (!CanSave) {
            return null;
        }
        var command = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), Original!.Expectation, Draft.Capture());
        Submitted = command;
        Historical = null;
        Busy = true;
        Current = null;
        Message = "Saving policy…";
        Changed?.Invoke();
        return command;
    }

    public void CompleteSave(ReplaceAgentDecisionDataPolicyCommand command, AgentDecisionDataPolicySaveOutcome outcome) {
        if (command != Submitted) {
            throw new InvalidOperationException("The outcome belongs to another policy submission.");
        }
        Busy = false;
        switch (outcome) {
            case AgentDecisionDataPolicySaveOutcome.Accepted accepted when accepted.Command == command:
                Unconfirmed = null;
                RequireIdentity(accepted.Snapshot);
                Original = accepted.Snapshot;
                NeedsReview = false;
                Message = accepted.FollowupRequired
                    ? "Policy saved. Some follow-up work failed; reload the catalog without repeating this save. Later draft edits are retained."
                    : "Policy saved for future work. Later draft edits are retained.";
                break;
            case AgentDecisionDataPolicySaveOutcome.Rejected rejected:
                Unconfirmed = null;
                NeedsReview = true;
                Message = $"Policy was not saved ({rejected.Reason}). Read current policy to compare; your draft is retained.";
                break;
            case AgentDecisionDataPolicySaveOutcome.Unconfirmed unconfirmed when unconfirmed.Command == command:
                Unconfirmed = command;
                NeedsReview = true;
                Message = "The save could not be confirmed. Keep the submitted values and operation identity; do not repeat this save.";
                break;
            default:
                throw new InvalidOperationException("The policy outcome is not bound to the original submission.");
        }
        Changed?.Invoke();
    }

    public void AdoptCurrent(bool keepDraft) {
        if (!CanAdopt) {
            return;
        }
        Original = Current;
        Current = null;
        if (!keepDraft) {
            Draft.Load(Original!.Policy);
        }
        NeedsReview = false;
        Message = keepDraft ? "Your draft is retained against the explicitly selected current version. Review it before saving."
            : "Current policy values loaded. No change was saved.";
        Changed?.Invoke();
    }

    public void ValidationFailed() {
        Message = "Choose supported options, a nonnegative replan count and positive durations, or leave fields blank to inherit.";
        Changed?.Invoke();
    }
    public void CatalogRefreshFailed() {
        Message = "Policy saved, but the catalog could not refresh. Do not repeat the save; your draft is retained.";
        Changed?.Invoke();
    }
    public void Retire() {
        Retired = true;
        Changed?.Invoke();
    }

    private void RequireIdentity(AgentDecisionDataPolicySnapshot snapshot) {
        if (snapshot.Expectation.AgentId != AgentId || Original is { } original && snapshot.Expectation.Workspace != original.Expectation.Workspace) {
            throw new InvalidOperationException("The policy observation belongs to another Agent or workspace.");
        }
    }

    private void RequirePendingIdentity(ReplaceAgentDecisionDataPolicyCommand command) {
        var workspace = command.Expectation.Workspace;
        if (command.Expectation.AgentId != AgentId || Original is { } original &&
            (workspace.DatabaseProfileId != original.Expectation.Workspace.DatabaseProfileId || workspace.WorkspaceScope != original.Expectation.Workspace.WorkspaceScope)) {
            throw new InvalidOperationException("The retained policy belongs to another Agent or workspace.");
        }
    }
}
