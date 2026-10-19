using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.UI.Catalog;

public enum AgentDecisionPolicyIntent { Save, ReadCurrent, UseCurrentValues, KeepDraft, Close }

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
    public bool Busy { get; private set; }
    public bool NeedsReview { get; private set; }
    public bool Retired { get; private set; }
    public string? Message { get; private set; }
    public bool CanSave => Original is not null && !Busy && !NeedsReview && !Retired && Unconfirmed is null;
    public bool CanAdopt => Current is not null && !Busy && !Retired && Unconfirmed is null;

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

    public ReplaceAgentDecisionDataPolicyCommand? BeginSave() {
        if (!CanSave) {
            return null;
        }
        var command = new ReplaceAgentDecisionDataPolicyCommand(Guid.NewGuid(), Original!.Expectation, Draft.Capture());
        Submitted = command;
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
                RequireIdentity(accepted.Snapshot);
                Original = accepted.Snapshot;
                NeedsReview = false;
                Message = accepted.FollowupRequired
                    ? "Policy saved. Some follow-up work failed; reload the catalog without repeating this save. Later draft edits are retained."
                    : "Policy saved for future work. Later draft edits are retained.";
                break;
            case AgentDecisionDataPolicySaveOutcome.Rejected rejected:
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
}
