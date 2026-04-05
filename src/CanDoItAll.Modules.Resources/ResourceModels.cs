using System.Text.Json;
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

    public ResourceKind ResourceKind { get; set; }

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
    ResourceKind ResourceKind,
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

    public ResourceKind ResourceKind { get; set; } = ResourceKind.Repository;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ConnectorPluginKey { get; set; } = string.Empty;

    public string ConfigSchemaVersion { get; set; } = string.Empty;

    public string LocationOrIdentifier { get; set; } = string.Empty;

    public string ConfigJson { get; set; } = "{}";

    public Guid? LinkedSecretId { get; set; }

    public string RepositoryUrl { get; set; } = string.Empty;

    public string DefaultBranch { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string FolderPath { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public string WebUrl { get; set; } = string.Empty;

    public string UrlTitleHint { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public int? Port { get; set; }

    public string RemotePath { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string ScriptPath { get; set; } = string.Empty;

    public string ScriptArguments { get; set; } = string.Empty;

    public string ComposeFilePath { get; set; } = string.Empty;

    public string ComposeService { get; set; } = string.Empty;

    public string SecretPurpose { get; set; } = string.Empty;

    public string PromptReference { get; set; } = string.Empty;

    public string PromptTitleHint { get; set; } = string.Empty;

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

        var editor = new ResourceEditorModel
        {
            Id = resource.Id,
            ProjectId = resource.ProjectId,
            OwnerPartyId = resource.OwnerPartyId,
            MaintainerPartyId = resource.MaintainerPartyId,
            ResourceKind = resource.ResourceKind,
            Name = resource.Name,
            Description = resource.Description,
            ConnectorPluginKey = resource.ConnectorPluginKey,
            ConfigSchemaVersion = resource.ConfigSchemaVersion,
            LocationOrIdentifier = resource.LocationOrIdentifier,
            ConfigJson = resource.ConfigJson,
            LinkedSecretId = ParseLinkedSecret(resource.LinkedSecretIdsJson),
            ValidationStatus = resource.ValidationStatus,
            Sensitivity = resource.Sensitivity,
            SupportsPreview = resource.SupportsPreview,
            SupportsIndexing = resource.SupportsIndexing
        };

        resourceConnectorPluginRegistry.Resolve(resource).ApplyConfig(editor, resource.ConfigJson);
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

        var connectorPlugin = resourceConnectorPluginRegistry.Resolve(model.ResourceKind, model.ConnectorPluginKey);
        var configSchemaVersion = string.IsNullOrWhiteSpace(model.ConfigSchemaVersion)
            ? connectorPlugin.Manifest.ConfigurationSchema.Version
            : model.ConfigSchemaVersion.Trim();
        if (!string.Equals(configSchemaVersion, connectorPlugin.Manifest.ConfigurationSchema.Version, StringComparison.Ordinal))
        {
            return Result<Guid>.Failure(Error.Validation(
                $"Resource connector '{connectorPlugin.Manifest.PluginKey}' requires config schema version '{connectorPlugin.Manifest.ConfigurationSchema.Version}', but '{configSchemaVersion}' was supplied."));
        }

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
        entity.ResourceKind = model.ResourceKind;
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

    private static Error? ValidateTypedEditor(ResourceEditorModel model)
        => model.ResourceKind switch
        {
            ResourceKind.Repository when string.IsNullOrWhiteSpace(model.RepositoryUrl) => Error.Validation("Repository URL is required."),
            ResourceKind.Folder when string.IsNullOrWhiteSpace(model.FolderPath) => Error.Validation("Folder path is required."),
            ResourceKind.File when string.IsNullOrWhiteSpace(model.FilePath) => Error.Validation("File path is required."),
            ResourceKind.WebLink when string.IsNullOrWhiteSpace(model.WebUrl) => Error.Validation("URL is required."),
            ResourceKind.Ftp when string.IsNullOrWhiteSpace(model.Host) => Error.Validation("FTP host is required."),
            ResourceKind.Ssh when string.IsNullOrWhiteSpace(model.Host) => Error.Validation("SSH host is required."),
            ResourceKind.PowerShellScript when string.IsNullOrWhiteSpace(model.ScriptPath) => Error.Validation("Script path is required."),
            ResourceKind.DockerCompose when string.IsNullOrWhiteSpace(model.ComposeFilePath) => Error.Validation("Compose file path is required."),
            ResourceKind.SecretLink when string.IsNullOrWhiteSpace(model.SecretPurpose) => Error.Validation("Secret purpose is required."),
            ResourceKind.PromptLink when string.IsNullOrWhiteSpace(model.PromptReference) => Error.Validation("Prompt reference is required."),
            _ => null
        };

    private static string BuildLocation(ResourceEditorModel model) => model.ResourceKind switch
    {
        ResourceKind.Repository => model.RepositoryUrl.Trim(),
        ResourceKind.Folder => model.FolderPath.Trim(),
        ResourceKind.File => model.FilePath.Trim(),
        ResourceKind.WebLink => model.WebUrl.Trim(),
        ResourceKind.Ftp => BuildRemoteEndpoint(model.Host, model.Port, model.RemotePath),
        ResourceKind.Ssh => BuildRemoteEndpoint(model.Host, model.Port, model.WorkingDirectory),
        ResourceKind.PowerShellScript => model.ScriptPath.Trim(),
        ResourceKind.DockerCompose => model.ComposeFilePath.Trim(),
        ResourceKind.SecretLink => model.SecretPurpose.Trim(),
        ResourceKind.PromptLink => model.PromptReference.Trim(),
        _ => model.LocationOrIdentifier.Trim()
    };

    private static string SerializeConfig(ResourceEditorModel model)
        => model.ResourceKind switch
        {
            ResourceKind.Repository => JsonSerializer.Serialize(new RepositoryResourceConfig(model.RepositoryUrl, model.DefaultBranch, model.RelativePath)),
            ResourceKind.Folder => JsonSerializer.Serialize(new FolderResourceConfig(model.FolderPath, model.WorkingDirectory)),
            ResourceKind.File => JsonSerializer.Serialize(new FileResourceConfig(model.FilePath, model.WorkingDirectory)),
            ResourceKind.WebLink => JsonSerializer.Serialize(new WebLinkResourceConfig(model.WebUrl, model.UrlTitleHint)),
            ResourceKind.Ftp => JsonSerializer.Serialize(new FtpResourceConfig(model.Host, model.Port, model.RemotePath, model.UserName)),
            ResourceKind.Ssh => JsonSerializer.Serialize(new SshResourceConfig(model.Host, model.Port, model.UserName, model.WorkingDirectory)),
            ResourceKind.PowerShellScript => JsonSerializer.Serialize(new PowerShellScriptResourceConfig(model.ScriptPath, model.ScriptArguments, model.WorkingDirectory)),
            ResourceKind.DockerCompose => JsonSerializer.Serialize(new DockerComposeResourceConfig(model.ComposeFilePath, model.ComposeService)),
            ResourceKind.SecretLink => JsonSerializer.Serialize(new SecretLinkResourceConfig(model.SecretPurpose, string.Empty)),
            ResourceKind.PromptLink => JsonSerializer.Serialize(new PromptLinkResourceConfig(model.PromptReference, model.PromptTitleHint)),
            _ => string.IsNullOrWhiteSpace(model.ConfigJson) ? "{}" : model.ConfigJson
        };

    private static void ApplyTypedConfiguration(ResourceEditorModel model, ResourceKind kind, string configJson)
    {
        var json = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson;
        switch (kind)
        {
            case ResourceKind.Repository:
                if (JsonSerializer.Deserialize<RepositoryResourceConfig>(json) is { } repository)
                {
                    model.RepositoryUrl = repository.RepositoryUrl;
                    model.DefaultBranch = repository.DefaultBranch;
                    model.RelativePath = repository.RelativePath;
                }
                break;
            case ResourceKind.Folder:
                if (JsonSerializer.Deserialize<FolderResourceConfig>(json) is { } folder)
                {
                    model.FolderPath = folder.Path;
                    model.WorkingDirectory = folder.WorkingDirectory;
                }
                break;
            case ResourceKind.File:
                if (JsonSerializer.Deserialize<FileResourceConfig>(json) is { } file)
                {
                    model.FilePath = file.Path;
                    model.WorkingDirectory = file.WorkingDirectory;
                }
                break;
            case ResourceKind.WebLink:
                if (JsonSerializer.Deserialize<WebLinkResourceConfig>(json) is { } webLink)
                {
                    model.WebUrl = webLink.Url;
                    model.UrlTitleHint = webLink.TitleHint;
                }
                break;
            case ResourceKind.Ftp:
                if (JsonSerializer.Deserialize<FtpResourceConfig>(json) is { } ftp)
                {
                    model.Host = ftp.Host;
                    model.Port = ftp.Port;
                    model.RemotePath = ftp.RemotePath;
                    model.UserName = ftp.UserName;
                }
                break;
            case ResourceKind.Ssh:
                if (JsonSerializer.Deserialize<SshResourceConfig>(json) is { } ssh)
                {
                    model.Host = ssh.Host;
                    model.Port = ssh.Port;
                    model.UserName = ssh.UserName;
                    model.WorkingDirectory = ssh.WorkingDirectory;
                }
                break;
            case ResourceKind.PowerShellScript:
                if (JsonSerializer.Deserialize<PowerShellScriptResourceConfig>(json) is { } script)
                {
                    model.ScriptPath = script.ScriptPath;
                    model.ScriptArguments = script.Arguments;
                    model.WorkingDirectory = script.WorkingDirectory;
                }
                break;
            case ResourceKind.DockerCompose:
                if (JsonSerializer.Deserialize<DockerComposeResourceConfig>(json) is { } compose)
                {
                    model.ComposeFilePath = compose.ComposeFilePath;
                    model.ComposeService = compose.ServiceName;
                }
                break;
            case ResourceKind.SecretLink:
                if (JsonSerializer.Deserialize<SecretLinkResourceConfig>(json) is { } secret)
                {
                    model.SecretPurpose = secret.Purpose;
                }
                break;
            case ResourceKind.PromptLink:
                if (JsonSerializer.Deserialize<PromptLinkResourceConfig>(json) is { } prompt)
                {
                    model.PromptReference = prompt.PromptReference;
                    model.PromptTitleHint = prompt.PromptTitleHint;
                }
                break;
        }
    }

    private static string BuildRemoteEndpoint(string host, int? port, string path)
    {
        var hostPart = string.IsNullOrWhiteSpace(host) ? "remote" : host.Trim();
        var portPart = port.HasValue ? $":{port.Value}" : string.Empty;
        var pathPart = string.IsNullOrWhiteSpace(path) ? string.Empty : $"/{path.Trim().TrimStart('/')}";
        return $"{hostPart}{portPart}{pathPart}";
    }

    private static Guid? ParseLinkedSecret(string json)
    {
        var trimmed = json.Trim('[', ']', '"');
        return Guid.TryParse(trimmed, out var parsed) ? parsed : null;
    }
}


