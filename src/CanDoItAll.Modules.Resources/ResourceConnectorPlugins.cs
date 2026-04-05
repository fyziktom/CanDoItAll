using System.Text.Json;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Resources;

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

    public IResourceConnectorPlugin Resolve(ResourceKind resourceKind, string? connectorPluginKey)
    {
        if (!string.IsNullOrWhiteSpace(connectorPluginKey) &&
            pluginsByKey.TryGetValue(connectorPluginKey.Trim(), out var pluginByKey))
        {
            return pluginByKey;
        }

        if (pluginsByLegacyKind.TryGetValue(resourceKind, out var pluginByKind))
        {
            return pluginByKind;
        }

        throw new InvalidOperationException(
            $"No resource connector plugin is registered for kind '{resourceKind}' and plugin key '{connectorPluginKey ?? "<empty>"}'.");
    }

    public IResourceConnectorPlugin Resolve(ProjectResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return Resolve(resource.ResourceKind, resource.ConnectorPluginKey);
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
            Legacy(ResourceKind.Repository, "resource.repository", "Repository resource", ProjectObjectType.Repository, "Repository", "Repository URL is required.", model => model.RepositoryUrl.Trim(), model => JsonSerializer.Serialize(new RepositoryResourceConfig(model.RepositoryUrl, model.DefaultBranch, model.RelativePath)), static (model, json) => { if (JsonSerializer.Deserialize<RepositoryResourceConfig>(NormalizeConfigJson(json)) is { } value) { model.RepositoryUrl = value.RepositoryUrl; model.DefaultBranch = value.DefaultBranch; model.RelativePath = value.RelativePath; } }),
            Legacy(ResourceKind.Folder, "resource.folder", "Folder resource", ProjectObjectType.Connector, "Folder", "Folder path is required.", model => model.FolderPath.Trim(), model => JsonSerializer.Serialize(new FolderResourceConfig(model.FolderPath, model.WorkingDirectory)), static (model, json) => { if (JsonSerializer.Deserialize<FolderResourceConfig>(NormalizeConfigJson(json)) is { } value) { model.FolderPath = value.Path; model.WorkingDirectory = value.WorkingDirectory; } }),
            Legacy(ResourceKind.File, "resource.file", "File resource", ProjectObjectType.File, "File", "File path is required.", model => model.FilePath.Trim(), model => JsonSerializer.Serialize(new FileResourceConfig(model.FilePath, model.WorkingDirectory)), static (model, json) => { if (JsonSerializer.Deserialize<FileResourceConfig>(NormalizeConfigJson(json)) is { } value) { model.FilePath = value.Path; model.WorkingDirectory = value.WorkingDirectory; } }),
            Legacy(ResourceKind.WebLink, "resource.web-link", "Web link resource", ProjectObjectType.Link, "Web link", "URL is required.", model => model.WebUrl.Trim(), model => JsonSerializer.Serialize(new WebLinkResourceConfig(model.WebUrl, model.UrlTitleHint)), static (model, json) => { if (JsonSerializer.Deserialize<WebLinkResourceConfig>(NormalizeConfigJson(json)) is { } value) { model.WebUrl = value.Url; model.UrlTitleHint = value.TitleHint; } }, ConnectorManifestCapability.ProjectResource | ConnectorManifestCapability.WorkbenchProjection | ConnectorManifestCapability.AgentExposure),
            Legacy(ResourceKind.Ftp, "resource.ftp", "FTP resource", ProjectObjectType.Connector, "FTP", "FTP host is required.", model => BuildRemoteEndpoint(model.Host, model.Port, model.RemotePath), model => JsonSerializer.Serialize(new FtpResourceConfig(model.Host, model.Port, model.RemotePath, model.UserName)), static (model, json) => { if (JsonSerializer.Deserialize<FtpResourceConfig>(NormalizeConfigJson(json)) is { } value) { model.Host = value.Host; model.Port = value.Port; model.RemotePath = value.RemotePath; model.UserName = value.UserName; } }),
            Legacy(ResourceKind.Ssh, "resource.ssh", "SSH resource", ProjectObjectType.Connector, "SSH", "SSH host is required.", model => BuildRemoteEndpoint(model.Host, model.Port, model.WorkingDirectory), model => JsonSerializer.Serialize(new SshResourceConfig(model.Host, model.Port, model.UserName, model.WorkingDirectory)), static (model, json) => { if (JsonSerializer.Deserialize<SshResourceConfig>(NormalizeConfigJson(json)) is { } value) { model.Host = value.Host; model.Port = value.Port; model.UserName = value.UserName; model.WorkingDirectory = value.WorkingDirectory; } }),
            Legacy(ResourceKind.PowerShellScript, "resource.powershell-script", "PowerShell script resource", ProjectObjectType.Script, "PowerShell script", "Script path is required.", model => model.ScriptPath.Trim(), model => JsonSerializer.Serialize(new PowerShellScriptResourceConfig(model.ScriptPath, model.ScriptArguments, model.WorkingDirectory)), static (model, json) => { if (JsonSerializer.Deserialize<PowerShellScriptResourceConfig>(NormalizeConfigJson(json)) is { } value) { model.ScriptPath = value.ScriptPath; model.ScriptArguments = value.Arguments; model.WorkingDirectory = value.WorkingDirectory; } }, workbenchSubtype: "shell"),
            Legacy(ResourceKind.DockerCompose, "resource.docker-compose", "Docker Compose resource", ProjectObjectType.Connector, "Docker Compose", "Compose file path is required.", model => model.ComposeFilePath.Trim(), model => JsonSerializer.Serialize(new DockerComposeResourceConfig(model.ComposeFilePath, model.ComposeService)), static (model, json) => { if (JsonSerializer.Deserialize<DockerComposeResourceConfig>(NormalizeConfigJson(json)) is { } value) { model.ComposeFilePath = value.ComposeFilePath; model.ComposeService = value.ServiceName; } }),
            Legacy(ResourceKind.SecretLink, "resource.secret-link", "Secret link resource", ProjectObjectType.SecretReference, "Secret", "Secret purpose is required.", model => model.SecretPurpose.Trim(), model => JsonSerializer.Serialize(new SecretLinkResourceConfig(model.SecretPurpose, string.Empty)), static (model, json) => { if (JsonSerializer.Deserialize<SecretLinkResourceConfig>(NormalizeConfigJson(json)) is { } value) { model.SecretPurpose = value.Purpose; } }),
            Legacy(ResourceKind.PromptLink, "resource.prompt-link", "Prompt link resource", ProjectObjectType.Link, "Prompt link", "Prompt reference is required.", model => model.PromptReference.Trim(), model => JsonSerializer.Serialize(new PromptLinkResourceConfig(model.PromptReference, model.PromptTitleHint)), static (model, json) => { if (JsonSerializer.Deserialize<PromptLinkResourceConfig>(NormalizeConfigJson(json)) is { } value) { model.PromptReference = value.PromptReference; model.PromptTitleHint = value.PromptTitleHint; } }, ConnectorManifestCapability.ProjectResource | ConnectorManifestCapability.WorkbenchProjection | ConnectorManifestCapability.AgentExposure)
        ];

        static IResourceConnectorPlugin Legacy(
            ResourceKind kind,
            string pluginKey,
            string displayName,
            ProjectObjectType workbenchType,
            string workbenchLabel,
            string validationMessage,
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
                    new ConnectorConfigurationSchema("1.0", []),
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
    }

    private static string NormalizeConfigJson(string configJson)
    {
        return string.IsNullOrWhiteSpace(configJson)
            ? "{}"
            : configJson;
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
    public const string PluginKey = "resource.webhook-endpoint";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly ConnectorPluginManifest PluginManifest = new(
        PluginKey,
        "Webhook endpoint connector",
        "1.0.0",
        ConnectorManifestCapability.ProjectResource | ConnectorManifestCapability.WorkbenchProjection | ConnectorManifestCapability.AgentExposure,
        new ConnectorConfigurationSchema(
            "1.0",
            [
                new ConnectorConfigFieldDescriptor("endpointUrl", "Endpoint URL", ConnectorConfigFieldType.Url, true, "Absolute webhook endpoint URL."),
                new ConnectorConfigFieldDescriptor("healthPath", "Health path", ConnectorConfigFieldType.Text, false, "Optional relative path used for lightweight health probes."),
                new ConnectorConfigFieldDescriptor("method", "Method", ConnectorConfigFieldType.Text, true, "HTTP verb used when invoking the connector.")
            ]),
        [new ConnectorSecretRequirement("authorization", "Authorization secret", false, "Optional secret used for outbound authorization.")],
        new ConnectorHealthCheckDescriptor("HTTP GET", "Performs a lightweight request against the configured webhook endpoint."),
        new ConnectorAgentExposure("connector.invoke.webhook", false, true, "Webhook calls require explicit approval."),
        new ConnectorWorkbenchNodeHook(ProjectObjectType.Connector, "webhook-endpoint", "Webhook connector"));

    public ResourceKind? LegacyResourceKind => null;

    public ConnectorPluginManifest Manifest => PluginManifest;

    public Error? ValidateEditor(ResourceEditorModel model)
    {
        if (string.IsNullOrWhiteSpace(model.ConfigJson))
        {
            return Error.Validation("Webhook connector config JSON is required.");
        }

        try
        {
            var config = JsonSerializer.Deserialize<WebhookResourceConfig>(model.ConfigJson, JsonOptions);
            if (config is null || string.IsNullOrWhiteSpace(config.EndpointUrl))
            {
                return Error.Validation("Webhook connector config must contain endpointUrl.");
            }

            return Uri.TryCreate(config.EndpointUrl, UriKind.Absolute, out _)
                ? null
                : Error.Validation("Webhook connector endpointUrl must be an absolute URL.");
        }
        catch (JsonException)
        {
            return Error.Validation("Webhook connector config must be valid JSON.");
        }
    }

    public string BuildLocation(ResourceEditorModel model)
    {
        return JsonSerializer.Deserialize<WebhookResourceConfig>(model.ConfigJson, JsonOptions)?.EndpointUrl?.Trim()
            ?? model.LocationOrIdentifier.Trim();
    }

    public string SerializeConfig(ResourceEditorModel model)
    {
        var config = JsonSerializer.Deserialize<WebhookResourceConfig>(model.ConfigJson, JsonOptions);
        return config is null
            ? "{}"
            : JsonSerializer.Serialize(new WebhookResourceConfig(
                config.EndpointUrl?.Trim() ?? string.Empty,
                config.HealthPath?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(config.Method) ? "POST" : config.Method.Trim().ToUpperInvariant()),
                JsonOptions);
    }

    public void ApplyConfig(ResourceEditorModel model, string configJson)
    {
        model.ConfigJson = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson;
    }

    public ProjectObjectType ResolveWorkbenchObjectType(ProjectResource resource) => ProjectObjectType.Connector;

    public string ResolveWorkbenchObjectSubtype(ProjectResource resource) => "webhook-endpoint";

    private sealed record WebhookResourceConfig(string EndpointUrl, string HealthPath, string Method);
}
