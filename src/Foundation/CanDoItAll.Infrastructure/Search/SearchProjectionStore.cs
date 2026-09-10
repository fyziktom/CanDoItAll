using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.Search;

public sealed record SearchProjectionPartition(string SourceType, long AdvisoryLockId);
public sealed record SearchProjectionEntry(SearchDocumentInput Document, DateTimeOffset UpdatedAtUtc);

public sealed class SearchProjectionStore(
    IDbContextFactory<SearchDbContext> factory,
    CoordinatedDatabaseTransaction transactions) {
    private const int BatchSize = 250;
    private static readonly SemaphoreSlim MutationGate = new(1, 1);

    public async Task UpsertAsync(SearchProjectionPartition partition, SearchProjectionEntry entry,
        Func<CancellationToken, Task<bool>> canApply, CancellationToken cancellationToken = default) {
        await MutateAsync(partition, async (context, token) => {
            if (!await canApply(token)) {
                return 0;
            }
            RequirePartition(partition, entry);
            var entity = await context.Set<SearchDocument>().FirstOrDefaultAsync(
                item => item.SourceType == partition.SourceType && item.SourceKey == entry.Document.SourceKey, token);
            if (entity is null) {
                entity = new SearchDocument();
                context.Add(entity);
            }
            Apply(entry, entity);
            await context.SaveChangesAsync(token);
            return 1;
        }, cancellationToken);
    }

    public async Task RemoveAsync(SearchProjectionPartition partition, string sourceKey,
        Func<CancellationToken, Task<bool>> canApply, CancellationToken cancellationToken = default) {
        await MutateAsync(partition, async (context, token) => {
            if (!await canApply(token)) {
                return 0;
            }
            return await context.Set<SearchDocument>()
                .Where(item => item.SourceType == partition.SourceType && item.SourceKey == sourceKey)
                .ExecuteDeleteAsync(token);
        }, cancellationToken);
    }

    public Task<int> RebuildAsync(SearchProjectionPartition partition, IAsyncEnumerable<SearchProjectionEntry> entries,
        CancellationToken cancellationToken = default) => MutateAsync(partition, async (context, token) => {
            await context.Set<SearchDocument>().Where(item => item.SourceType == partition.SourceType).ExecuteDeleteAsync(token);
            var pending = 0;
            var processed = 0;
            await foreach (var entry in entries.WithCancellation(token)) {
                RequirePartition(partition, entry);
                var entity = new SearchDocument();
                Apply(entry, entity);
                context.Add(entity);
                pending++;
                if (pending < BatchSize) {
                    continue;
                }
                await context.SaveChangesAsync(token);
                processed += pending;
                pending = 0;
                context.ChangeTracker.Clear();
            }
            if (pending > 0) {
                await context.SaveChangesAsync(token);
            }
            return processed + pending;
        }, cancellationToken);

    private async Task<int> MutateAsync(SearchProjectionPartition partition,
        Func<SearchDbContext, CancellationToken, Task<int>> mutation, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(partition);
        ArgumentException.ThrowIfNullOrWhiteSpace(partition.SourceType);
        await MutationGate.WaitAsync(cancellationToken);
        try {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            using (transactions.Enter(context)) {
                if (context.Database.IsNpgsql()) {
                    await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({partition.AdvisoryLockId})", cancellationToken);
                }
                var result = await mutation(context, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
        } finally {
            MutationGate.Release();
        }
    }

    private static void RequirePartition(SearchProjectionPartition partition, SearchProjectionEntry entry) {
        if (!string.Equals(partition.SourceType, entry.Document.SourceType, StringComparison.Ordinal)) {
            throw new InvalidOperationException("A search projection entry must belong to its declared source partition.");
        }
    }

    private static void Apply(SearchProjectionEntry entry, SearchDocument entity) {
        var document = entry.Document;
        entity.SourceType = document.SourceType;
        entity.SourceKey = document.SourceKey;
        entity.ProjectId = document.ProjectId;
        entity.Category = document.Category;
        entity.Title = document.Title.Trim();
        entity.Summary = document.Summary.Trim();
        entity.Body = document.Body.Trim();
        entity.Route = document.Route.Trim();
        entity.UpdatedAtUtc = entry.UpdatedAtUtc;
    }
}
