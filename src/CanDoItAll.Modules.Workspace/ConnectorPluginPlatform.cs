using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workspace;

[Flags]
public enum ConnectorManifestCapability
{
    None = 0,
    ProviderExecution = 1 << 0,
    ProjectResource = 1 << 1,
    WorkbenchProjection = 1 << 2,
    AgentExposure = 1 << 3
}

public enum ConnectorConfigFieldType
{
    Text,
    Url,
    Number,
    Boolean,
    Json,
    SecretReference
}

public sealed record ConnectorConfigFieldDescriptor(
    string Key,
    string Label,
    ConnectorConfigFieldType FieldType,
    bool IsRequired,
    string HelpText);

public sealed record ConnectorConfigurationSchema(
    string Version,
    IReadOnlyList<ConnectorConfigFieldDescriptor> Fields);

public sealed record ConnectorSecretRequirement(
    string Key,
    string Label,
    bool IsRequired,
    string Purpose);

public sealed record ConnectorHealthCheckDescriptor(
    string OperationName,
    string Summary);

public sealed record ConnectorAgentExposure(
    string CapabilityKey,
    bool IsExposed,
    bool RequiresApproval,
    string Summary);

public sealed record ConnectorWorkbenchNodeHook(
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string Label);

public sealed record ConnectorPluginManifest(
    string PluginKey,
    string DisplayName,
    string PluginVersion,
    ConnectorManifestCapability Capabilities,
    ConnectorConfigurationSchema ConfigurationSchema,
    IReadOnlyList<ConnectorSecretRequirement> SecretRequirements,
    ConnectorHealthCheckDescriptor HealthCheck,
    ConnectorAgentExposure AgentExposure,
    ConnectorWorkbenchNodeHook? WorkbenchNodeHook);

public interface IConnectorPlugin
{
    ConnectorPluginManifest Manifest { get; }
}

public interface IConnectorManifestSource
{
    IReadOnlyList<ConnectorPluginManifest> ListManifests();
}

public sealed class ConnectorPluginRegistry(IEnumerable<IConnectorManifestSource> sources)
{
    private readonly IReadOnlyDictionary<string, ConnectorPluginManifest> manifestsByKey = sources
        .SelectMany(source => source.ListManifests())
        .GroupBy(manifest => manifest.PluginKey, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

    public ConnectorPluginManifest Resolve(string pluginKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);

        if (manifestsByKey.TryGetValue(pluginKey.Trim(), out var manifest))
        {
            return manifest;
        }

        throw new InvalidOperationException($"Connector plugin '{pluginKey}' is not registered.");
    }

    public bool TryResolve(string? pluginKey, out ConnectorPluginManifest manifest)
    {
        manifest = default!;

        if (string.IsNullOrWhiteSpace(pluginKey))
        {
            return false;
        }

        return manifestsByKey.TryGetValue(pluginKey.Trim(), out manifest!);
    }

    public IReadOnlyList<ConnectorPluginManifest> List(ConnectorManifestCapability? requiredCapability = null)
    {
        return manifestsByKey.Values
            .Where(manifest => !requiredCapability.HasValue || manifest.Capabilities.HasFlag(requiredCapability.Value))
            .OrderBy(manifest => manifest.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
