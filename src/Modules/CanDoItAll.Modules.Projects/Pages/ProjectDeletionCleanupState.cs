using CanDoItAll.Modules.Projects.Pages.Components;

namespace CanDoItAll.Modules.Projects.Pages;

internal sealed class ProjectDeletionCleanupState {
    internal const string OwnerCompletionGuidance = "The project was deleted, but the owner's completion could not be confirmed. Operator review is required before retrying cleanup.";
    internal const string UnconfirmedGuidance = "Cleanup completion is unconfirmed. Operator review is required before retrying this operation.";
    private readonly List<ProjectDeletionPendingCleanup> pending = [];
    private readonly Dictionary<ProjectCleanupTarget, (ProjectDeletionPendingCleanup Pending, bool OwnerCompletionUnconfirmed)> retained = [];

    public IReadOnlyList<ProjectCleanupStatus> Statuses => pending.Select(item => {
        var target = Target(item);
        bool blocked = retained.TryGetValue(target, out var observation);
        return new ProjectCleanupStatus(target, item.Status.ToString(), item.CanRetryNow && !blocked,
            blocked ? null : item.RetryAvailableAtUtc,
            blocked ? observation.OwnerCompletionUnconfirmed ? OwnerCompletionGuidance : UnconfirmedGuidance : item.RetryGuidance,
            RetryBlocked: blocked);
    }).ToArray();

    public void Refresh(IReadOnlyList<ProjectDeletionPendingCleanup> observed) {
        pending.Clear();
        pending.AddRange(observed);
        foreach (var (target, observation) in retained) {
            if (!pending.Any(item => Target(item) == target)) {
                pending.Add(observation.Pending);
            }
        }
    }

    public void Observe(ProjectDeletionRecovery recovery) {
        foreach (var failure in recovery.Failures) {
            if (failure.RecoveryId is not { } recoveryId) {
                continue;
            }
            var target = new ProjectCleanupTarget(recovery.ProjectId, failure.ParticipantId, recoveryId);
            var item = pending.FirstOrDefault(item => Target(item) == target);
            if (item is null) {
                item = new(recovery.ProjectId, failure.ParticipantId, recoveryId,
                    ProjectDeletionRecoveryStatus.Failed, true, null, recovery.RetryGuidance);
                pending.Add(item);
            }
            if (recovery.OwnerCompletionUnconfirmed) {
                retained[target] = (item, true);
            }
        }
    }

    public bool CanRetry(ProjectCleanupTarget target)
        => !retained.ContainsKey(target) && pending.Any(item => Target(item) == target);

    public void MarkUnconfirmed(ProjectCleanupTarget target, bool ownerCompletionUnconfirmed = false) {
        if (pending.FirstOrDefault(item => Target(item) == target) is { } item) {
            bool previousOwnerUncertainty = retained.TryGetValue(target, out var previous) && previous.OwnerCompletionUnconfirmed;
            retained[target] = (item, ownerCompletionUnconfirmed || previousOwnerUncertainty);
        }
    }

    public void Complete(ProjectCleanupTarget target) {
        if (!retained.ContainsKey(target)) {
            pending.RemoveAll(item => Target(item) == target);
        }
    }

    private static ProjectCleanupTarget Target(ProjectDeletionPendingCleanup item)
        => new(item.ProjectId, item.ParticipantId, item.RecoveryId);
}
