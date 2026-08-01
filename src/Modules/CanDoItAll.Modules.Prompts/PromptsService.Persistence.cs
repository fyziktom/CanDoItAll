using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Prompts;

internal static class PromptGalleryPersistence
{
    internal static IReadOnlyList<Error> ValidateDraft(PromptGalleryDraft draft)
    {
        var errors = new List<Error>();
        if (draft.Id == Guid.Empty)
        {
            errors.Add(Error.Validation("Prompt ID cannot be empty.", "prompts.gallery.id-invalid"));
        }

        if (draft.Id.HasValue && !draft.ExpectedUpdatedAtUtc.HasValue)
        {
            errors.Add(Error.Validation(
                "ExpectedUpdatedAtUtc is required when updating a Prompt Gallery item.",
                "prompts.gallery.expected-updated-at-required"));
        }

        if (string.IsNullOrWhiteSpace(draft.Title) || draft.Title.Trim().Length > 200)
        {
            errors.Add(Error.Validation(
                "Prompt title must contain 1 to 200 characters.",
                "prompts.gallery.title-invalid"));
        }

        if (draft.Summary is null || draft.Summary.Length > 10_000)
        {
            errors.Add(Error.Validation(
                "Prompt summary is required and cannot exceed 10,000 characters.",
                "prompts.gallery.summary-too-long"));
        }

        if (draft.Phase is null || draft.Phase.Trim().Length > 80)
        {
            errors.Add(Error.Validation(
                "Prompt phase is required and cannot exceed 80 characters.",
                "prompts.gallery.phase-too-long"));
        }

        if (draft.Content is null)
        {
            errors.Add(Error.Validation(
                "Prompt content cannot be null.",
                "prompts.gallery.content-invalid"));
        }
        else if (draft.Content.Length > PromptGalleryLimits.MaximumContentLength)
        {
            errors.Add(Error.Validation(
                $"Prompt content cannot exceed {PromptGalleryLimits.MaximumContentLength:N0} characters.",
                "prompts.gallery.content-too-long"));
        }

        if (!Enum.IsDefined(draft.Kind))
        {
            errors.Add(Error.Validation("Prompt item kind is invalid.", "prompts.gallery.kind-invalid"));
        }

        if (draft.Tags is { Count: > 50 } ||
            draft.Tags?.Any(tag => string.IsNullOrWhiteSpace(tag) || tag.Trim().Length > 120) == true)
        {
            errors.Add(Error.Validation(
                "Prompts support at most 50 tags, each containing 1 to 120 characters.",
                "prompts.gallery.tags-invalid"));
        }

        if (draft.SupportedModels is { Count: > 100 } ||
            draft.SupportedModels?.Any(model =>
                model is null ||
                string.IsNullOrWhiteSpace(model.Provider) ||
                model.Provider.Trim().Length > 120 ||
                string.IsNullOrWhiteSpace(model.Model) ||
                model.Model.Trim().Length > 200) == true)
        {
            errors.Add(Error.Validation(
                "Supported provider/model entries must contain valid provider and model names.",
                "prompts.gallery.models-invalid"));
        }

        if (draft.SupportedModels is not null &&
            draft.SupportedModels.All(model => model is not null) &&
            draft.SupportedModels
                .Select(model => $"{NormalizeRequiredKey(model.Provider)}\u001f{NormalizeRequiredKey(model.Model)}")
                .Distinct(StringComparer.Ordinal)
                .Count() != draft.SupportedModels.Count)
        {
            errors.Add(Error.Validation(
                "Supported provider/model entries must be unique.",
                "prompts.gallery.models-duplicate"));
        }

        if (draft.SupportedModels?.Count(model => model.IsPreferred) > 1)
        {
            errors.Add(Error.Validation(
                "Only one supported provider/model entry can be preferred.",
                "prompts.gallery.models-preferred-duplicate"));
        }

        if (draft.SupportedConsumers?.Any(consumer => !Enum.IsDefined(consumer)) == true)
        {
            errors.Add(Error.Validation(
                "One or more supported consumers are invalid.",
                "prompts.gallery.consumers-invalid"));
        }

        if (draft.Recommendations?.Temperature is < 0 or > 2)
        {
            errors.Add(Error.Validation(
                "Recommended temperature must be between 0 and 2.",
                "prompts.gallery.temperature-invalid"));
        }

        if (draft.Recommendations?.MaxOutputTokens is < 1 or > 1_000_000)
        {
            errors.Add(Error.Validation(
                "Recommended max output tokens must be between 1 and 1,000,000.",
                "prompts.gallery.max-output-tokens-invalid"));
        }

        if (draft.Recommendations?.TopP is < 0 or > 1)
        {
            errors.Add(Error.Validation(
                "Recommended top-p must be between 0 and 1.",
                "prompts.gallery.top-p-invalid"));
        }

        return errors;
    }

    internal static async Task<PromptGalleryItemDetails?> LoadItemDetailsAsync(
        AppDbContext dbContext,
        Guid promptArtifactId,
        CancellationToken cancellationToken)
    {
        var artifact = await dbContext.Set<PromptArtifact>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == promptArtifactId, cancellationToken);
        if (artifact is null)
        {
            return null;
        }

        var tags = await (
                from link in dbContext.Set<PromptArtifactTag>().AsNoTracking()
                join tag in dbContext.Set<PromptTag>().AsNoTracking() on link.PromptTagId equals tag.Id
                where link.PromptArtifactId == promptArtifactId
                orderby tag.Name
                select tag.Name)
            .ToListAsync(cancellationToken);
        var templateTokens = await dbContext.Set<PromptTemplateToken>()
            .AsNoTracking()
            .Where(item => item.PromptArtifactId == promptArtifactId)
            .OrderBy(item => item.Name)
            .Select(item => item.Name)
            .ToListAsync(cancellationToken);
        var supportedModels = await dbContext.Set<PromptSupportedProviderModel>()
            .AsNoTracking()
            .Where(item => item.PromptArtifactId == promptArtifactId)
            .OrderByDescending(item => item.IsPreferred)
            .ThenBy(item => item.Provider)
            .ThenBy(item => item.Model)
            .Select(item => new PromptProviderModel(item.Provider, item.Model, item.IsPreferred))
            .ToListAsync(cancellationToken);
        var supportedConsumers = await dbContext.Set<PromptSupportedConsumer>()
            .AsNoTracking()
            .Where(item => item.PromptArtifactId == promptArtifactId)
            .OrderBy(item => item.Consumer)
            .Select(item => item.Consumer)
            .ToListAsync(cancellationToken);
        var warningSuppressions = await dbContext.Set<PromptCompatibilityWarningPreference>()
            .AsNoTracking()
            .Where(item => item.PromptArtifactId == promptArtifactId && item.IsSuppressed)
            .OrderBy(item => item.Consumer)
            .ThenBy(item => item.IssueCode)
            .Select(item => new PromptWarningSuppression(item.Consumer, item.IssueCode))
            .ToListAsync(cancellationToken);
        var versions = await dbContext.Set<PromptVersion>()
            .AsNoTracking()
            .Where(item => item.PromptArtifactId == promptArtifactId)
            .OrderByDescending(item => item.VersionNumber)
            .Select(item => new PromptGalleryVersionInfo(
                item.Id,
                item.VersionNumber,
                item.CreationReason,
                item.OutputFormat,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PromptGalleryItemDetails(
            artifact.Id,
            artifact.ProjectId,
            artifact.CollectionId,
            artifact.Title,
            artifact.Summary,
            artifact.Kind,
            artifact.Phase,
            artifact.Status,
            artifact.IsArchived,
            artifact.CurrentDraftText,
            artifact.CurrentVersionNumber,
            tags,
            templateTokens,
            supportedModels,
            supportedConsumers,
            warningSuppressions,
            new PromptModelRecommendations(
                artifact.RecommendedTemperature,
                artifact.RecommendedMaxOutputTokens,
                artifact.RecommendedTopP),
            new PromptGallerySourceInfo(
                artifact.Provenance,
                artifact.SourceCatalog,
                artifact.SourceKey,
                artifact.SourceGroupKey,
                artifact.SourceGroupName,
                artifact.SourceItemKind,
                artifact.SourceOrderIndex),
            versions,
            artifact.CreatedAtUtc,
            artifact.UpdatedAtUtc,
            artifact.IsFavorite);
    }

    internal static async Task SyncTagsAsync(
        AppDbContext dbContext,
        Guid promptArtifactId,
        IReadOnlyList<string> tags,
        bool isNewArtifact,
        CancellationToken cancellationToken)
    {
        var normalized = tags
            .Select(tag => new NormalizedValue(tag.Trim(), NormalizeRequiredKey(tag)))
            .DistinctBy(item => item.Key, StringComparer.Ordinal)
            .ToList();
        var desiredKeys = normalized
            .Select(item => item.Key)
            .ToHashSet(StringComparer.Ordinal);
        var existingLinks = isNewArtifact
            ? []
            : await (
                    from link in dbContext.Set<PromptArtifactTag>()
                    join tag in dbContext.Set<PromptTag>() on link.PromptTagId equals tag.Id
                    where link.PromptArtifactId == promptArtifactId
                    select new ExistingTagLink(link, tag.NameKey))
                .ToListAsync(cancellationToken);
        dbContext.RemoveRange(existingLinks
            .Where(item => !desiredKeys.Contains(item.NameKey))
            .Select(item => item.Link));

        var existingKeys = existingLinks
            .Where(item => desiredKeys.Contains(item.NameKey))
            .Select(item => item.NameKey)
            .ToHashSet(StringComparer.Ordinal);
        var missing = normalized
            .Where(item => !existingKeys.Contains(item.Key))
            .ToList();
        if (missing.Count == 0)
        {
            return;
        }

        var keys = missing.Select(item => item.Key).ToArray();
        var existingTags = await dbContext.Set<PromptTag>()
            .Where(tag => keys.Contains(tag.NameKey))
            .ToListAsync(cancellationToken);
        var tagsByKey = existingTags.ToDictionary(tag => tag.NameKey, StringComparer.Ordinal);

        foreach (var value in missing)
        {
            if (!tagsByKey.TryGetValue(value.Key, out var tag))
            {
                tag = new PromptTag
                {
                    Name = value.Display,
                    NameKey = value.Key
                };
                await dbContext.Set<PromptTag>().AddAsync(tag, cancellationToken);
                tagsByKey[value.Key] = tag;
            }

            await dbContext.Set<PromptArtifactTag>().AddAsync(new PromptArtifactTag
            {
                PromptArtifactId = promptArtifactId,
                PromptTagId = tag.Id
            }, cancellationToken);
        }
    }

    internal static async Task SyncSupportedModelsAsync(
        AppDbContext dbContext,
        Guid promptArtifactId,
        IReadOnlyList<PromptProviderModel> supportedModels,
        bool isNewArtifact,
        CancellationToken cancellationToken)
    {
        var desired = supportedModels
            .Select(item => new NormalizedProviderModel(
                item.Provider.Trim(),
                item.Model.Trim(),
                NormalizeRequiredKey(item.Provider),
                NormalizeRequiredKey(item.Model),
                item.IsPreferred))
            .ToDictionary(item => (item.ProviderKey, item.ModelKey));
        var existing = isNewArtifact
            ? []
            : await dbContext.Set<PromptSupportedProviderModel>()
                .Where(item => item.PromptArtifactId == promptArtifactId)
                .ToListAsync(cancellationToken);
        foreach (var current in existing)
        {
            if (!desired.TryGetValue((current.ProviderKey, current.ModelKey), out var wanted))
            {
                dbContext.Remove(current);
                continue;
            }

            current.Provider = wanted.Provider;
            current.Model = wanted.Model;
            current.IsPreferred = wanted.IsPreferred;
            desired.Remove((current.ProviderKey, current.ModelKey));
        }

        foreach (var supportedModel in desired.Values)
        {
            await dbContext.Set<PromptSupportedProviderModel>().AddAsync(new PromptSupportedProviderModel
            {
                PromptArtifactId = promptArtifactId,
                Provider = supportedModel.Provider,
                Model = supportedModel.Model,
                ProviderKey = supportedModel.ProviderKey,
                ModelKey = supportedModel.ModelKey,
                IsPreferred = supportedModel.IsPreferred
            }, cancellationToken);
        }
    }

    internal static async Task SyncSupportedConsumersAsync(
        AppDbContext dbContext,
        Guid promptArtifactId,
        IReadOnlyList<PromptGalleryConsumer> supportedConsumers,
        bool isNewArtifact,
        CancellationToken cancellationToken)
    {
        var desired = supportedConsumers.ToHashSet();
        var existing = isNewArtifact
            ? []
            : await dbContext.Set<PromptSupportedConsumer>()
                .Where(item => item.PromptArtifactId == promptArtifactId)
                .ToListAsync(cancellationToken);
        foreach (var current in existing)
        {
            if (desired.Remove(current.Consumer))
            {
                continue;
            }

            dbContext.Remove(current);
        }

        foreach (var consumer in desired)
        {
            await dbContext.Set<PromptSupportedConsumer>().AddAsync(new PromptSupportedConsumer
            {
                PromptArtifactId = promptArtifactId,
                Consumer = consumer
            }, cancellationToken);
        }
    }

    internal static string NormalizeRequiredKey(string value)
        => value.Trim().ToUpperInvariant();

    internal static string BuildSearchText(PromptArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        return string.Join(
                '\n',
                artifact.Title,
                artifact.Summary,
                artifact.Phase,
                artifact.CurrentDraftText,
                artifact.SourceKey,
                artifact.SourceCatalog,
                artifact.SourceGroupKey,
                artifact.SourceGroupName,
                artifact.SourceItemKind)
            .ToUpperInvariant();
    }

    private sealed record NormalizedValue(string Display, string Key);

    private sealed record ExistingTagLink(PromptArtifactTag Link, string NameKey);

    private sealed record NormalizedProviderModel(
        string Provider,
        string Model,
        string ProviderKey,
        string ModelKey,
        bool IsPreferred);
}
