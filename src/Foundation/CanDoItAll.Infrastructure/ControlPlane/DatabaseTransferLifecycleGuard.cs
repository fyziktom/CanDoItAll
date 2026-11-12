using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.ControlPlane;

internal static class DatabaseTransferLifecycleGuard {
    internal static async Task AcquireExclusionBeforeSnapshotAsync(DbContext target,
        CancellationToken cancellationToken) {
        await PrepareAsync(target, cancellationToken);
        // Utility statements must precede the first Serializable snapshot, including the lifecycle row read.
        await target.Database.ExecuteSqlRawAsync(
            "LOCK TABLE \"Infrastructure_DatasetLifecycle\" IN EXCLUSIVE MODE", cancellationToken);
    }

    internal static async Task AcquireImportExclusionBeforeSnapshotAsync(DbContext target,
        CancellationToken cancellationToken) {
        await PrepareAsync(target, cancellationToken);
        await target.Database.ExecuteSqlRawAsync(
            "LOCK TABLE \"Infrastructure_DatasetLifecycle\" IN EXCLUSIVE MODE NOWAIT", cancellationToken);
    }

    private static async Task PrepareAsync(DbContext target, CancellationToken cancellationToken) {
        if (!target.Database.IsNpgsql() || target.Database.CurrentTransaction is null) {
            throw new InvalidOperationException("A target transfer lifecycle guard requires a fresh PostgreSQL transaction.");
        }
        await target.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'", cancellationToken);
    }
}
