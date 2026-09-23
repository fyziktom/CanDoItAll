using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Modules.AgentFramework;

public sealed record ProviderUnresolvedAttempt(
    ProviderMutationAttempt Attempt,
    ProviderEditorSubmission? Submission,
    EditContext Context,
    ProviderEditorSection Section,
    bool RetryAllowed = false);

public sealed class ProviderEditorRecovery {
    private readonly Dictionary<Guid, ProviderUnresolvedAttempt> attempts = [];
    private readonly Dictionary<Guid, Guid> current = [];

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
        if (current.GetValueOrDefault(entry.Attempt.ProviderId) == entry.Attempt.AttemptId) {
            attempts[entry.Attempt.ProviderId] = entry;
        }
    }

    public bool Remove(ProviderUnresolvedAttempt entry) {
        if (attempts.GetValueOrDefault(entry.Attempt.ProviderId) != entry) {
            return false;
        }
        if (current.GetValueOrDefault(entry.Attempt.ProviderId) == entry.Attempt.AttemptId) {
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
