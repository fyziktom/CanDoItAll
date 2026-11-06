using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.AgentFramework.SharedProviders.UI;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class SharedProviderRecovery {
    private readonly object gate = new();
    private readonly Dictionary<Guid, SharedProviderTargetAttempt> targets = [];
    private readonly Dictionary<Guid, SharedProviderChangeDelivery> deliveries = [];
    private readonly Dictionary<Guid, SharedProviderImportDraft> targetDrafts = [];
    private readonly Queue<SharedProviderTargetAttempt> reviewedTargets = [];
    private SharedProviderSourceMutationAttempt? source;
    private ProviderRecoveryAddress? address;
    private SharedProviderSourceDraft? retainedSourceDraft;
    private SharedProviderCatalogSelection? retainedSourceCatalog;
    private readonly Queue<SharedProviderSourceMutationAttempt> reviewedSources = [];
    private const int MaximumReviewedSources = 32;

    public IReadOnlyList<SharedProviderTargetAttempt> ReviewedTargets {
        get {
            lock (gate) {
                return reviewedTargets.ToArray();
            }
        }
    }

    public SharedProviderImportDraft? TargetDraft(Guid? providerId) {
        lock (gate) {
            return providerId is { } id ? targetDrafts.GetValueOrDefault(id) : null;
        }
    }

    public void RetainTargetDraft(SharedProviderTargetAttempt attempt, SharedProviderImportDraft? draft) {
        lock (gate) {
            if (IsCurrent(attempt) && draft is not null && draft.Baseline.ProviderId == attempt.ProviderId) {
                targetDrafts[attempt.ProviderId] = draft;
            }
        }
    }

    public bool AcknowledgeTargetCurrent(SharedProviderTargetAttempt attempt) {
        lock (gate) {
            if (!IsCurrent(attempt) || deliveries.ContainsKey(attempt.AttemptId)) {
                return false;
            }
            if (reviewedTargets.Count == MaximumReviewedSources) {
                reviewedTargets.Dequeue();
            }
            reviewedTargets.Enqueue(attempt);
            targets.Remove(attempt.ProviderId);
            targetDrafts.Remove(attempt.ProviderId);
            return true;
        }
    }

    public SharedProviderSourceDraft? SourceDraft {
        get {
            lock (gate) {
                return retainedSourceDraft;
            }
        }
    }

    public IReadOnlyList<SharedProviderSourceMutationAttempt> ReviewedSources {
        get {
            lock (gate) {
                return reviewedSources.ToArray();
            }
        }
    }

    public SharedProviderCatalogSelection? SourceCatalog {
        get {
            lock (gate) {
                return retainedSourceCatalog;
            }
        }
    }

    public void RetainSourceCatalog(SharedProviderSourceMutationAttempt attempt, SharedProviderCatalogSelection catalog) {
        lock (gate) {
            if (source?.AttemptId == attempt.AttemptId && attempt.Kind == SharedProviderSourceMutationKind.Synchronize &&
                catalog.Origin.SourceId == attempt.SourceId) {
                retainedSourceCatalog = catalog;
            }
        }
    }

    public void Bind(ProviderRecoveryAddress opening) {
        lock (gate) {
            if (address is not null && address != opening) {
                if (source is not null || targets.Count > 0 || deliveries.Count > 0) {
                    throw new UnauthorizedAccessException("Pending shared-provider attempts belong to another original dataset or actor.");
                }
                retainedSourceDraft = null;
                retainedSourceCatalog = null;
                reviewedSources.Clear();
                reviewedTargets.Clear();
                targetDrafts.Clear();
            }
            address = opening;
        }
    }

    public void RetainSourceDraft(SharedProviderSourceMutationAttempt attempt, SharedProviderSourceDraft draft) {
        lock (gate) {
            if (source?.AttemptId == attempt.AttemptId && draft.SourceId == attempt.SourceId) {
                retainedSourceDraft = draft;
            }
        }
    }

    public bool AcknowledgeSourceCurrent(SharedProviderSourceMutationAttempt attempt) => AcknowledgeSource(attempt,
        attempt.Kind is SharedProviderSourceMutationKind.Create or SharedProviderSourceMutationKind.Update or SharedProviderSourceMutationKind.Synchronize);

    public bool AcknowledgeSourceStatus(SharedProviderSourceMutationAttempt attempt) => AcknowledgeSource(attempt,
        attempt.Kind is SharedProviderSourceMutationKind.Enablement or SharedProviderSourceMutationKind.Delete or SharedProviderSourceMutationKind.Test or SharedProviderSourceMutationKind.Synchronize);

    private bool AcknowledgeSource(SharedProviderSourceMutationAttempt attempt, bool allowed) {
        lock (gate) {
            if (!allowed || source?.AttemptId != attempt.AttemptId || deliveries.ContainsKey(attempt.AttemptId)) {
                return false;
            }
            if (reviewedSources.Count == MaximumReviewedSources) {
                reviewedSources.Dequeue();
            }
            reviewedSources.Enqueue(attempt);
            source = null;
            retainedSourceDraft = null;
            retainedSourceCatalog = null;
            return true;
        }
    }

    public SharedProviderSourceMutationAttempt? Source {
        get {
            lock (gate) {
                return source;
            }
        }
    }

    public SharedProviderTargetAttempt? FindTarget(Guid? providerId) {
        lock (gate) {
            return providerId.HasValue ? targets.GetValueOrDefault(providerId.Value) : null;
        }
    }

    public SharedProviderTargetAttempt BeginTarget(Guid id, SharedProviderTargetMutationKind kind,
        SharedProviderProfileSharingSnapshot before, SharedProviderImportedProfileUpdateRequest? request = null) {
        lock (gate) {
            if (targets.ContainsKey(id)) {
                throw new InvalidOperationException("Resolve the pending sharing attempt before another write.");
            }
            if (before.ProviderProfileId != id) {
                throw new ArgumentException("The sharing snapshot has a different provider identity.", nameof(before));
            }
            var attempt = new SharedProviderTargetAttempt(Guid.NewGuid(), id, kind, before,
                SharedProviderTargetVerification.Capture(kind, before, request));
            targets[id] = attempt;
            return attempt;
        }
    }

    public bool CompleteTarget(SharedProviderTargetAttempt attempt) {
        lock (gate) {
            if (!IsCurrent(attempt) || deliveries.ContainsKey(attempt.AttemptId) || !targets.Remove(attempt.ProviderId)) {
                return false;
            }
            targetDrafts.Remove(attempt.ProviderId);
            return true;
        }
    }

    public void BeginSource(SharedProviderSourceMutationAttempt attempt) {
        lock (gate) {
            if (source is not null) {
                throw new InvalidOperationException("Resolve the pending source attempt before another write.");
            }
            retainedSourceDraft = null;
            retainedSourceCatalog = null;
            source = attempt;
        }
    }

    public bool CompleteSource(SharedProviderSourceMutationAttempt attempt) {
        lock (gate) {
            if (source?.AttemptId != attempt.AttemptId || deliveries.ContainsKey(attempt.AttemptId)) {
                return false;
            }
            source = null;
            retainedSourceDraft = null;
            retainedSourceCatalog = null;
            return true;
        }
    }

    public void RecordCommit(Guid attemptId, SharedProviderChange? change) {
        lock (gate) {
            if (IsActive(attemptId) && change is { CommitState: SharedProviderCommitState.Committed }) {
                deliveries.TryAdd(attemptId, new(attemptId, change));
            }
        }
    }

    public SharedProviderChangeDelivery? PendingDelivery(Guid attemptId) {
        lock (gate) {
            return deliveries.GetValueOrDefault(attemptId);
        }
    }

    public Task<SharedProviderDeliveryDisposition> DeliverTargetAsync(SharedProviderTargetAttempt attempt,
        Guid? providerId, Func<bool> ownerIsCurrent, Func<SharedProviderChangeDelivery, Task> callback) =>
        DeliverAsync(attempt.AttemptId, () => providerId == attempt.ProviderId && IsCurrent(attempt) && ownerIsCurrent(),
            () => {
                targets.Remove(attempt.ProviderId);
                targetDrafts.Remove(attempt.ProviderId);
            }, callback);

    public Task<SharedProviderDeliveryDisposition> DeliverSourceAsync(SharedProviderSourceMutationAttempt attempt,
        Guid sourceId, Func<bool> ownerIsCurrent, Func<SharedProviderChangeDelivery, Task> callback) =>
        DeliverAsync(attempt.AttemptId, () => sourceId == attempt.SourceId && source?.AttemptId == attempt.AttemptId && ownerIsCurrent(),
            () => {
                source = null;
                retainedSourceDraft = null;
                retainedSourceCatalog = null;
            }, callback);

    private async Task<SharedProviderDeliveryDisposition> DeliverAsync(Guid attemptId, Func<bool> isCurrent,
        Action complete, Func<SharedProviderChangeDelivery, Task> callback) {
        SharedProviderChangeDelivery delivery;
        lock (gate) {
            if (!isCurrent() || !deliveries.TryGetValue(attemptId, out delivery!)) {
                return SharedProviderDeliveryDisposition.NotCurrent;
            }
            if (delivery.InProgress) {
                return SharedProviderDeliveryDisposition.InProgress;
            }
            delivery.InProgress = true;
        }
        try {
            if (!delivery.IsAcknowledged) {
                await callback(delivery);
            }
            lock (gate) {
                if (!delivery.IsAcknowledged || !isCurrent() || deliveries.GetValueOrDefault(attemptId) != delivery) {
                    return SharedProviderDeliveryDisposition.Pending;
                }
                deliveries.Remove(attemptId);
                complete();
                return SharedProviderDeliveryDisposition.Acknowledged;
            }
        } catch (Exception) {
            return SharedProviderDeliveryDisposition.Pending;
        } finally {
            lock (gate) {
                delivery.InProgress = false;
            }
        }
    }

    private bool IsCurrent(SharedProviderTargetAttempt attempt) =>
        targets.GetValueOrDefault(attempt.ProviderId)?.AttemptId == attempt.AttemptId;

    private bool IsActive(Guid attemptId) =>
        source?.AttemptId == attemptId || targets.Values.Any(attempt => attempt.AttemptId == attemptId);
}
