using CanDoItAll.Infrastructure.ControlPlane;
using Npgsql;

namespace CanDoItAll.Modules.Projects;

internal static class ProjectDeletionRecoveryResources {
    internal static ProjectDeletionRecoveryRefusal Reason(Exception failure) => failure switch {
        ProjectDeletionRecoveryRefusedException refused => refused.Reason,
        ProjectDeletionRecoveryCleanupException => ProjectDeletionRecoveryRefusal.ProbeReleaseUnconfirmed,
        PostgresException { SqlState: PostgresErrorCodes.LockNotAvailable } => ProjectDeletionRecoveryRefusal.ExclusionBusy,
        TimeoutException => ProjectDeletionRecoveryRefusal.ExclusionBusy,
        DatasetActivationMismatchException => ProjectDeletionRecoveryRefusal.BindingChanged,
        OperationCanceledException => ProjectDeletionRecoveryRefusal.Cancelled,
        NotSupportedException or InvalidDataException => ProjectDeletionRecoveryRefusal.UnsupportedEvidence,
        InvalidOperationException { InnerException: { } inner } => Reason(inner),
        _ => ProjectDeletionRecoveryRefusal.ReadUnavailable
    };

    internal static CancellationTokenSource Deadline(CancellationToken cancellationToken) {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        return deadline;
    }

    internal static async Task<(Exception? Failure, bool CleanupFailed)> ReleaseAsync(Exception? primary, params IAsyncDisposable?[] owned) {
        var failures = primary is null ? new List<Exception>() : [primary];
        bool cleanupFailed = false;
        foreach (var resource in owned) {
            try {
                if (resource is not null) {
                    await resource.DisposeAsync();
                }
            } catch (Exception failure) {
                cleanupFailed = true;
                failures.Add(failure);
            }
        }
        return (failures.Count switch { 0 => null, 1 => failures[0], _ => new AggregateException(failures) }, cleanupFailed);
    }

    internal static void ThrowAfterRelease((Exception? Failure, bool CleanupFailed) result) {
        if (result.CleanupFailed) {
            throw new ProjectDeletionRecoveryCleanupException(result.Failure!);
        }
        if (result.Failure is not null) {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(result.Failure).Throw();
        }
    }

}
