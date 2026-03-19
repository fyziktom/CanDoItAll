using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Resources;

public enum ResourceKind
{
    Folder,
    File,
    WebLink,
    Ftp,
    PowerShellScript,
    Repository,
    DockerCompose,
    Ssh,
    SecretLink,
    PromptLink
}

public enum ResourceValidationStatus
{
    Unknown,
    Valid,
    Warning,
    Invalid
}

public enum ResourceSensitivity
{
    Normal,
    Sensitive,
    Restricted
}

public sealed class ProjectResource
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public ResourceKind ResourceKind { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string LocationOrIdentifier { get; set; } = string.Empty;

    public string ConfigJson { get; set; } = "{}";

    public string LinkedSecretIdsJson { get; set; } = "[]";

    public ResourceValidationStatus ValidationStatus { get; set; } = ResourceValidationStatus.Unknown;

    public ResourceSensitivity Sensitivity { get; set; } = ResourceSensitivity.Normal;

    public bool SupportsPreview { get; set; }

    public bool SupportsIndexing { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class ProjectResourceConfiguration : IEntityTypeConfiguration<ProjectResource>
{
    public void Configure(EntityTypeBuilder<ProjectResource> builder)
    {
        builder.ToTable("Resources_ProjectResources");
        builder.HasKey(resource => resource.Id);
        builder.Property(resource => resource.Name).HasMaxLength(200).IsRequired();
        builder.Property(resource => resource.Description).HasColumnType("TEXT");
        builder.Property(resource => resource.LocationOrIdentifier).HasMaxLength(1000).IsRequired();
        builder.Property(resource => resource.ConfigJson).HasColumnType("TEXT");
        builder.Property(resource => resource.LinkedSecretIdsJson).HasColumnType("TEXT");
    }
}

public sealed record ResourceSummary(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    ResourceKind ResourceKind,
    string Name,
    string LocationOrIdentifier,
    ResourceValidationStatus ValidationStatus,
    ResourceSensitivity Sensitivity);

public sealed class ResourceEditorModel
{
    public Guid? Id { get; set; }

    public Guid? ProjectId { get; set; }

    public ResourceKind ResourceKind { get; set; } = ResourceKind.Repository;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string LocationOrIdentifier { get; set; } = string.Empty;

    public string ConfigJson { get; set; } = "{}";

    public Guid? LinkedSecretId { get; set; }

    public ResourceValidationStatus ValidationStatus { get; set; } = ResourceValidationStatus.Unknown;

    public ResourceSensitivity Sensitivity { get; set; } = ResourceSensitivity.Normal;

    public bool SupportsPreview { get; set; }

    public bool SupportsIndexing { get; set; }
}

public sealed class ResourcesService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IActivityStream activityStream,
    ISearchIndexService searchIndexService)
{
    public async Task<IReadOnlyList<ResourceSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var projects = await dbContext.Set<Projects.Project>()
            .Select(project => new { project.Id, project.Name })
            .ToDictionaryAsync(project => project.Id, project => project.Name, cancellationToken);

        var resources = await dbContext.Set<ProjectResource>()
            .OrderBy(resource => resource.Name)
            .ToListAsync(cancellationToken);

        return resources.Select(resource => new ResourceSummary(
                resource.Id,
                resource.ProjectId,
                projects.GetValueOrDefault(resource.ProjectId, "Unknown project"),
                resource.ResourceKind,
                resource.Name,
                resource.LocationOrIdentifier,
                resource.ValidationStatus,
                resource.Sensitivity))
            .ToList();
    }

    public async Task<ResourceEditorModel> GetAsync(Guid? id, CancellationToken cancellationToken = default)
    {
        if (!id.HasValue)
        {
            return new ResourceEditorModel();
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var resource = await dbContext.Set<ProjectResource>().FirstOrDefaultAsync(item => item.Id == id.Value, cancellationToken);
        if (resource is null)
        {
            return new ResourceEditorModel();
        }

        return new ResourceEditorModel
        {
            Id = resource.Id,
            ProjectId = resource.ProjectId,
            ResourceKind = resource.ResourceKind,
            Name = resource.Name,
            Description = resource.Description,
            LocationOrIdentifier = resource.LocationOrIdentifier,
            ConfigJson = resource.ConfigJson,
            LinkedSecretId = ParseLinkedSecret(resource.LinkedSecretIdsJson),
            ValidationStatus = resource.ValidationStatus,
            Sensitivity = resource.Sensitivity,
            SupportsPreview = resource.SupportsPreview,
            SupportsIndexing = resource.SupportsIndexing
        };
    }

    public async Task<Result<Guid>> SaveAsync(ResourceEditorModel model, CancellationToken cancellationToken = default)
    {
        if (!model.ProjectId.HasValue)
        {
            return Result<Guid>.Failure(Error.Validation("Select a project before saving a resource."));
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Result<Guid>.Failure(Error.Validation("Resource name is required."));
        }

        if (string.IsNullOrWhiteSpace(model.LocationOrIdentifier))
        {
            return Result<Guid>.Failure(Error.Validation("Location or identifier is required."));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = model.Id.HasValue
            ? await dbContext.Set<ProjectResource>().FirstOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;

        if (entity is null)
        {
            entity = new ProjectResource
            {
                CreatedAtUtc = clock.GetUtcNow()
            };

            await dbContext.Set<ProjectResource>().AddAsync(entity, cancellationToken);
        }

        entity.ProjectId = model.ProjectId.Value;
        entity.ResourceKind = model.ResourceKind;
        entity.Name = model.Name.Trim();
        entity.Description = model.Description?.Trim() ?? string.Empty;
        entity.LocationOrIdentifier = model.LocationOrIdentifier.Trim();
        entity.ConfigJson = string.IsNullOrWhiteSpace(model.ConfigJson) ? "{}" : model.ConfigJson;
        entity.LinkedSecretIdsJson = model.LinkedSecretId.HasValue ? $"[\"{model.LinkedSecretId.Value}\"]" : "[]";
        entity.ValidationStatus = model.ValidationStatus;
        entity.Sensitivity = model.Sensitivity;
        entity.SupportsPreview = model.SupportsPreview;
        entity.SupportsIndexing = model.SupportsIndexing;
        entity.UpdatedAtUtc = clock.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);
        await searchIndexService.UpsertAsync(new SearchDocumentInput(
            "resource",
            entity.Id.ToString(),
            "Resources",
            entity.Name,
            entity.Description,
            $"{entity.LocationOrIdentifier}\nKind: {entity.ResourceKind}\nSensitivity: {entity.Sensitivity}\nValidation: {entity.ValidationStatus}",
            $"/resources?resourceId={entity.Id}",
            entity.ProjectId), cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "resources",
            model.Id.HasValue ? "update" : "create",
            $"{(model.Id.HasValue ? "Updated" : "Created")} resource",
            entity.Name,
            ProjectId: entity.ProjectId,
            ArtifactKind: "resource",
            ArtifactId: entity.Id,
            Route: $"/resources?resourceId={entity.Id}"), cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var resource = await dbContext.Set<ProjectResource>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (resource is null)
        {
            return;
        }

        dbContext.Remove(resource);
        await dbContext.SaveChangesAsync(cancellationToken);
        await searchIndexService.DeleteAsync("resource", id.ToString(), cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "resources",
            "delete",
            "Deleted resource",
            resource.Name,
            ProjectId: resource.ProjectId,
            ArtifactKind: "resource",
            ArtifactId: resource.Id,
            Route: "/resources"), cancellationToken);
    }

    private static Guid? ParseLinkedSecret(string json)
    {
        var trimmed = json.Trim('[', ']', '"');
        return Guid.TryParse(trimmed, out var parsed) ? parsed : null;
    }
}
