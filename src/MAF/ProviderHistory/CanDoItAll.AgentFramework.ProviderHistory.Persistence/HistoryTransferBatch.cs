using System.Linq.Expressions;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

internal static class HistoryTransferBatch {
    private const int BatchSize = 500;

    internal static async Task<int> CopyAsync<T>(IQueryable<T> source, AppDbContext target,
        Expression<Func<T, Guid>> key, CancellationToken cancellationToken) where T : class {
        var copied = 0;
        Guid? cursor = null;
        var readKey = key.Compile();
        while (true) {
            var pageQuery = source.AsNoTracking();
            if (cursor is { } after) {
                var compare = Expression.Call(key.Body, nameof(Guid.CompareTo), Type.EmptyTypes, Expression.Constant(after));
                var predicate = Expression.Lambda<Func<T, bool>>(Expression.GreaterThan(compare, Expression.Constant(0)), key.Parameters);
                pageQuery = pageQuery.Where(predicate);
            }
            var page = await pageQuery.OrderBy(key).Take(BatchSize).ToListAsync(cancellationToken);
            if (page.Count == 0) {
                return copied;
            }
            target.AddRange(page);
            await target.SaveChangesAsync(cancellationToken);
            target.ChangeTracker.Clear();
            copied = checked(copied + page.Count);
            cursor = readKey(page[^1]);
        }
    }

    internal static async Task<int> CopyOwnersAsync(AppDbContext source, AppDbContext target, CancellationToken cancellationToken) {
        var copied = 0;
        Guid? sourceCursor = null;
        var entryCursor = Guid.Empty;
        while (true) {
            var query = source.Set<HistoryOwnerRow>().AsNoTracking();
            if (sourceCursor is { } after) {
                query = query.Where(row => row.SourceId.CompareTo(after) > 0 ||
                    row.SourceId == after && row.EntryId.CompareTo(entryCursor) > 0);
            }
            var page = await query.OrderBy(row => row.SourceId).ThenBy(row => row.EntryId).Take(BatchSize).ToListAsync(cancellationToken);
            if (page.Count == 0) {
                return copied;
            }
            target.AddRange(page);
            await target.SaveChangesAsync(cancellationToken);
            target.ChangeTracker.Clear();
            copied = checked(copied + page.Count);
            sourceCursor = page[^1].SourceId;
            entryCursor = page[^1].EntryId;
        }
    }
}
