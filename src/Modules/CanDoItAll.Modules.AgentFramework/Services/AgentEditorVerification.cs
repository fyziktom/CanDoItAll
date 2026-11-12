using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentEditorVerification(Guid agentId, Guid capabilityId, DateTimeOffset? expectedVersion,
    CapabilityVerificationOutcome outcome) {
    public CapabilityVerificationDisposition Disposition => outcome.Disposition;
    public bool NeedsReview { get; private set; } = outcome.Disposition is
        CapabilityVerificationDisposition.Committed or CapabilityVerificationDisposition.Unconfirmed;
    public bool IsConflict { get; private set; }
    public bool BlocksWrites => NeedsReview || IsConflict;
    public string Message { get; private set; } = outcome.Disposition switch {
        CapabilityVerificationDisposition.Rejected => "Verification was rejected. Check the saved attachment and provider. Your draft is retained.",
        CapabilityVerificationDisposition.CanceledBeforeDiagnostic => "Verification was cancelled before the diagnostic started.",
        CapabilityVerificationDisposition.DiagnosticInterrupted => "The diagnostic was interrupted. No proof was published.",
        CapabilityVerificationDisposition.Superseded => "The diagnostic inputs changed. No stale proof was published; your draft is retained.",
        CapabilityVerificationDisposition.PublicationCanceled => "Proof publication was cancelled before writing. Your draft is retained.",
        CapabilityVerificationDisposition.PublicationNotStarted => "Proof publication did not start because the catalog was unavailable.",
        CapabilityVerificationDisposition.Committed => "Proof was published. Read back its exact receipt without running the diagnostic again.",
        CapabilityVerificationDisposition.InfrastructureUnavailable => "Verification did not start because its current data is unavailable.",
        _ => "Proof publication is unconfirmed. Review its exact receipt before another write or diagnostic."
    };

    public bool Reconcile(AgentEditorModel draft, AgentEditorCatalogRefresh refreshed) {
        if (!NeedsReview) {
            return false;
        }
        if (outcome.Receipt is not { } receipt) {
            Message = "No proof receipt was returned. Publication remains unconfirmed; keep your draft and review the native catalog before reopening.";
            return false;
        }
        if (receipt.AgentId != agentId || receipt.CapabilityId != capabilityId || draft.Id != agentId || refreshed.Draft.Id != agentId) {
            throw new InvalidOperationException("Verification read-back does not belong to this editor attempt.");
        }
        var recovery = receipt.Classify(refreshed.Agents, refreshed.Capabilities);
        if (recovery == CapabilityProofRecovery.Unconfirmed) {
            Message = "Proof publication is still unconfirmed. Retry this read without repeating the diagnostic.";
            return false;
        }
        NeedsReview = false;
        if (recovery == CapabilityProofRecovery.NotPublished) {
            Message = "The exact prior native state remains. No proof was published; a new diagnostic requires an explicit Verify action.";
            return false;
        }
        if (recovery != CapabilityProofRecovery.Satisfied || expectedVersion != receipt.ExpectedUpdatedAtUtc ||
            draft.ExpectedUpdatedAtUtc != expectedVersion) {
            IsConflict = true;
            Message = "The agent changed elsewhere. Your unsaved draft and original version are retained; reopen only after preserving and reviewing your edits.";
            return false;
        }
        var published = refreshed.Agents.Single(agent => agent.Id == agentId);
        if (refreshed.Draft.ExpectedUpdatedAtUtc != published.UpdatedAtUtc) {
            NeedsReview = true;
            throw new InvalidOperationException("Verification read-back contains inconsistent versions.");
        }
        draft.ExpectedUpdatedAtUtc = published.UpdatedAtUtc;
        Message = "Capability proof verified. Unsaved editor changes are retained and have not been saved.";
        return true;
    }
}
