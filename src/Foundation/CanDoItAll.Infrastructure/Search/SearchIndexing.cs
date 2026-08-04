using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Infrastructure.Search;

public sealed class SearchDocument
{
    public const string ProjectSourceType = "project";

    public Guid Id { get; set; } = Guid.NewGuid();

    public string SourceType { get; set; } = string.Empty;

    public string SourceKey { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Route { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class SearchDocumentConfiguration : IEntityTypeConfiguration<SearchDocument>
{
    public void Configure(EntityTypeBuilder<SearchDocument> builder)
    {
        builder.ToTable("Infrastructure_SearchDocuments");
        builder.HasKey(document => document.Id);
        builder.Property(document => document.SourceType).HasMaxLength(120).IsRequired();
        builder.Property(document => document.SourceKey).HasMaxLength(200).IsRequired();
        builder.Property(document => document.Category).HasMaxLength(120).IsRequired();
        builder.Property(document => document.Title).HasMaxLength(200).IsRequired();
        builder.Property(document => document.Summary).HasColumnType("TEXT");
        builder.Property(document => document.Body).HasColumnType("TEXT");
        builder.Property(document => document.Route).HasMaxLength(500).IsRequired();
        builder.HasIndex(document => new { document.SourceType, document.SourceKey }).IsUnique();
    }
}

public sealed record SearchDocumentInput(
    string SourceType,
    string SourceKey,
    string Category,
    string Title,
    string Summary,
    string Body,
    string Route,
    Guid? ProjectId = null);

public sealed record SearchResult(
    Guid Id,
    string Category,
    string Title,
    string Summary,
    string Route,
    Guid? ProjectId);

public interface ISearchIndexService
{
    Task UpsertAsync(SearchDocumentInput input, CancellationToken cancellationToken = default);

    Task DeleteAsync(string sourceType, string sourceKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int take = 12, CancellationToken cancellationToken = default);
}

/* codex-capsule
kind: service
name: SearchIndexService
summary: Maintains a relational search index for projects, prompts, resources, validations, and tests.
owns: search-documents, simple-query-ranking
deps: AppDbContext, IClock
risks: stale-summary, duplicate-source-key
tests: integration:SearchIndexServiceTests
inputs: SearchDocumentInput, query text
outputs: SearchResult list
*/
public sealed class SearchIndexService(IDbContextFactory<AppDbContext> dbContextFactory, SharedKernel.IClock clock) : ISearchIndexService
{
    public async Task UpsertAsync(SearchDocumentInput input, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<SearchDocument>()
            .FirstOrDefaultAsync(
                document => document.SourceType == input.SourceType && document.SourceKey == input.SourceKey,
                cancellationToken);

        if (entity is null)
        {
            entity = new SearchDocument();
            await dbContext.Set<SearchDocument>().AddAsync(entity, cancellationToken);
        }

        entity.SourceType = input.SourceType.Trim();
        entity.SourceKey = input.SourceKey.Trim();
        entity.ProjectId = input.ProjectId;
        entity.Category = input.Category.Trim();
        entity.Title = input.Title.Trim();
        entity.Summary = input.Summary.Trim();
        entity.Body = input.Body.Trim();
        entity.Route = input.Route.Trim();
        entity.UpdatedAtUtc = clock.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string sourceType, string sourceKey, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<SearchDocument>()
            .FirstOrDefaultAsync(
                document => document.SourceType == sourceType && document.SourceKey == sourceKey,
                cancellationToken);

        if (entity is null)
        {
            return;
        }

        dbContext.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int take = 12, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var normalized = query.Trim().ToLowerInvariant();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Set<SearchDocument>()
            .Where(document =>
                EF.Functions.Like(document.Title.ToLower(), $"%{normalized}%") ||
                EF.Functions.Like(document.Summary.ToLower(), $"%{normalized}%") ||
                EF.Functions.Like(document.Body.ToLower(), $"%{normalized}%"))
            .OrderBy(document => document.Title)
            .Take(Math.Clamp(take, 1, 50))
            .Select(document => new SearchResult(
                document.Id,
                document.Category,
                document.Title,
                document.Summary,
                document.Route,
                document.ProjectId))
            .ToListAsync(cancellationToken);
    }
}
