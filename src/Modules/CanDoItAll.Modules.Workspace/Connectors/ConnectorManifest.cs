using CanDoItAll.SharedKernel;
using CanDoItAll.SharedKernel.Configuration;

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

public static class ConnectorConfigFieldType
{
    public const ConfigurationFieldType Text = ConfigurationFieldType.Text;
    public const ConfigurationFieldType Url = ConfigurationFieldType.Url;
    public const ConfigurationFieldType Number = ConfigurationFieldType.Number;
    public const ConfigurationFieldType Boolean = ConfigurationFieldType.Boolean;
    public const ConfigurationFieldType Json = ConfigurationFieldType.Json;
    public const ConfigurationFieldType SecretReference = ConfigurationFieldType.SecretReference;
    public const ConfigurationFieldType Select = ConfigurationFieldType.Select;
    public const ConfigurationFieldType MultilineText = ConfigurationFieldType.MultilineText;
    public const ConfigurationFieldType Guid = ConfigurationFieldType.Guid;
}

public sealed record ConnectorConfigFieldDescriptor(
    string Key,
    string Label,
    ConfigurationFieldType FieldType,
    bool IsRequired,
    string HelpText) : ConfigurationFieldDescriptor(
    Key,
    Label,
    FieldType,
    IsRequired,
    HelpText);

public sealed record ConnectorConfigurationSchema(
    string Version,
    IReadOnlyList<ConfigurationFieldDescriptor> Fields) : ConfigurationSchema(
    Version,
    Fields);

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
