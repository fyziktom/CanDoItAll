using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Plugins.Abstractions;

[Flags]
public enum PluginCapabilityKind
{
    None = 0,
    WorkflowExecutor = 1 << 0,
    SettingsRenderer = 1 << 1,
    SecretReference = 1 << 2,
    WorkspaceFiles = 1 << 3,
    Storage = 1 << 4,
    ProjectStructure = 1 << 5,
    HttpClient = 1 << 6,
    OAuth2 = 1 << 7,
    ExecutionEvents = 1 << 8,
    HostCommand = 1 << 9
}

public enum PluginSourceKind
{
    Bundled,
    LocalPackage,
    RemotePackage,
    ShopCatalog
}

public enum PluginTrustLevel
{
    Application,
    Bundled,
    LocalPackage,
    RemotePackage,
    Untrusted
}

public sealed record PluginDescriptor(
    PluginId Id,
    string DisplayName,
    string Description,
    string Version,
    string Vendor,
    PluginSourceKind SourceKind,
    PluginTrustLevel TrustLevel,
    string MinAppVersion,
    PluginCapabilityKind Capabilities,
    IReadOnlyList<PluginWorkflowExecutorDescriptor> WorkflowExecutors,
    PluginSettingsDescriptor Settings,
    IReadOnlyList<PluginConnectionDescriptor> Connections,
    PluginPackageDescriptor? Package = null,
    PluginOAuth2Descriptor? OAuth2 = null,
    UiIconDescriptor? Icon = null);

public sealed record PluginWorkflowExecutorDescriptor(
    WorkflowExecutorId ExecutorId,
    string Name,
    string Description,
    WorkflowExecutorCategoryKind Category,
    PluginRendererKey SettingsRendererKey,
    ConfigurationSchema SettingsSchema,
    WorkflowValueShape InputShape,
    WorkflowValueShape ResultShape,
    WorkflowExecutorExecutionPolicy DefaultPolicy);

public sealed record PluginPackageDescriptor(
    PluginPackageId PackageId,
    string Version,
    string MinAppVersion,
    string Sha256,
    string Signature,
    Uri? CatalogUri = null);
