using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel.Configuration;

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
    ResourceSensitivity Sensitivity) {
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid? ProjectLifetimeId { get; init; }
}

public sealed class ResourceEditorModel
{
    public ResourceEditorModel Capture() {
        var copy = (ResourceEditorModel)MemberwiseClone();
        copy.Configuration = Configuration.Clone();
        return copy;
    }

    public Guid? Id { get; set; }

    public Guid? ProjectId { get; set; }

    public ProjectWriteAdmission? ExpectedProjectAdmission { get; set; }

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
