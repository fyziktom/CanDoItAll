using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.AspNetCore.Components.Forms;
using CanDoItAll.Editing;

namespace CanDoItAll.Modules.AgentFramework;

public sealed record ProviderUnresolvedAttempt(
    ProviderMutationAttempt Attempt,
    ProviderEditorSubmission? Submission,
    EditContext Context,
    ProviderEditorSection Section,
    bool RetryAllowed = false,
    Guid? DispatchId = null);

public sealed record ProviderRecoveryAddress(EditingScope Scope, string PrincipalStamp);

public sealed class ProviderEditorRecovery {
    private const int MaximumAcknowledgedAttempts = 32;
    private readonly Dictionary<Guid, ProviderUnresolvedAttempt> attempts = [];
    private readonly Dictionary<Guid, Guid> current = [];
    private ProviderRecoveryAddress? address;
    private readonly Queue<ProviderMutationAttempt> acknowledgedDiagnostics = [];
    public IReadOnlyList<ProviderMutationAttempt> AcknowledgedDiagnostics => acknowledgedDiagnostics.ToArray();
    private readonly Queue<ProviderMutationAttempt> reviewedUnconfirmed = [];
    public IReadOnlyList<ProviderMutationAttempt> ReviewedUnconfirmed => reviewedUnconfirmed.ToArray();

    public void Bind(ProviderRecoveryAddress opening) {
        if (address is not null && address != opening) {
            if (attempts.Count > 0 || current.Count > 0) {
                throw new UnauthorizedAccessException("Pending provider attempts belong to an earlier dataset or actor. Reload the workspace with current access.");
            }
            acknowledgedDiagnostics.Clear();
            reviewedUnconfirmed.Clear();
        }
        address = opening;
    }

    public bool AcknowledgeDiagnostic(ProviderUnresolvedAttempt entry) {
        if (entry.Attempt.Kind != ProviderMutationKind.HealthPersistence || !Remove(entry)) {
            return false;
        }
        if (acknowledgedDiagnostics.Count == MaximumAcknowledgedAttempts) {
            acknowledgedDiagnostics.Dequeue();
        }
        acknowledgedDiagnostics.Enqueue(entry.Attempt);
        return true;
    }

    public bool AcknowledgeCurrentReview(ProviderUnresolvedAttempt entry) {
        if (entry.Attempt.Kind is not (ProviderMutationKind.Create or ProviderMutationKind.Update) || !Remove(entry)) {
            return false;
        }
        if (reviewedUnconfirmed.Count == MaximumAcknowledgedAttempts) {
            reviewedUnconfirmed.Dequeue();
        }
        reviewedUnconfirmed.Enqueue(entry.Attempt);
        return true;
    }

    public void Begin(ProviderMutationAttempt attempt) => current[attempt.ProviderId] = attempt.AttemptId;

    public void Complete(ProviderMutationAttempt attempt) {
        if (current.GetValueOrDefault(attempt.ProviderId) == attempt.AttemptId) {
            attempts.Remove(attempt.ProviderId);
            current.Remove(attempt.ProviderId);
        }
    }

    public ProviderUnresolvedAttempt? Find(Guid? providerId) => providerId is { } id
        ? attempts.GetValueOrDefault(id)
        : attempts.Values.FirstOrDefault(entry => entry.Attempt.IsCreate);

    public void Retain(ProviderUnresolvedAttempt entry) {
        if (current.GetValueOrDefault(entry.Attempt.ProviderId) == (entry.DispatchId ?? entry.Attempt.AttemptId)) {
            attempts[entry.Attempt.ProviderId] = entry;
        }
    }

    public bool Remove(ProviderUnresolvedAttempt entry) {
        if (attempts.GetValueOrDefault(entry.Attempt.ProviderId) != entry) {
            return false;
        }
        if (current.GetValueOrDefault(entry.Attempt.ProviderId) == (entry.DispatchId ?? entry.Attempt.AttemptId)) {
            current.Remove(entry.Attempt.ProviderId);
        }
        return attempts.Remove(entry.Attempt.ProviderId);
    }

    public void AllowRetry(ProviderUnresolvedAttempt entry) {
        if (attempts.GetValueOrDefault(entry.Attempt.ProviderId) == entry) {
            attempts[entry.Attempt.ProviderId] = entry with { RetryAllowed = true };
        }
    }
}
