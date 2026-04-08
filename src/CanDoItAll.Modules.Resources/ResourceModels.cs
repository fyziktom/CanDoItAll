using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Resources;

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

public sealed record ResourceDescriptor(
    ResourceKind Kind,
    string DisplayName,
    string PrimaryLabel,
    string Summary);

public sealed record RepositoryResourceConfig(string RepositoryUrl, string DefaultBranch, string RelativePath);

public sealed record FolderResourceConfig(string Path, string WorkingDirectory);

public sealed record FileResourceConfig(string Path, string WorkingDirectory);

public sealed record WebLinkResourceConfig(string Url, string TitleHint);

public sealed record FtpResourceConfig(string Host, int? Port, string RemotePath, string UserName);

public sealed record SshResourceConfig(string Host, int? Port, string UserName, string WorkingDirectory);

public sealed record PowerShellScriptResourceConfig(string ScriptPath, string Arguments, string WorkingDirectory);

public sealed record DockerComposeResourceConfig(string ComposeFilePath, string ServiceName);

public sealed record SecretLinkResourceConfig(string Purpose, string SecretNameHint);

public sealed record PromptLinkResourceConfig(string PromptReference, string PromptTitleHint);

public static class ResourceDescriptorRegistry
{
    public static IReadOnlyList<ResourceDescriptor> All { get; } =
    [
        new(ResourceKind.Repository, "Repository", "Repository URL", "Track a source repository with branch and path details."),
        new(ResourceKind.Folder, "Folder", "Folder path", "Register a working directory, mounted volume, or content root."),
        new(ResourceKind.File, "File", "File path", "Track a concrete file that the project depends on."),
        new(ResourceKind.WebLink, "Web link", "URL", "Register documentation, APIs, and browser-based resources."),
        new(ResourceKind.Ftp, "FTP", "Host", "Store FTP connection metadata while keeping secrets external."),
        new(ResourceKind.Ssh, "SSH", "Host", "Store SSH connection metadata and target working directory."),
        new(ResourceKind.PowerShellScript, "PowerShell script", "Script path", "Track automation scripts and expected arguments."),
        new(ResourceKind.DockerCompose, "Docker Compose", "Compose file", "Describe Compose or Docker-based local infrastructure."),
        new(ResourceKind.SecretLink, "Secret link", "Purpose", "Link a resource to an external secret reference."),
        new(ResourceKind.PromptLink, "Prompt link", "Prompt reference", "Connect a resource to a reusable prompt artifact.")
    ];

    public static ResourceDescriptor Get(ResourceKind kind) => All.First(item => item.Kind == kind);
}

public sealed class ProjectResource
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid? OwnerPartyId { get; set; }

    public Guid? MaintainerPartyId { get; set; }

    public ResourceKind? ResourceKind { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ConnectorPluginKey { get; set; } = string.Empty;

    public string ConfigSchemaVersion { get; set; } = string.Empty;

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
        builder.Property(resource => resource.ConnectorPluginKey).HasMaxLength(160).IsRequired();
        builder.Property(resource => resource.ConfigSchemaVersion).HasMaxLength(40).IsRequired();
        builder.Property(resource => resource.LocationOrIdentifier).HasMaxLength(1000).IsRequired();
        builder.Property(resource => resource.ConfigJson).HasColumnType("TEXT");
        builder.Property(resource => resource.LinkedSecretIdsJson).HasColumnType("TEXT");
    }
}

public sealed record ResourceSummary(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    ResourceKind? LegacyResourceKind,
    string ConnectorPluginKey,
    string ConnectorDisplayName,
    string Name,
    string LocationOrIdentifier,
    ResourceValidationStatus ValidationStatus,
    ResourceSensitivity Sensitivity);

public sealed class ResourceEditorModel
{
    public Guid? Id { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? OwnerPartyId { get; set; }

    public Guid? MaintainerPartyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ConnectorPluginKey { get; set; } = ResourceConnectorPluginKeys.Repository;

    public string ConfigSchemaVersion { get; set; } = string.Empty;

    public string LocationOrIdentifier { get; set; } = string.Empty;

    public string ConfigJson { get; set; } = "{}";

    public ConnectorConfigState Configuration { get; set; } = new();

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
    ISearchIndexService searchIndexService,
    ResourceConnectorPluginRegistry resourceConnectorPluginRegistry)
{
    public IReadOnlyList<ConnectorPluginManifest> ListConnectorManifests()
    {
        return resourceConnectorPluginRegistry.ListManifests();
    }

    public string BuildLocationPreview(ResourceEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        try
        {
            var connectorPlugin = resourceConnectorPluginRegistry.Resolve(ResolveRequestedConnectorPluginKey(model));
            return connectorPlugin.BuildLocation(model);
        }
        catch
        {
            return model.LocationOrIdentifier?.Trim() ?? string.Empty;
        }
    }

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
                resourceConnectorPluginRegistry.Resolve(resource).Manifest.PluginKey,
                resourceConnectorPluginRegistry.Resolve(resource).Manifest.DisplayName,
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

        var editor = new ResourceEditorModel
        {
            Id = resource.Id,
            ProjectId = resource.ProjectId,
            OwnerPartyId = resource.OwnerPartyId,
            MaintainerPartyId = resource.MaintainerPartyId,
            Name = resource.Name,
            Description = resource.Description,
            ConfigSchemaVersion = resource.ConfigSchemaVersion,
            LocationOrIdentifier = resource.LocationOrIdentifier,
            ConfigJson = resource.ConfigJson,
            Configuration = ConnectorConfigState.FromJson(resource.ConfigJson),
            LinkedSecretId = ParseLinkedSecret(resource.LinkedSecretIdsJson),
            ValidationStatus = resource.ValidationStatus,
            Sensitivity = resource.Sensitivity,
            SupportsPreview = resource.SupportsPreview,
            SupportsIndexing = resource.SupportsIndexing
        };

        var connectorPlugin = resourceConnectorPluginRegistry.Resolve(resource);
        editor.ConnectorPluginKey = connectorPlugin.Manifest.PluginKey;
        connectorPlugin.ApplyConfig(editor, resource.ConfigJson);
        return editor;
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

        var connectorPlugin = resourceConnectorPluginRegistry.Resolve(ResolveRequestedConnectorPluginKey(model));
        var configSchemaVersion = string.IsNullOrWhiteSpace(model.ConfigSchemaVersion)
            ? connectorPlugin.Manifest.ConfigurationSchema.Version
            : model.ConfigSchemaVersion.Trim();
        if (!string.Equals(configSchemaVersion, connectorPlugin.Manifest.ConfigurationSchema.Version, StringComparison.Ordinal))
        {
            return Result<Guid>.Failure(Error.Validation(
                $"Resource connector '{connectorPlugin.Manifest.PluginKey}' requires config schema version '{connectorPlugin.Manifest.ConfigurationSchema.Version}', but '{configSchemaVersion}' was supplied."));
        }

        HydrateLegacyConfigIfNeeded(model, connectorPlugin);

        var requiresSecret = connectorPlugin.Manifest.SecretRequirements.Any(requirement => requirement.IsRequired);
        if (requiresSecret && !model.LinkedSecretId.HasValue)
        {
            return Result<Guid>.Failure(Error.Validation(
                $"{connectorPlugin.Manifest.DisplayName} requires a linked secret reference."));
        }

        var validationError = connectorPlugin.ValidateEditor(model);
        if (validationError is not null)
        {
            return Result<Guid>.Failure(validationError);
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
        entity.OwnerPartyId = model.OwnerPartyId;
        entity.MaintainerPartyId = model.MaintainerPartyId;
        entity.ResourceKind = connectorPlugin.LegacyResourceKind;
        entity.Name = model.Name.Trim();
        entity.Description = model.Description?.Trim() ?? string.Empty;
        entity.ConnectorPluginKey = connectorPlugin.Manifest.PluginKey;
        entity.ConfigSchemaVersion = configSchemaVersion;
        entity.LocationOrIdentifier = connectorPlugin.BuildLocation(model);
        entity.ConfigJson = connectorPlugin.SerializeConfig(model);
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
            $"{entity.LocationOrIdentifier}\nConnector: {connectorPlugin.Manifest.DisplayName}\nSensitivity: {entity.Sensitivity}\nValidation: {entity.ValidationStatus}",
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

    private static string ResolveRequestedConnectorPluginKey(ResourceEditorModel model)
    {
        var connectorPluginKey = model.ConnectorPluginKey?.Trim();
        if (string.IsNullOrWhiteSpace(connectorPluginKey))
        {
            throw new InvalidOperationException("Select a connector plugin before building the resource configuration.");
        }
        return connectorPluginKey;
    }

    private static void HydrateLegacyConfigIfNeeded(ResourceEditorModel model, IResourceConnectorPlugin connectorPlugin)
    {
        if (string.IsNullOrWhiteSpace(model.ConfigJson) ||
            string.Equals(model.ConfigJson.Trim(), "{}", StringComparison.Ordinal))
        {
            return;
        }

        if (!string.Equals(connectorPlugin.Manifest.PluginKey, WebhookResourceConnectorPlugin.PluginKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(model.Configuration.GetText(ResourceConnectorFieldKeys.EndpointUrl)))
        {
            return;
        }

        connectorPlugin.ApplyConfig(model, model.ConfigJson);
    }
}


