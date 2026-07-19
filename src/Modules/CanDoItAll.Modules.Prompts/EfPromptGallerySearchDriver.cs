using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Prompts;

public sealed class EfPromptGallerySearchDriver(IDbContextFactory<AppDbContext> dbContextFactory)
    : IPromptGallerySearchDriver
{
    public async Task<PromptGalleryPage<PromptGallerySearchItem>> SearchAsync(
        PromptGalleryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        query.Validate();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var artifacts = dbContext.Set<PromptArtifact>().AsNoTracking();

        if (!query.IncludeArchived)
        {
            artifacts = artifacts.Where(artifact => !artifact.IsArchived);
        }

        if (query.Kind.HasValue)
        {
            artifacts = artifacts.Where(artifact => artifact.Kind == query.Kind.Value);
        }

        if (query.Status.HasValue)
        {
            artifacts = artifacts.Where(artifact => artifact.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var text = query.Text.Trim().ToUpperInvariant();
            artifacts = artifacts.Where(artifact =>
                artifact.SearchText.Contains(text) ||
                dbContext.Set<PromptArtifactTag>().Any(link =>
                    link.PromptArtifactId == artifact.Id &&
                    dbContext.Set<PromptTag>().Any(tag =>
                        tag.Id == link.PromptTagId && tag.NameKey.Contains(text))));
        }

        foreach (var tag in NormalizeFilters(query.Tags))
        {
            artifacts = artifacts.Where(artifact => dbContext.Set<PromptArtifactTag>().Any(link =>
                link.PromptArtifactId == artifact.Id &&
                dbContext.Set<PromptTag>().Any(candidate =>
                    candidate.Id == link.PromptTagId && candidate.NameKey == tag)));
        }

        var providerKey = NormalizeKey(query.Provider);
        var modelKey = NormalizeKey(query.Model);
        if (providerKey is not null || modelKey is not null)
        {
            artifacts = artifacts.Where(artifact =>
                !dbContext.Set<PromptSupportedProviderModel>().Any(supported =>
                    supported.PromptArtifactId == artifact.Id) ||
                dbContext.Set<PromptSupportedProviderModel>().Any(supported =>
                    supported.PromptArtifactId == artifact.Id &&
                    (providerKey == null || supported.ProviderKey == providerKey) &&
                    (modelKey == null || supported.ModelKey == modelKey)));
        }

        if (query.Consumer.HasValue)
        {
            var consumer = query.Consumer.Value;
            artifacts = artifacts.Where(artifact =>
                !dbContext.Set<PromptSupportedConsumer>().Any(supported =>
                    supported.PromptArtifactId == artifact.Id) ||
                dbContext.Set<PromptSupportedConsumer>().Any(supported =>
                    supported.PromptArtifactId == artifact.Id && supported.Consumer == consumer));
        }

        var totalCount = await artifacts.CountAsync(cancellationToken);
        var skip = checked(query.PageIndex * query.PageSize);
        var rows = await (
                from artifact in artifacts
                join collection in dbContext.Set<PromptCollection>().AsNoTracking()
                    on artifact.CollectionId equals collection.Id into collections
                from collection in collections.DefaultIfEmpty()
                orderby artifact.UpdatedAtUtc descending, artifact.Title, artifact.Id
                select new SearchRow(
                    artifact.Id,
                    artifact.Title,
                    artifact.Summary,
                    artifact.CurrentDraftText.Length <= SearchRow.PreviewLength
                        ? artifact.CurrentDraftText
                        : artifact.CurrentDraftText.Substring(0, SearchRow.PreviewLength),
                    artifact.Kind,
                    artifact.Phase,
                    artifact.Status,
                    artifact.IsArchived,
                    collection == null ? null : collection.Name,
                    artifact.RecommendedTemperature,
                    artifact.RecommendedMaxOutputTokens,
                    artifact.RecommendedTopP,
                    artifact.CurrentVersionNumber,
                    artifact.UpdatedAtUtc))
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return new PromptGalleryPage<PromptGallerySearchItem>([], query.PageIndex, query.PageSize, totalCount);
        }

        var ids = rows.Select(row => row.Id).ToArray();
        var tagsByArtifact = await LoadTagsAsync(dbContext, ids, cancellationToken);
        var modelsByArtifact = await LoadModelsAsync(dbContext, ids, cancellationToken);
        var items = rows
            .Select(row => new PromptGallerySearchItem(
                row.Id,
                row.Title,
                row.Summary,
                row.ContentPreview,
                row.Kind,
                row.Phase,
                row.Status,
                row.IsArchived,
                row.CollectionName,
                tagsByArtifact.GetValueOrDefault(row.Id, []),
                modelsByArtifact.GetValueOrDefault(row.Id, []),
                new PromptModelRecommendations(
                    row.RecommendedTemperature,
                    row.RecommendedMaxOutputTokens,
                    row.RecommendedTopP),
                row.CurrentVersionNumber,
                row.UpdatedAtUtc))
            .ToList();

        return new PromptGalleryPage<PromptGallerySearchItem>(items, query.PageIndex, query.PageSize, totalCount);
    }

    private static IEnumerable<string> NormalizeFilters(IReadOnlyList<string>? values)
        => values is null
            ? []
            : values
                .Select(value => value.Trim().ToUpperInvariant())
                .Distinct(StringComparer.Ordinal);

    internal static string? NormalizeKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static async Task<Dictionary<Guid, IReadOnlyList<string>>> LoadTagsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> artifactIds,
        CancellationToken cancellationToken)
    {
        var rows = await (
                from link in dbContext.Set<PromptArtifactTag>().AsNoTracking()
                join tag in dbContext.Set<PromptTag>().AsNoTracking() on link.PromptTagId equals tag.Id
                where artifactIds.Contains(link.PromptArtifactId)
                orderby tag.Name
                select new { link.PromptArtifactId, tag.Name })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.PromptArtifactId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(row => row.Name).ToList());
    }

    private static async Task<Dictionary<Guid, IReadOnlyList<PromptProviderModel>>> LoadModelsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> artifactIds,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Set<PromptSupportedProviderModel>()
            .AsNoTracking()
            .Where(model => artifactIds.Contains(model.PromptArtifactId))
            .OrderBy(model => model.Provider)
            .ThenBy(model => model.Model)
            .Select(model => new { model.PromptArtifactId, model.Provider, model.Model })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.PromptArtifactId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PromptProviderModel>)group
                    .Select(row => new PromptProviderModel(row.Provider, row.Model))
                    .ToList());
    }

    private sealed record SearchRow(
        Guid Id,
        string Title,
        string Summary,
        string ContentPreview,
        PromptGalleryItemKind Kind,
        string Phase,
        PromptArtifactStatus Status,
        bool IsArchived,
        string? CollectionName,
        double? RecommendedTemperature,
        int? RecommendedMaxOutputTokens,
        double? RecommendedTopP,
        int CurrentVersionNumber,
        DateTimeOffset UpdatedAtUtc)
    {
        public const int PreviewLength = 280;
    }
}
