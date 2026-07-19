using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Prompts;

public enum PromptGallerySeedConflictCode
{
    ArtifactIdCollision,
    SourceKeyCollision,
    SourceChangedOrItemModified
}

public sealed record PromptGallerySeedConflict(
    string SourceKey,
    Guid SourceId,
    PromptGallerySeedConflictCode Code,
    string Message);

public sealed record PromptGallerySeedImportResult(
    int CatalogComponentCount,
    int CreatedCount,
    int ExistingCount,
    IReadOnlyList<PromptGallerySeedConflict> Conflicts)
{
    public bool Succeeded => Conflicts.Count == 0;
}

public sealed class PromptGallerySeedImporter(
    IDbContextFactory<AppDbContext> dbContextFactory,
    PromptGallerySeedLoader loader,
    IClock clock,
    IPromptGalleryProjectionCoordinator projectionCoordinator,
    ILogger<PromptGallerySeedImporter> logger)
{
    public async Task<PromptGallerySeedImportResult> ImportAsync(CancellationToken cancellationToken = default)
    {
        var pack = loader.Load();
        var sourceKeys = pack.Components
            .Select(component => component.Key)
            .ToArray();
        var sourceIds = pack.Components.Select(component => component.Id).ToArray();
        var createdCount = 0;
        var conflicts = new List<PromptGallerySeedConflict>();
        var existingCount = 0;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingBySourceKey = (await dbContext.Set<PromptArtifact>()
                .AsNoTracking()
                .Where(artifact =>
                    artifact.Provenance == PromptArtifactProvenance.PackagedComponentCatalog &&
                    artifact.SourceKey != null &&
                    sourceKeys.Contains(artifact.SourceKey))
                .ToListAsync(cancellationToken))
            .ToDictionary(artifact => artifact.SourceKey!, StringComparer.OrdinalIgnoreCase);
        var existingById = (await dbContext.Set<PromptArtifact>()
                .AsNoTracking()
                .Where(artifact => sourceIds.Contains(artifact.Id))
                .ToListAsync(cancellationToken))
            .ToDictionary(artifact => artifact.Id);
        var tagsByKey = await LoadTagsAsync(dbContext, pack, cancellationToken);
        var importedAtUtc = clock.GetUtcNow();

        foreach (var component in pack.Components)
        {
            var fingerprint = PromptGallerySeedFingerprint.Compute(component);
            if (existingBySourceKey.TryGetValue(component.Key, out var bySourceKey))
            {
                if (bySourceKey.Id != component.Id)
                {
                    conflicts.Add(new PromptGallerySeedConflict(
                        component.Key,
                        component.Id,
                        PromptGallerySeedConflictCode.SourceKeyCollision,
                        $"Source key '{component.Key}' belongs to Gallery item '{bySourceKey.Id}', not packaged ID '{component.Id}'."));
                }
                else if (!string.Equals(bySourceKey.SourceFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    conflicts.Add(new PromptGallerySeedConflict(
                        component.Key,
                        component.Id,
                        PromptGallerySeedConflictCode.SourceChangedOrItemModified,
                        $"Gallery item '{component.Id}' differs from the packaged source and was not overwritten."));
                }
                else
                {
                    existingCount += 1;
                }

                continue;
            }

            if (existingById.TryGetValue(component.Id, out var byId))
            {
                conflicts.Add(new PromptGallerySeedConflict(
                    component.Key,
                    component.Id,
                    PromptGallerySeedConflictCode.ArtifactIdCollision,
                    $"Packaged ID '{component.Id}' is already used by Gallery item '{byId.Title}' and was not overwritten."));
                continue;
            }

            var group = component.GroupMetadata
                ?? throw new InvalidDataException($"Seed component '{component.Key}' has no validated group metadata.");
            var artifact = new PromptArtifact
            {
                Id = component.Id,
                Title = component.Name,
                Summary = component.Summary,
                Kind = PromptGalleryItemKind.Part,
                Status = PromptArtifactStatus.Final,
                CurrentDraftText = component.Content,
                CurrentVersionNumber = 1,
                Provenance = PromptArtifactProvenance.PackagedComponentCatalog,
                SourceCatalog = PromptGallerySeedLoader.CatalogSource,
                SourceKey = component.Key,
                SourceGroupKey = group.Key,
                SourceGroupName = group.Name,
                SourceItemKind = component.BlockKind,
                SourceOrderIndex = component.OrderIndex,
                SourceFingerprint = fingerprint,
                CreatedAtUtc = importedAtUtc,
                UpdatedAtUtc = importedAtUtc
            };
            artifact.SearchText = PromptGalleryPersistence.BuildSearchText(artifact);
            await dbContext.Set<PromptArtifact>().AddAsync(artifact, cancellationToken);
            await dbContext.Set<PromptVersion>().AddAsync(new PromptVersion
            {
                PromptArtifactId = artifact.Id,
                VersionNumber = 1,
                Content = component.Content,
                CreationReason = "Imported from the packaged prompt component catalog.",
                OutputFormat = "Markdown",
                TitleSnapshot = component.Name,
                SummarySnapshot = component.Summary,
                KindSnapshot = PromptGalleryItemKind.Part,
                CreatedAtUtc = importedAtUtc
            }, cancellationToken);

            foreach (var tagName in ComponentTags(component))
            {
                var tag = tagsByKey[PromptGalleryPersistence.NormalizeRequiredKey(tagName)];
                await dbContext.Set<PromptArtifactTag>().AddAsync(new PromptArtifactTag
                {
                    PromptArtifactId = artifact.Id,
                    PromptTagId = tag.Id
                }, cancellationToken);
            }

            foreach (var token in component.TemplateTokens
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await dbContext.Set<PromptTemplateToken>().AddAsync(new PromptTemplateToken
                {
                    PromptArtifactId = artifact.Id,
                    Name = token,
                    NameKey = PromptGalleryPersistence.NormalizeRequiredKey(token)
                }, cancellationToken);
            }

            existingBySourceKey[component.Key] = artifact;
            existingById[component.Id] = artifact;
            createdCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (createdCount > 0)
        {
            try
            {
                await projectionCoordinator.RebuildAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Prompt Gallery projection rebuild failed after {CreatedCount} packaged items committed. Run a projection rebuild to repair the derivative index.",
                    createdCount);
            }
        }

        return new PromptGallerySeedImportResult(
            pack.Components.Count,
            createdCount,
            existingCount,
            conflicts);
    }

    private static async Task<Dictionary<string, PromptTag>> LoadTagsAsync(
        AppDbContext dbContext,
        PromptGallerySeedPack pack,
        CancellationToken cancellationToken)
    {
        var displayByKey = pack.Components
            .SelectMany(ComponentTags)
            .GroupBy(PromptGalleryPersistence.NormalizeRequiredKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var keys = displayByKey.Keys.ToArray();
        var existing = await dbContext.Set<PromptTag>()
            .Where(tag => keys.Contains(tag.NameKey))
            .ToListAsync(cancellationToken);
        var tagsByKey = existing.ToDictionary(
            tag => tag.NameKey,
            StringComparer.Ordinal);

        foreach (var (key, display) in displayByKey)
        {
            if (tagsByKey.ContainsKey(key))
            {
                continue;
            }

            var tag = new PromptTag
            {
                Name = display,
                NameKey = key
            };
            await dbContext.Set<PromptTag>().AddAsync(tag, cancellationToken);
            tagsByKey[key] = tag;
        }

        return tagsByKey;
    }

    private static IEnumerable<string> ComponentTags(PromptGalleryComponentSeed component)
        => component.Tags
            .Concat(component.StackTags)
            .Distinct(StringComparer.OrdinalIgnoreCase);
}

public static class PromptGallerySeedFingerprint
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Compute(PromptGalleryComponentSeed component)
    {
        ArgumentNullException.ThrowIfNull(component);
        var group = component.GroupMetadata
            ?? throw new ArgumentException("Component group metadata is required.", nameof(component));
        var canonical = new
        {
            component.Id,
            component.Key,
            component.Name,
            component.BlockKind,
            component.Summary,
            component.Content,
            component.IsRecommendedByDefault,
            component.PromptTypeRules,
            component.BlueprintRules,
            component.PhaseRules,
            SourceGroupKey = component.Group,
            Tags = component.Tags.Order(StringComparer.Ordinal).ToArray(),
            StackTags = component.StackTags.Order(StringComparer.Ordinal).ToArray(),
            component.ToolboxEligible,
            TemplateTokens = component.TemplateTokens.Order(StringComparer.Ordinal).ToArray(),
            component.OrderIndex,
            GroupMetadata = new
            {
                group.Key,
                group.Name,
                group.Summary,
                group.Purpose,
                group.UiMode,
                group.Order
            }
        };
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical, JsonOptions));
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}

public sealed class PromptGallerySeedImportHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<PromptGallerySeedImportHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var importer = scope.ServiceProvider.GetRequiredService<PromptGallerySeedImporter>();
        var result = await importer.ImportAsync(cancellationToken);
        if (result.Conflicts.Count > 0)
        {
            logger.LogWarning(
                "Prompt Gallery seed import created {CreatedCount} of {CatalogCount} items, retained {ExistingCount}, and reported {ConflictCount} non-overwrite conflicts: {ConflictKeys}",
                result.CreatedCount,
                result.CatalogComponentCount,
                result.ExistingCount,
                result.Conflicts.Count,
                string.Join(", ", result.Conflicts.Select(conflict => conflict.SourceKey).Take(20)));
            return;
        }

        logger.LogInformation(
            "Prompt Gallery seed import completed with {CreatedCount} created and {ExistingCount} existing items from {CatalogCount} packaged components.",
            result.CreatedCount,
            result.ExistingCount,
            result.CatalogComponentCount);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
