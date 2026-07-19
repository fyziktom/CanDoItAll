using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Prompts;

public sealed class SearchIndexPromptGalleryProjectionDriver(
    IDbContextFactory<AppDbContext> dbContextFactory) : IPromptGalleryProjectionDriver
{
    public const string SourceType = "prompt";
    private const int BatchSize = 250;
    private const long PostgreSqlAdvisoryLockId = 7_142_033_841_991_137_043;
    private static readonly SemaphoreSlim MutationGate = new(1, 1);

    private static readonly PromptGalleryProjectionStatus ReadyStatus = new(
        nameof(SearchIndexPromptGalleryProjectionDriver),
        Enabled: true,
        PromptGalleryProjectionHealth.Ready,
        "Prompt Gallery items are projected into the relational search index.");

    public string Name => ReadyStatus.DriverName;

    public bool Enabled => true;

    public Task<PromptGalleryProjectionStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ReadyStatus);

    public async Task UpsertAsync(
        PromptGalleryProjectionDocument document,
        CancellationToken cancellationToken = default)
    {
        await MutationGate.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await AcquireDatabaseMutationLockAsync(dbContext, cancellationToken);
            var sourceKey = document.PromptArtifactId.ToString();
            var entity = await dbContext.Set<SearchDocument>()
                .FirstOrDefaultAsync(
                    item => item.SourceType == SourceType && item.SourceKey == sourceKey,
                    cancellationToken);
            if (entity is null)
            {
                entity = new SearchDocument();
                await dbContext.Set<SearchDocument>().AddAsync(entity, cancellationToken);
            }

            Apply(document, entity);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            MutationGate.Release();
        }
    }

    public async Task RemoveAsync(Guid promptArtifactId, CancellationToken cancellationToken = default)
    {
        await MutationGate.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await AcquireDatabaseMutationLockAsync(dbContext, cancellationToken);
            var sourceKey = promptArtifactId.ToString();
            await dbContext.Set<SearchDocument>()
                .Where(document => document.SourceType == SourceType && document.SourceKey == sourceKey)
                .ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            MutationGate.Release();
        }
    }

    public async Task<int> RebuildAsync(
        IAsyncEnumerable<PromptGalleryProjectionDocument> documents,
        CancellationToken cancellationToken = default)
    {
        await MutationGate.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await AcquireDatabaseMutationLockAsync(dbContext, cancellationToken);
            await dbContext.Set<SearchDocument>()
                .Where(document => document.SourceType == SourceType)
                .ExecuteDeleteAsync(cancellationToken);

            var projected = new List<SearchDocument>(BatchSize);
            var processedCount = 0;
            await foreach (var document in documents.WithCancellation(cancellationToken))
            {
                projected.Add(ToSearchDocumentEntity(document));
                if (projected.Count < BatchSize)
                {
                    continue;
                }

                dbContext.Set<SearchDocument>().AddRange(projected);
                await dbContext.SaveChangesAsync(cancellationToken);
                processedCount += projected.Count;
                projected.Clear();
                dbContext.ChangeTracker.Clear();
            }

            if (projected.Count > 0)
            {
                dbContext.Set<SearchDocument>().AddRange(projected);
                await dbContext.SaveChangesAsync(cancellationToken);
                processedCount += projected.Count;
            }

            await transaction.CommitAsync(cancellationToken);
            return processedCount;
        }
        finally
        {
            MutationGate.Release();
        }
    }

    private static SearchDocument ToSearchDocumentEntity(PromptGalleryProjectionDocument document)
    {
        var entity = new SearchDocument();
        Apply(document, entity);
        return entity;
    }

    private static void Apply(PromptGalleryProjectionDocument document, SearchDocument entity)
    {
        entity.SourceType = SourceType;
        entity.SourceKey = document.PromptArtifactId.ToString();
        entity.ProjectId = document.ProjectId;
        entity.Category = "Prompts";
        entity.Title = document.Title.Trim();
        entity.Summary = document.Summary.Trim();
        entity.Body = document.Content.Trim();
        entity.Route = document.Route.Trim();
        entity.UpdatedAtUtc = document.UpdatedAtUtc;
    }

    private static Task AcquireDatabaseMutationLockAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                dbContext.Database.ProviderName,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        return dbContext.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_xact_lock({PostgreSqlAdvisoryLockId})",
            cancellationToken);
    }
}
