using CanDoItAll.Modules.AgentFramework.ProviderManagement;

namespace CanDoItAll.Modules.AgentFramework;

public enum SharedProviderTargetMutationKind { Publish, Unpublish, ImportedSettings, Retirement }

public sealed record SharedProviderTargetAttempt(
    Guid AttemptId, Guid ProviderId, SharedProviderTargetMutationKind Kind,
    SharedProviderProfileSharingSnapshot Before);

public sealed class SharedProviderRecovery {
    private readonly Dictionary<Guid, SharedProviderTargetAttempt> targets = [];
    private readonly HashSet<Guid> delivered = [];
    private readonly Dictionary<Guid, SharedProviderChange> knownChanges = [];

    public SharedProviderSourceMutationAttempt? Source { get; private set; }
    public bool SourceRetryAllowed { get; private set; }

    public SharedProviderTargetAttempt? FindTarget(Guid? providerId) =>
        providerId.HasValue ? targets.GetValueOrDefault(providerId.Value) : null;

    public SharedProviderTargetAttempt BeginTarget(Guid id, SharedProviderTargetMutationKind kind,
        SharedProviderProfileSharingSnapshot before) {
        if (targets.ContainsKey(id)) {
            throw new InvalidOperationException("Verify the pending sharing attempt before another write.");
        }
        var attempt = new SharedProviderTargetAttempt(Guid.NewGuid(), id, kind, before);
        targets[id] = attempt;
        return attempt;
    }

    public bool CompleteTarget(SharedProviderTargetAttempt attempt) =>
        targets.GetValueOrDefault(attempt.ProviderId)?.AttemptId == attempt.AttemptId && targets.Remove(attempt.ProviderId);

    public void BeginSource(SharedProviderSourceMutationAttempt attempt) {
        if (Source is not null && (!SourceRetryAllowed || Source.AttemptId != attempt.AttemptId)) {
            throw new InvalidOperationException("Verify the pending source attempt before another write.");
        }
        Source = attempt;
        SourceRetryAllowed = false;
    }

    public void AllowSourceRetry(SharedProviderSourceMutationAttempt attempt) {
        if (Source?.AttemptId == attempt.AttemptId) {
            SourceRetryAllowed = true;
        }
    }

    public bool CompleteSource(SharedProviderSourceMutationAttempt attempt) {
        if (Source?.AttemptId != attempt.AttemptId) {
            return false;
        }
        Source = null;
        SourceRetryAllowed = false;
        return true;
    }

    public void RecordCommit(Guid attemptId, SharedProviderChange? change) {
        if (change is { CommitState: SharedProviderCommitState.Committed }) {
            knownChanges[attemptId] = change;
        }
    }

    public SharedProviderChange? KnownChange(Guid attemptId) => knownChanges.GetValueOrDefault(attemptId);

    public bool ClaimPublication(Guid attemptId) => delivered.Add(attemptId);
}
