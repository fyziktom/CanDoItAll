using System.Text.Json;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.Resources;

internal static class ResourceConnectorPluginKeys
{
    public const string Repository = "resource.repository";
    public const string Folder = "resource.folder";
    public const string File = "resource.file";
    public const string WebLink = "resource.web-link";
    public const string Ftp = "resource.ftp";
    public const string Ssh = "resource.ssh";
    public const string PowerShellScript = "resource.powershell-script";
    public const string DockerCompose = "resource.docker-compose";
    public const string SecretLink = "resource.secret-link";
    public const string PromptLink = "resource.prompt-link";
    public const string WebhookEndpoint = "resource.webhook-endpoint";
    public const string StorageObject = "resource.storage-object";
}

internal static class ResourceConnectorFieldKeys
{
    public const string RepositoryUrl = "repositoryUrl";
    public const string DefaultBranch = "defaultBranch";
    public const string RelativePath = "relativePath";
    public const string FolderPath = "folderPath";
    public const string FilePath = "filePath";
    public const string WorkingDirectory = "workingDirectory";
    public const string WebUrl = "webUrl";
    public const string UrlTitleHint = "urlTitleHint";
    public const string Host = "host";
    public const string Port = "port";
    public const string RemotePath = "remotePath";
    public const string UserName = "userName";
    public const string ScriptPath = "scriptPath";
    public const string ScriptArguments = "scriptArguments";
    public const string ComposeFilePath = "composeFilePath";
    public const string ComposeService = "composeService";
    public const string SecretPurpose = "secretPurpose";
    public const string PromptReference = "promptReference";
    public const string PromptTitleHint = "promptTitleHint";
    public const string EndpointUrl = "endpointUrl";
    public const string HealthPath = "healthPath";
    public const string HttpMethod = "method";
}

public interface IResourceConnectorPlugin : IConnectorPlugin
{
    ResourceKind? LegacyResourceKind { get; }

    Error? ValidateEditor(ResourceEditorModel model);

    string BuildLocation(ResourceEditorModel model);

    string SerializeConfig(ResourceEditorModel model);

    void ApplyConfig(ResourceEditorModel model, string configJson);

    ProjectObjectType ResolveWorkbenchObjectType(ProjectResource resource);

    string ResolveWorkbenchObjectSubtype(ProjectResource resource);
}

public sealed class ResourceConnectorPluginRegistry(IEnumerable<IResourceConnectorPlugin> extensions) : IConnectorManifestSource
{
    private readonly IReadOnlyDictionary<string, IResourceConnectorPlugin> pluginsByKey = CreatePluginMap(extensions);

    private readonly IReadOnlyDictionary<ResourceKind, IResourceConnectorPlugin> pluginsByLegacyKind = CreatePluginMap(extensions)
        .Values
        .Where(plugin => plugin.LegacyResourceKind.HasValue)
        .GroupBy(plugin => plugin.LegacyResourceKind!.Value)
        .ToDictionary(group => group.Key, group => group.Last());

    public IResourceConnectorPlugin Resolve(string? connectorPluginKey, ResourceKind? legacyResourceKind = null)
    {
        if (!string.IsNullOrWhiteSpace(connectorPluginKey))
        {
            if (pluginsByKey.TryGetValue(connectorPluginKey.Trim(), out var pluginByKey))
            {
                return pluginByKey;
            }

            throw new InvalidOperationException(
                $"No resource connector plugin is registered for plugin key '{connectorPluginKey}'.");
        }

        if (legacyResourceKind.HasValue &&
            pluginsByLegacyKind.TryGetValue(legacyResourceKind.Value, out var pluginByKind))
        {
            return pluginByKind;
        }

        throw new InvalidOperationException("No resource connector plugin is registered for the supplied resource editor.");
    }

    public IResourceConnectorPlugin Resolve(ProjectResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return Resolve(resource.ConnectorPluginKey, resource.ResourceKind);
    }

    public IReadOnlyList<ConnectorPluginManifest> ListManifests()
    {
        return pluginsByKey.Values
            .Select(plugin => plugin.Manifest)
            .OrderBy(manifest => manifest.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyDictionary<string, IResourceConnectorPlugin> CreatePluginMap(IEnumerable<IResourceConnectorPlugin> extensions)
    {
        return BuildLegacyPlugins()
            .Concat(extensions)
            .GroupBy(plugin => plugin.Manifest.PluginKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<IResourceConnectorPlugin> BuildLegacyPlugins()
    {
        return
        [
            Legacy(
                ResourceKind.Repository,
                ResourceConnectorPluginKeys.Repository,
                "Repository resource",
                ProjectObjectType.Repository,
                "Repository",
                "Repository URL is required.",
                LegacySchema(
                    Field(ResourceConnectorFieldKeys.RepositoryUrl, "Repository URL", ConnectorConfigFieldType.Url, true, "Source repository URL."),
                    Field(ResourceConnectorFieldKeys.DefaultBranch, "Default branch", ConnectorConfigFieldType.Text, false, "Default branch for checkouts."),
                    Field(ResourceConnectorFieldKeys.RelativePath, "Relative path", ConnectorConfigFieldType.Text, false, "Optional path inside the repository.")),
                model => GetText(model, ResourceConnectorFieldKeys.RepositoryUrl).Trim(),
                model => JsonSerializer.Serialize(new RepositoryResourceConfig(
                    GetText(model, ResourceConnectorFieldKeys.RepositoryUrl),
                    GetText(model, ResourceConnectorFieldKeys.DefaultBranch),
                    GetText(model, ResourceConnectorFieldKeys.RelativePath))),
                static (model, json) =>
                {
                    if (JsonSerializer.Deserialize<RepositoryResourceConfig>(NormalizeConfigJson(json)) is { } value)
                    {
                        SetText(model, ResourceConnectorFieldKeys.RepositoryUrl, value.RepositoryUrl);
                        SetText(model, ResourceConnectorFieldKeys.DefaultBranch, value.DefaultBranch);
                        SetText(model, ResourceConnectorFieldKeys.RelativePath, value.RelativePath);
                    }
                }),
            Legacy(
                ResourceKind.Folder,
                ResourceConnectorPluginKeys.Folder,
                "Folder resource",
                ProjectObjectType.Connector,
                "Folder",
                "Folder path is required.",
                LegacySchema(
                    Field(ResourceConnectorFieldKeys.FolderPath, "Folder path", ConnectorConfigFieldType.Text, true, "Absolute or project-relative folder path."),
                    Field(ResourceConnectorFieldKeys.WorkingDirectory, "Working directory", ConnectorConfigFieldType.Text, false, "Optional working directory.")),
                model => GetText(model, ResourceConnectorFieldKeys.FolderPath).Trim(),
                model => JsonSerializer.Serialize(new FolderResourceConfig(
                    GetText(model, ResourceConnectorFieldKeys.FolderPath),
                    GetText(model, ResourceConnectorFieldKeys.WorkingDirectory))),
                static (model, json) =>
                {
                    if (JsonSerializer.Deserialize<FolderResourceConfig>(NormalizeConfigJson(json)) is { } value)
                    {
                        SetText(model, ResourceConnectorFieldKeys.FolderPath, value.Path);
                        SetText(model, ResourceConnectorFieldKeys.WorkingDirectory, value.WorkingDirectory);
                    }
                }),
            Legacy(
                ResourceKind.File,
                ResourceConnectorPluginKeys.File,
                "File resource",
                ProjectObjectType.File,
                "File",
                "File path is required.",
                LegacySchema(
                    Field(ResourceConnectorFieldKeys.FilePath, "File path", ConnectorConfigFieldType.Text, true, "Absolute or project-relative file path."),
                    Field(ResourceConnectorFieldKeys.WorkingDirectory, "Working directory", ConnectorConfigFieldType.Text, false, "Optional working directory.")),
                model => GetText(model, ResourceConnectorFieldKeys.FilePath).Trim(),
                model => JsonSerializer.Serialize(new FileResourceConfig(
                    GetText(model, ResourceConnectorFieldKeys.FilePath),
                    GetText(model, ResourceConnectorFieldKeys.WorkingDirectory))),
                static (model, json) =>
                {
                    if (JsonSerializer.Deserialize<FileResourceConfig>(NormalizeConfigJson(json)) is { } value)
                    {
                        SetText(model, ResourceConnectorFieldKeys.FilePath, value.Path);
                        SetText(model, ResourceConnectorFieldKeys.WorkingDirectory, value.WorkingDirectory);
                    }
                }),
            Legacy(
                ResourceKind.WebLink,
                ResourceConnectorPluginKeys.WebLink,
                "Web link resource",
                ProjectObjectType.Link,
                "Web link",
                "URL is required.",
                LegacySchema(
                    Field(ResourceConnectorFieldKeys.WebUrl, "URL", ConnectorConfigFieldType.Url, true, "Absolute URL."),
                    Field(ResourceConnectorFieldKeys.UrlTitleHint, "Title hint", ConnectorConfigFieldType.Text, false, "Optional display title hint.")),
                model => GetText(model, ResourceConnectorFieldKeys.WebUrl).Trim(),
                model => JsonSerializer.Serialize(new WebLinkResourceConfig(
                    GetText(model, ResourceConnectorFieldKeys.WebUrl),
                    GetText(model, ResourceConnectorFieldKeys.UrlTitleHint))),
                static (model, json) =>
                {
                    if (JsonSerializer.Deserialize<WebLinkResourceConfig>(NormalizeConfigJson(json)) is { } value)
                    {
                        SetText(model, ResourceConnectorFieldKeys.WebUrl, value.Url);
                        SetText(model, ResourceConnectorFieldKeys.UrlTitleHint, value.TitleHint);
                    }
                },
                ConnectorManifestCapability.ProjectResource | ConnectorManifestCapability.WorkbenchProjection | ConnectorManifestCapability.AgentExposure),
            Legacy(
                ResourceKind.Ftp,
                ResourceConnectorPluginKeys.Ftp,
                "FTP resource",
                ProjectObjectType.Connector,
                "FTP",
                "FTP host is required.",
                LegacySchema(
                    Field(ResourceConnectorFieldKeys.Host, "Host", ConnectorConfigFieldType.Text, true, "FTP host."),
                    Field(ResourceConnectorFieldKeys.Port, "Port", ConnectorConfigFieldType.Number, false, "Optional FTP port."),
                    Field(ResourceConnectorFieldKeys.UserName, "User", ConnectorConfigFieldType.Text, false, "Optional user name."),
                    Field(ResourceConnectorFieldKeys.RemotePath, "Remote path", ConnectorConfigFieldType.Text, false, "Optional remote path.")),
                model => BuildRemoteEndpoint(
                    GetText(model, ResourceConnectorFieldKeys.Host),
                    GetNumber(model, ResourceConnectorFieldKeys.Port),
                    GetText(model, ResourceConnectorFieldKeys.RemotePath)),
                model => JsonSerializer.Serialize(new FtpResourceConfig(
                    GetText(model, ResourceConnectorFieldKeys.Host),
                    GetNumber(model, ResourceConnectorFieldKeys.Port),
                    GetText(model, ResourceConnectorFieldKeys.RemotePath),
                    GetText(model, ResourceConnectorFieldKeys.UserName))),
                static (model, json) =>
                {
                    if (JsonSerializer.Deserialize<FtpResourceConfig>(NormalizeConfigJson(json)) is { } value)
                    {
                        SetText(model, ResourceConnectorFieldKeys.Host, value.Host);
                        SetNumber(model, ResourceConnectorFieldKeys.Port, value.Port);
                        SetText(model, ResourceConnectorFieldKeys.RemotePath, value.RemotePath);
                        SetText(model, ResourceConnectorFieldKeys.UserName, value.UserName);
                    }
                }),
            Legacy(
                ResourceKind.Ssh,
                ResourceConnectorPluginKeys.Ssh,
                "SSH resource",
                ProjectObjectType.Connector,
                "SSH",
                "SSH host is required.",
                LegacySchema(
                    Field(ResourceConnectorFieldKeys.Host, "Host", ConnectorConfigFieldType.Text, true, "SSH host."),
                    Field(ResourceConnectorFieldKeys.Port, "Port", ConnectorConfigFieldType.Number, false, "Optional SSH port."),
                    Field(ResourceConnectorFieldKeys.UserName, "User", ConnectorConfigFieldType.Text, false, "Optional user name."),
                    Field(ResourceConnectorFieldKeys.WorkingDirectory, "Working directory", ConnectorConfigFieldType.Text, false, "Optional working directory.")),
                model => BuildRemoteEndpoint(
                    GetText(model, ResourceConnectorFieldKeys.Host),
                    GetNumber(model, ResourceConnectorFieldKeys.Port),
                    GetText(model, ResourceConnectorFieldKeys.WorkingDirectory)),
                model => JsonSerializer.Serialize(new SshResourceConfig(
                    GetText(model, ResourceConnectorFieldKeys.Host),
                    GetNumber(model, ResourceConnectorFieldKeys.Port),
                    GetText(model, ResourceConnectorFieldKeys.UserName),
                    GetText(model, ResourceConnectorFieldKeys.WorkingDirectory))),
                static (model, json) =>
                {
                    if (JsonSerializer.Deserialize<SshResourceConfig>(NormalizeConfigJson(json)) is { } value)
                    {
                        SetText(model, ResourceConnectorFieldKeys.Host, value.Host);
                        SetNumber(model, ResourceConnectorFieldKeys.Port, value.Port);
                        SetText(model, ResourceConnectorFieldKeys.UserName, value.UserName);
                        SetText(model, ResourceConnectorFieldKeys.WorkingDirectory, value.WorkingDirectory);
                    }
                }),
            Legacy(
                ResourceKind.PowerShellScript,
                ResourceConnectorPluginKeys.PowerShellScript,
                "PowerShell script resource",
                ProjectObjectType.Script,
                "PowerShell script",
                "Script path is required.",
                LegacySchema(
                    Field(ResourceConnectorFieldKeys.ScriptPath, "Script path", ConnectorConfigFieldType.Text, true, "Script file path."),
                    Field(ResourceConnectorFieldKeys.WorkingDirectory, "Working directory", ConnectorConfigFieldType.Text, false, "Optional working directory."),
                    Field(ResourceConnectorFieldKeys.ScriptArguments, "Arguments", ConnectorConfigFieldType.Text, false, "Optional script arguments.")),
                model => GetText(model, ResourceConnectorFieldKeys.ScriptPath).Trim(),
                model => JsonSerializer.Serialize(new PowerShellScriptResourceConfig(
                    GetText(model, ResourceConnectorFieldKeys.ScriptPath),
                    GetText(model, ResourceConnectorFieldKeys.ScriptArguments),
                    GetText(model, ResourceConnectorFieldKeys.WorkingDirectory))),
                static (model, json) =>
                {
                    if (JsonSerializer.Deserialize<PowerShellScriptResourceConfig>(NormalizeConfigJson(json)) is { } value)
                    {
                        SetText(model, ResourceConnectorFieldKeys.ScriptPath, value.ScriptPath);
                        SetText(model, ResourceConnectorFieldKeys.ScriptArguments, value.Arguments);
                        SetText(model, ResourceConnectorFieldKeys.WorkingDirectory, value.WorkingDirectory);
                    }
                },
                workbenchSubtype: "shell"),
            Legacy(
                ResourceKind.DockerCompose,
                ResourceConnectorPluginKeys.DockerCompose,
                "Docker Compose resource",
                ProjectObjectType.Connector,
                "Docker Compose",
                "Compose file path is required.",
                LegacySchema(
                    Field(ResourceConnectorFieldKeys.ComposeFilePath, "Compose file", ConnectorConfigFieldType.Text, true, "Compose file path."),
                    Field(ResourceConnectorFieldKeys.ComposeService, "Service name", ConnectorConfigFieldType.Text, false, "Optional compose service name.")),
                model => GetText(model, ResourceConnectorFieldKeys.ComposeFilePath).Trim(),
                model => JsonSerializer.Serialize(new DockerComposeResourceConfig(
                    GetText(model, ResourceConnectorFieldKeys.ComposeFilePath),
                    GetText(model, ResourceConnectorFieldKeys.ComposeService))),
                static (model, json) =>
                {
                    if (JsonSerializer.Deserialize<DockerComposeResourceConfig>(NormalizeConfigJson(json)) is { } value)
                    {
                        SetText(model, ResourceConnectorFieldKeys.ComposeFilePath, value.ComposeFilePath);
                        SetText(model, ResourceConnectorFieldKeys.ComposeService, value.ServiceName);
                    }
                }),
            Legacy(
                ResourceKind.SecretLink,
                ResourceConnectorPluginKeys.SecretLink,
                "Secret link resource",
                ProjectObjectType.SecretReference,
                "Secret",
                "Secret purpose is required.",
                LegacySchema(
                    Field(ResourceConnectorFieldKeys.SecretPurpose, "Secret purpose", ConnectorConfigFieldType.Text, true, "Purpose for the linked secret.")),
                model => GetText(model, ResourceConnectorFieldKeys.SecretPurpose).Trim(),
                model => JsonSerializer.Serialize(new SecretLinkResourceConfig(
                    GetText(model, ResourceConnectorFieldKeys.SecretPurpose),
                    string.Empty)),
                static (model, json) =>
                {
                    if (JsonSerializer.Deserialize<SecretLinkResourceConfig>(NormalizeConfigJson(json)) is { } value)
                    {
                        SetText(model, ResourceConnectorFieldKeys.SecretPurpose, value.Purpose);
                    }
                }),
            Legacy(
                ResourceKind.PromptLink,
                ResourceConnectorPluginKeys.PromptLink,
                "Prompt link resource",
                ProjectObjectType.Link,
                "Prompt link",
                "Prompt reference is required.",
                LegacySchema(
                    Field(ResourceConnectorFieldKeys.PromptReference, "Prompt reference", ConnectorConfigFieldType.Text, true, "Prompt reference or identifier."),
                    Field(ResourceConnectorFieldKeys.PromptTitleHint, "Prompt title hint", ConnectorConfigFieldType.Text, false, "Optional display title hint.")),
                model => GetText(model, ResourceConnectorFieldKeys.PromptReference).Trim(),
                model => JsonSerializer.Serialize(new PromptLinkResourceConfig(
                    GetText(model, ResourceConnectorFieldKeys.PromptReference),
                    GetText(model, ResourceConnectorFieldKeys.PromptTitleHint))),
                static (model, json) =>
                {
                    if (JsonSerializer.Deserialize<PromptLinkResourceConfig>(NormalizeConfigJson(json)) is { } value)
                    {
                        SetText(model, ResourceConnectorFieldKeys.PromptReference, value.PromptReference);
                        SetText(model, ResourceConnectorFieldKeys.PromptTitleHint, value.PromptTitleHint);
                    }
                },
                ConnectorManifestCapability.ProjectResource | ConnectorManifestCapability.WorkbenchProjection | ConnectorManifestCapability.AgentExposure)
        ];
    }

    private static IResourceConnectorPlugin Legacy(
        ResourceKind kind,
        string pluginKey,
        string displayName,
        ProjectObjectType workbenchType,
        string workbenchLabel,
        string validationMessage,
        ConnectorConfigurationSchema schema,
        Func<ResourceEditorModel, string> buildLocation,
        Func<ResourceEditorModel, string> serializeConfig,
        Action<ResourceEditorModel, string> applyConfig,
        ConnectorManifestCapability capabilities = ConnectorManifestCapability.ProjectResource | ConnectorManifestCapability.WorkbenchProjection,
        string workbenchSubtype = "")
    {
        return new LegacyResourceConnectorPlugin(
            kind,
            new ConnectorPluginManifest(
                pluginKey,
                displayName,
                "1.0.0",
                capabilities,
                schema,
                [],
                new ConnectorHealthCheckDescriptor("metadata-only", "The connector currently relies on stored metadata rather than active probing."),
                new ConnectorAgentExposure($"resource.read.{kind.ToString().ToLowerInvariant()}", capabilities.HasFlag(ConnectorManifestCapability.AgentExposure), true, $"Agent access to {displayName} is governed by the connector manifest."),
                new ConnectorWorkbenchNodeHook(workbenchType, workbenchSubtype, workbenchLabel)),
            model => string.IsNullOrWhiteSpace(buildLocation(model))
                ? Error.Validation(validationMessage)
                : null,
            buildLocation,
            serializeConfig,
            applyConfig);
    }

    private static ConnectorConfigurationSchema LegacySchema(params ConnectorConfigFieldDescriptor[] fields)
    {
        return new ConnectorConfigurationSchema("1.0", fields);
    }

    private static ConnectorConfigFieldDescriptor Field(
        string key,
        string label,
        ConfigurationFieldType fieldType,
        bool isRequired,
        string helpText)
    {
        return new ConnectorConfigFieldDescriptor(key, label, fieldType, isRequired, helpText);
    }

    private static string NormalizeConfigJson(string configJson)
    {
        return string.IsNullOrWhiteSpace(configJson)
            ? "{}"
            : configJson;
    }

    private static string GetText(ResourceEditorModel model, string key)
    {
        return model.Configuration.GetText(key);
    }

    private static int? GetNumber(ResourceEditorModel model, string key)
    {
        return model.Configuration.GetNumber(key);
    }

    private static void SetText(ResourceEditorModel model, string key, string? value)
    {
        model.Configuration.SetText(key, value);
    }

    private static void SetNumber(ResourceEditorModel model, string key, int? value)
    {
        model.Configuration.SetNumber(key, value);
    }

    private static string BuildRemoteEndpoint(string host, int? port, string path)
    {
        var hostPart = string.IsNullOrWhiteSpace(host) ? "remote" : host.Trim();
        var portPart = port.HasValue ? $":{port.Value}" : string.Empty;
        var pathPart = string.IsNullOrWhiteSpace(path) ? string.Empty : $"/{path.Trim().TrimStart('/')}";
        return $"{hostPart}{portPart}{pathPart}";
    }
}

internal sealed class LegacyResourceConnectorPlugin(
    ResourceKind legacyResourceKind,
    ConnectorPluginManifest manifest,
    Func<ResourceEditorModel, Error?> validateEditor,
    Func<ResourceEditorModel, string> buildLocation,
    Func<ResourceEditorModel, string> serializeConfig,
    Action<ResourceEditorModel, string> applyConfig) : IResourceConnectorPlugin
{
    public ResourceKind? LegacyResourceKind => legacyResourceKind;

    public ConnectorPluginManifest Manifest => manifest;

    public Error? ValidateEditor(ResourceEditorModel model) => validateEditor(model);

    public string BuildLocation(ResourceEditorModel model) => buildLocation(model);

    public string SerializeConfig(ResourceEditorModel model) => serializeConfig(model);

    public void ApplyConfig(ResourceEditorModel model, string configJson) => applyConfig(model, configJson);

    public ProjectObjectType ResolveWorkbenchObjectType(ProjectResource resource) => manifest.WorkbenchNodeHook?.ObjectType ?? ProjectObjectType.Connector;

    public string ResolveWorkbenchObjectSubtype(ProjectResource resource) => manifest.WorkbenchNodeHook?.ObjectSubtype ?? string.Empty;
}

public sealed class WebhookResourceConnectorPlugin : IResourceConnectorPlugin
{
    public const string PluginKey = ResourceConnectorPluginKeys.WebhookEndpoint;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly ConnectorPluginManifest PluginManifest = new(
        PluginKey,
        "Webhook endpoint connector",
        "1.0.0",
        ConnectorManifestCapability.ProjectResource | ConnectorManifestCapability.WorkbenchProjection | ConnectorManifestCapability.AgentExposure,
        new ConnectorConfigurationSchema(
            "1.0",
            [
                new ConnectorConfigFieldDescriptor(ResourceConnectorFieldKeys.EndpointUrl, "Endpoint URL", ConnectorConfigFieldType.Url, true, "Absolute webhook endpoint URL."),
                new ConnectorConfigFieldDescriptor(ResourceConnectorFieldKeys.HealthPath, "Health path", ConnectorConfigFieldType.Text, false, "Optional relative path used for lightweight health probes."),
                new ConnectorConfigFieldDescriptor(ResourceConnectorFieldKeys.HttpMethod, "Method", ConnectorConfigFieldType.Text, true, "HTTP verb used when invoking the connector.")
            ]),
        [new ConnectorSecretRequirement("authorization", "Authorization secret", false, "Optional secret used for outbound authorization.")],
        new ConnectorHealthCheckDescriptor("HTTP GET", "Performs a lightweight request against the configured webhook endpoint."),
        new ConnectorAgentExposure("connector.invoke.webhook", false, true, "Webhook calls require explicit approval."),
        new ConnectorWorkbenchNodeHook(ProjectObjectType.Connector, "webhook-endpoint", "Webhook connector"));

    public ResourceKind? LegacyResourceKind => null;

    public ConnectorPluginManifest Manifest => PluginManifest;

    public Error? ValidateEditor(ResourceEditorModel model)
    {
        var endpointUrl = model.Configuration.GetText(ResourceConnectorFieldKeys.EndpointUrl);
        if (string.IsNullOrWhiteSpace(endpointUrl))
        {
            return Error.Validation("Webhook connector config must contain endpointUrl.");
        }

        return Uri.TryCreate(endpointUrl, UriKind.Absolute, out _)
            ? null
            : Error.Validation("Webhook connector endpointUrl must be an absolute URL.");
    }

    public string BuildLocation(ResourceEditorModel model)
    {
        return model.Configuration.GetText(ResourceConnectorFieldKeys.EndpointUrl).Trim();
    }

    public string SerializeConfig(ResourceEditorModel model)
    {
        return JsonSerializer.Serialize(
            new WebhookResourceConfig(
                model.Configuration.GetText(ResourceConnectorFieldKeys.EndpointUrl).Trim(),
                model.Configuration.GetText(ResourceConnectorFieldKeys.HealthPath).Trim(),
                NormalizeHttpMethod(model.Configuration.GetText(ResourceConnectorFieldKeys.HttpMethod))),
            JsonOptions);
    }

    public void ApplyConfig(ResourceEditorModel model, string configJson)
    {
        model.ConfigJson = NormalizeConfigJson(configJson);
        if (JsonSerializer.Deserialize<WebhookResourceConfig>(model.ConfigJson, JsonOptions) is not { } config)
        {
            return;
        }

        model.Configuration.SetText(ResourceConnectorFieldKeys.EndpointUrl, config.EndpointUrl);
        model.Configuration.SetText(ResourceConnectorFieldKeys.HealthPath, config.HealthPath);
        model.Configuration.SetText(ResourceConnectorFieldKeys.HttpMethod, NormalizeHttpMethod(config.Method));
    }

    public ProjectObjectType ResolveWorkbenchObjectType(ProjectResource resource) => ProjectObjectType.Connector;

    public string ResolveWorkbenchObjectSubtype(ProjectResource resource) => "webhook-endpoint";

    private static string NormalizeConfigJson(string configJson)
    {
        return string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson;
    }

    private static string NormalizeHttpMethod(string? method)
    {
        return string.IsNullOrWhiteSpace(method)
            ? "POST"
            : method.Trim().ToUpperInvariant();
    }

    private sealed record WebhookResourceConfig(string EndpointUrl, string HealthPath, string Method);
}
