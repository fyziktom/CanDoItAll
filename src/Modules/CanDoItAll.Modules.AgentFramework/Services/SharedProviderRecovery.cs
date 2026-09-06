using CanDoItAll.Modules.AgentFramework.ProviderManagement;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class SharedProviderRecovery {
    private readonly object gate = new();
    private readonly Dictionary<Guid, SharedProviderTargetAttempt> targets = [];
    private readonly Dictionary<Guid, SharedProviderChangeDelivery> deliveries = [];
    private SharedProviderSourceMutationAttempt? source;
    private bool sourceRetryAllowed;

    public SharedProviderSourceMutationAttempt? Source {
        get {
            lock (gate) {
                return source;
            }
        }
    }

    public bool SourceRetryAllowed {
        get {
            lock (gate) {
                return sourceRetryAllowed;
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
            return IsCurrent(attempt) && !deliveries.ContainsKey(attempt.AttemptId) && targets.Remove(attempt.ProviderId);
        }
    }

    public void BeginSource(SharedProviderSourceMutationAttempt attempt) {
        lock (gate) {
            if (source is not null && (!sourceRetryAllowed || source.AttemptId != attempt.AttemptId || deliveries.ContainsKey(attempt.AttemptId))) {
                throw new InvalidOperationException("Resolve the pending source attempt before another write.");
            }
            source = attempt;
            sourceRetryAllowed = false;
        }
    }

    public void AllowSourceRetry(SharedProviderSourceMutationAttempt attempt) {
        lock (gate) {
            if (source?.AttemptId == attempt.AttemptId && !deliveries.ContainsKey(attempt.AttemptId)) {
                sourceRetryAllowed = true;
            }
        }
    }

    public bool CompleteSource(SharedProviderSourceMutationAttempt attempt) {
        lock (gate) {
            if (source?.AttemptId != attempt.AttemptId || deliveries.ContainsKey(attempt.AttemptId)) {
                return false;
            }
            source = null;
            sourceRetryAllowed = false;
            return true;
        }
    }

    public void RecordCommit(Guid attemptId, SharedProviderChange? change) {
        lock (gate) {
            if (IsActive(attemptId) && change is { CommitState: SharedProviderCommitState.Committed }) {
                deliveries.TryAdd(attemptId, new(attemptId, change));
                if (source?.AttemptId == attemptId) {
                    sourceRetryAllowed = false;
                }
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
            () => targets.Remove(attempt.ProviderId), callback);

    public Task<SharedProviderDeliveryDisposition> DeliverSourceAsync(SharedProviderSourceMutationAttempt attempt,
        Guid sourceId, Func<bool> ownerIsCurrent, Func<SharedProviderChangeDelivery, Task> callback) =>
        DeliverAsync(attempt.AttemptId, () => sourceId == attempt.SourceId && source?.AttemptId == attempt.AttemptId && ownerIsCurrent(),
            () => {
                source = null;
                sourceRetryAllowed = false;
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
                if (!isCurrent() || deliveries.GetValueOrDefault(attemptId) != delivery) {
                    return SharedProviderDeliveryDisposition.Pending;
                }
                delivery.Acknowledge();
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
