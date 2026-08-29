using System.Buffers.Binary;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

internal static class HistoryWriteLock {
    internal static Task AttemptAsync(AppDbContext db, Guid attemptId, CancellationToken cancellationToken) {
        var key = BinaryPrimitives.ReadInt64LittleEndian(attemptId.ToByteArray());
        return db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({key})", cancellationToken);
    }
}
