using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Prompts;

public enum PromptArtifactStatus
{
    Draft,
    Final
}

public sealed class PromptArtifact
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? ProjectId { get; set; }

    public Guid? CollectionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public PromptArtifactStatus Status { get; set; } = PromptArtifactStatus.Draft;

    public string CurrentDraftText { get; set; } = string.Empty;

    public int CurrentVersionNumber { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class PromptVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PromptArtifactId { get; set; }

    public int VersionNumber { get; set; }

    public string Content { get; set; } = string.Empty;

    public string CreationReason { get; set; } = string.Empty;

    public string OutputFormat { get; set; } = "Markdown";

    public string? SourceBlueprintId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class PromptCollection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public sealed class PromptTag
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
}

public sealed class PromptArtifactTag
{
    public Guid PromptArtifactId { get; set; }

    public Guid PromptTagId { get; set; }
}

public sealed class PromptUsageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PromptArtifactId { get; set; }

    public int? PromptVersionNumber { get; set; }

    public Guid? ProjectId { get; set; }

    public string Phase { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public string RepositoryName { get; set; } = string.Empty;

    public string BranchName { get; set; } = string.Empty;

    public string CommitSha { get; set; } = string.Empty;

    public string CommitUrl { get; set; } = string.Empty;

    public string UsageNote { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class PromptArtifactConfiguration : IEntityTypeConfiguration<PromptArtifact>
{
    public void Configure(EntityTypeBuilder<PromptArtifact> builder)
    {
        builder.ToTable("Prompts_PromptArtifacts");
        builder.HasKey(prompt => prompt.Id);
        builder.Property(prompt => prompt.Title).HasMaxLength(200).IsRequired();
        builder.Property(prompt => prompt.Phase).HasMaxLength(80);
        builder.Property(prompt => prompt.CurrentDraftText).HasColumnType("TEXT");
    }
}

internal sealed class PromptVersionConfiguration : IEntityTypeConfiguration<PromptVersion>
{
    public void Configure(EntityTypeBuilder<PromptVersion> builder)
    {
        builder.ToTable("Prompts_PromptVersions");
        builder.HasKey(version => version.Id);
        builder.Property(version => version.Content).HasColumnType("TEXT");
        builder.Property(version => version.CreationReason).HasMaxLength(200);
        builder.HasIndex(version => new { version.PromptArtifactId, version.VersionNumber }).IsUnique();
    }
}

internal sealed class PromptCollectionConfiguration : IEntityTypeConfiguration<PromptCollection>
{
    public void Configure(EntityTypeBuilder<PromptCollection> builder)
    {
        builder.ToTable("Prompts_PromptCollections");
        builder.HasKey(collection => collection.Id);
        builder.Property(collection => collection.Name).HasMaxLength(120).IsRequired();
        builder.Property(collection => collection.Description).HasColumnType("TEXT");
    }
}

internal sealed class PromptTagConfiguration : IEntityTypeConfiguration<PromptTag>
{
    public void Configure(EntityTypeBuilder<PromptTag> builder)
    {
        builder.ToTable("Prompts_PromptTags");
        builder.HasKey(tag => tag.Id);
        builder.Property(tag => tag.Name).HasMaxLength(120).IsRequired();
        builder.HasIndex(tag => tag.Name).IsUnique();
    }
}

internal sealed class PromptArtifactTagConfiguration : IEntityTypeConfiguration<PromptArtifactTag>
{
    public void Configure(EntityTypeBuilder<PromptArtifactTag> builder)
    {
        builder.ToTable("Prompts_PromptArtifactTags");
        builder.HasKey(item => new { item.PromptArtifactId, item.PromptTagId });
    }
}

internal sealed class PromptUsageRecordConfiguration : IEntityTypeConfiguration<PromptUsageRecord>
{
    public void Configure(EntityTypeBuilder<PromptUsageRecord> builder)
    {
        builder.ToTable("Prompts_PromptUsageRecords");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.ProviderName).HasMaxLength(120);
        builder.Property(record => record.RepositoryName).HasMaxLength(200);
        builder.Property(record => record.BranchName).HasMaxLength(120);
        builder.Property(record => record.CommitSha).HasMaxLength(80);
        builder.Property(record => record.CommitUrl).HasMaxLength(500);
        builder.Property(record => record.UsageNote).HasColumnType("TEXT");
    }
}

public sealed record PromptSummary(
    Guid Id,
    string Title,
    string Phase,
    PromptArtifactStatus Status,
    string? CollectionName,
    string Tags,
    int VersionCount,
    DateTimeOffset UpdatedAtUtc);

public sealed record PromptCollectionSummary(Guid Id, string Name, string Description);

public sealed record PromptVersionSummary(int VersionNumber, string CreationReason, DateTimeOffset CreatedAtUtc);

public sealed record PromptUsageSummary(string ProviderName, string RepositoryName, string CommitSha, string UsageNote, DateTimeOffset CreatedAtUtc);

public sealed class PromptEditorModel
{
    public Guid? Id { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? CollectionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Phase { get; set; } = string.Empty;

    public string DraftText { get; set; } = string.Empty;

    public string TagsCsv { get; set; } = string.Empty;

    public string FinalizationReason { get; set; } = "Ready for reuse";

    public List<PromptVersionSummary> Versions { get; set; } = [];

    public List<PromptUsageSummary> UsageHistory { get; set; } = [];
}

public sealed class PromptsService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    ISearchIndexService searchIndexService)
{
    public async Task<IReadOnlyList<PromptSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var collections = await dbContext.Set<PromptCollection>()
            .ToDictionaryAsync(collection => collection.Id, collection => collection.Name, cancellationToken);

        var tags = await LoadTagsByPromptAsync(dbContext, cancellationToken);
        var versions = await dbContext.Set<PromptVersion>()
            .GroupBy(item => item.PromptArtifactId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);

        var prompts = await dbContext.Set<PromptArtifact>().ToListAsync(cancellationToken);

        return prompts
            .OrderByDescending(prompt => prompt.UpdatedAtUtc)
            .Select(prompt => new PromptSummary(
                prompt.Id,
                prompt.Title,
                prompt.Phase,
                prompt.Status,
                prompt.CollectionId.HasValue ? collections.GetValueOrDefault(prompt.CollectionId.Value) : null,
                string.Join(", ", tags.GetValueOrDefault(prompt.Id, [])),
                versions.GetValueOrDefault(prompt.Id),
                prompt.UpdatedAtUtc))
            .ToList();
    }

    public async Task<IReadOnlyList<PromptCollectionSummary>> ListCollectionsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<PromptCollection>()
            .OrderBy(collection => collection.Name)
            .Select(collection => new PromptCollectionSummary(collection.Id, collection.Name, collection.Description))
            .ToListAsync(cancellationToken);
    }

    public async Task EnsureCollectionAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var exists = await dbContext.Set<PromptCollection>().AnyAsync(collection => collection.Name == name.Trim(), cancellationToken);
        if (exists)
        {
            return;
        }

        await dbContext.Set<PromptCollection>().AddAsync(new PromptCollection
        {
            Name = name.Trim(),
            Description = "Created from the prompt gallery."
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PromptEditorModel> GetAsync(Guid? id, CancellationToken cancellationToken = default)
    {
        if (!id.HasValue)
        {
            return new PromptEditorModel();
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var prompt = await dbContext.Set<PromptArtifact>().FirstOrDefaultAsync(item => item.Id == id.Value, cancellationToken);
        if (prompt is null)
        {
            return new PromptEditorModel();
        }

        var tags = await LoadTagsByPromptAsync(dbContext, cancellationToken);
        var versions = await dbContext.Set<PromptVersion>()
            .Where(item => item.PromptArtifactId == prompt.Id)
            .OrderByDescending(item => item.VersionNumber)
            .Select(item => new PromptVersionSummary(item.VersionNumber, item.CreationReason, item.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var usageRecords = await dbContext.Set<PromptUsageRecord>()
            .Where(item => item.PromptArtifactId == prompt.Id)
            .ToListAsync(cancellationToken);
        var usage = usageRecords
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(10)
            .Select(item => new PromptUsageSummary(item.ProviderName, item.RepositoryName, item.CommitSha, item.UsageNote, item.CreatedAtUtc))
            .ToList();

        return new PromptEditorModel
        {
            Id = prompt.Id,
            ProjectId = prompt.ProjectId,
            CollectionId = prompt.CollectionId,
            Title = prompt.Title,
            Phase = prompt.Phase,
            DraftText = prompt.CurrentDraftText,
            TagsCsv = string.Join(", ", tags.GetValueOrDefault(prompt.Id, [])),
            Versions = versions,
            UsageHistory = usage
        };
    }

    public async Task<Result<Guid>> SaveDraftAsync(PromptEditorModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            return Result<Guid>.Failure(Error.Validation("Prompt title is required."));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = model.Id.HasValue
            ? await dbContext.Set<PromptArtifact>().FirstOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;

        if (entity is null)
        {
            entity = new PromptArtifact
            {
                CreatedAtUtc = clock.GetUtcNow()
            };
            await dbContext.Set<PromptArtifact>().AddAsync(entity, cancellationToken);
        }

        entity.ProjectId = model.ProjectId;
        entity.CollectionId = model.CollectionId;
        entity.Title = model.Title.Trim();
        entity.Phase = model.Phase?.Trim() ?? string.Empty;
        entity.CurrentDraftText = model.DraftText ?? string.Empty;
        entity.Status = PromptArtifactStatus.Draft;
        entity.UpdatedAtUtc = clock.GetUtcNow();

        await SyncTagsAsync(dbContext, entity.Id, model.TagsCsv, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await searchIndexService.UpsertAsync(new SearchDocumentInput(
            "prompt",
            entity.Id.ToString(),
            "Prompts",
            entity.Title,
            entity.Phase,
            entity.CurrentDraftText,
            $"/prompt-gallery?promptId={entity.Id}",
            entity.ProjectId), cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "prompts",
            model.Id.HasValue ? "update-draft" : "create-draft",
            $"{(model.Id.HasValue ? "Updated" : "Created")} prompt draft",
            entity.Title,
            ProjectId: entity.ProjectId,
            ArtifactKind: "prompt",
            ArtifactId: entity.Id,
            Route: $"/prompt-gallery?promptId={entity.Id}"), cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task<Result<Guid>> FinalizeAsync(PromptEditorModel model, CancellationToken cancellationToken = default)
    {
        var saved = await SaveDraftAsync(model, cancellationToken);
        if (saved.IsFailure)
        {
            return saved;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<PromptArtifact>().FirstAsync(item => item.Id == saved.Value, cancellationToken);
        entity.Status = PromptArtifactStatus.Final;
        entity.CurrentVersionNumber += 1;
        entity.UpdatedAtUtc = clock.GetUtcNow();

        await dbContext.Set<PromptVersion>().AddAsync(new PromptVersion
        {
            PromptArtifactId = entity.Id,
            VersionNumber = entity.CurrentVersionNumber,
            Content = entity.CurrentDraftText,
            CreationReason = string.IsNullOrWhiteSpace(model.FinalizationReason) ? "Ready for reuse" : model.FinalizationReason.Trim(),
            CreatedAtUtc = clock.GetUtcNow()
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "prompts",
            "finalize",
            $"Finalized prompt version {entity.CurrentVersionNumber}",
            entity.Title,
            ProjectId: entity.ProjectId,
            ArtifactKind: "prompt",
            ArtifactId: entity.Id,
            Route: $"/prompt-gallery?promptId={entity.Id}"), cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task<Guid?> CloneAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var model = await GetAsync(id, cancellationToken);
        if (model.Id is null)
        {
            return null;
        }

        model.Id = null;
        model.Title = $"{model.Title} (Clone)";
        var result = await SaveDraftAsync(model, cancellationToken);
        if (result.IsSuccess)
        {
            await activityStream.RecordAsync(new ActivityWriteRequest(
                "prompts",
                "clone",
                "Cloned prompt draft",
                model.Title,
                ProjectId: model.ProjectId,
                ArtifactKind: "prompt",
                ArtifactId: result.Value,
                Route: $"/prompt-gallery?promptId={result.Value}"), cancellationToken);
        }
        return result.Value;
    }

    public async Task RecordUsageAsync(
        Guid promptArtifactId,
        int? promptVersionNumber,
        Guid? projectId,
        string phase,
        string providerName,
        string repositoryName,
        string branchName,
        string commitSha,
        string commitUrl,
        string usageNote,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Set<PromptUsageRecord>().AddAsync(new PromptUsageRecord
        {
            PromptArtifactId = promptArtifactId,
            PromptVersionNumber = promptVersionNumber,
            ProjectId = projectId,
            Phase = phase,
            ProviderName = providerName,
            RepositoryName = repositoryName,
            BranchName = branchName,
            CommitSha = commitSha,
            CommitUrl = commitUrl,
            UsageNote = usageNote,
            CreatedAtUtc = clock.GetUtcNow()
        }, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "prompts",
            "record-usage",
            "Recorded prompt usage",
            providerName,
            ProjectId: projectId,
            ArtifactKind: "prompt",
            ArtifactId: promptArtifactId,
            Route: $"/prompt-gallery?promptId={promptArtifactId}"), cancellationToken);
    }

    private static async Task<Dictionary<Guid, List<string>>> LoadTagsByPromptAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var tagLookup = await dbContext.Set<PromptTag>()
            .ToDictionaryAsync(tag => tag.Id, tag => tag.Name, cancellationToken);

        var links = await dbContext.Set<PromptArtifactTag>().ToListAsync(cancellationToken);
        return links
            .GroupBy(link => link.PromptArtifactId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(link => tagLookup.GetValueOrDefault(link.PromptTagId))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToList());
    }

    private static async Task SyncTagsAsync(AppDbContext dbContext, Guid promptId, string tagsCsv, CancellationToken cancellationToken)
    {
        var tagNames = tagsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var existingLinks = await dbContext.Set<PromptArtifactTag>()
            .Where(link => link.PromptArtifactId == promptId)
            .ToListAsync(cancellationToken);

        dbContext.RemoveRange(existingLinks);

        foreach (var tagName in tagNames)
        {
            var tag = await dbContext.Set<PromptTag>().FirstOrDefaultAsync(item => item.Name == tagName, cancellationToken);
            if (tag is null)
            {
                tag = new PromptTag
                {
                    Name = tagName
                };

                await dbContext.Set<PromptTag>().AddAsync(tag, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await dbContext.Set<PromptArtifactTag>().AddAsync(new PromptArtifactTag
            {
                PromptArtifactId = promptId,
                PromptTagId = tag.Id
            }, cancellationToken);
        }
    }
}


