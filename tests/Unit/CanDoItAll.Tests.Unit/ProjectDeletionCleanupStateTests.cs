using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using CanDoItAll.Modules.Projects.Pages.Components;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectDeletionCleanupStateTests {
    [Fact]
    public void Owner_completion_uncertainty_retains_exact_targets_across_inventory_refreshes() {
        var projectId = Guid.NewGuid();
        var recoveryId = Guid.NewGuid();
        ProjectDeletionRecoveryFailure[] failures = [
            new(ProjectDeletionRecoveryOperation.ParticipantCleanup, new("first"), recoveryId),
            new(ProjectDeletionRecoveryOperation.ParticipantCleanup, new("second"), recoveryId),
            new(ProjectDeletionRecoveryOperation.ParticipantCleanup, new("no-preparation"), null)
        ];
        var state = new ProjectDeletionCleanupState();
        state.Observe(new(projectId, failures, "Retry the exact durable operation.", OwnerCompletionUnconfirmed: true));
        var targets = state.Statuses.Select(item => item.Target).ToArray();
        Assert.Equal(2, targets.Length);
        Assert.All(targets, target => {
            Assert.Equal(projectId, target.ProjectId);
            Assert.Equal(recoveryId, target.RecoveryId);
        });
        Assert.Equal(failures.Take(2).Select(item => item.ParticipantId), targets.Select(item => item.ParticipantId));

        foreach (bool inventoryContainsTargets in new[] { true, false, true }) {
            state.Refresh(inventoryContainsTargets ? targets.Select(target => new ProjectDeletionPendingCleanup(
                target.ProjectId, target.ParticipantId, target.RecoveryId, ProjectDeletionRecoveryStatus.Pending,
                true, DateTimeOffset.UtcNow, "The current list permits retry.")).ToArray() : []);
            Assert.Equal(targets, state.Statuses.Select(item => item.Target));
            Assert.All(state.Statuses, status => {
                Assert.True(status.RetryBlocked);
                Assert.False(status.CanRetryNow);
                Assert.False(state.CanRetry(status.Target));
                Assert.Null(status.RetryAvailableAtUtc);
                Assert.Equal(ProjectDeletionCleanupState.OwnerCompletionGuidance, status.RetryGuidance);
                state.Complete(status.Target);
            });
            Assert.Equal(2, state.Statuses.Count);
            state.Observe(new(projectId, failures, "A later normal observation does not clear the block."));
        }

        var empty = new ProjectDeletionCleanupState();
        empty.Observe(new(projectId, [], "Owner review required.", OwnerCompletionUnconfirmed: true));
        Assert.Empty(empty.Statuses);
    }

    [Fact]
    public void Normal_retry_keeps_exact_target_and_unknown_completion_is_not_cleared_by_reads() {
        var projectId = Guid.NewGuid();
        var target = new ProjectCleanupTarget(projectId, new("participant"), Guid.NewGuid());
        var state = new ProjectDeletionCleanupState();
        state.Observe(new(projectId, [new(ProjectDeletionRecoveryOperation.ParticipantCleanup,
            target.ParticipantId, target.RecoveryId)], "Retry this exact operation."));
        Assert.True(state.CanRetry(target));
        Assert.False(state.CanRetry(target with { RecoveryId = Guid.NewGuid() }));
        Assert.False(Assert.Single(state.Statuses).RetryBlocked);
        state.Complete(target);
        Assert.Empty(state.Statuses);

        var pending = new ProjectDeletionPendingCleanup(projectId, target.ParticipantId, target.RecoveryId,
            ProjectDeletionRecoveryStatus.Processing, false, DateTimeOffset.UtcNow.AddMinutes(1), "Refresh the current claim.");
        state.Refresh([pending]);
        Assert.True(state.CanRetry(target));
        Assert.False(Assert.Single(state.Statuses).CanRetryNow);
        state.MarkUnconfirmed(target);
        state.Refresh([]);
        var retained = Assert.Single(state.Statuses);
        Assert.Equal(target, retained.Target);
        Assert.True(retained.RetryBlocked);
        Assert.Equal(ProjectDeletionCleanupState.UnconfirmedGuidance, retained.RetryGuidance);
        Assert.False(state.CanRetry(target));
        state.Refresh([pending with { CanRetryNow = true }]);
        Assert.True(Assert.Single(state.Statuses).RetryBlocked);
        Assert.False(state.CanRetry(target));
        state.Observe(new(projectId, [], "The retry release needs review.", OwnerCompletionUnconfirmed: true));
        state.MarkUnconfirmed(target, ownerCompletionUnconfirmed: true);
        state.MarkUnconfirmed(target);
        state.Refresh([]);
        Assert.Equal(ProjectDeletionCleanupState.OwnerCompletionGuidance, Assert.Single(state.Statuses).RetryGuidance);
    }
}
