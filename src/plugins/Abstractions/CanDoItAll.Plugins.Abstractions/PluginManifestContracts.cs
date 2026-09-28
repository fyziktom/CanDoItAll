using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Plugins.Abstractions;

/// <summary>
/// Capabilities that a plugin declares, as a JSON integer bit mask (add the values of the flags): 0 None,
/// 1 WorkflowExecutor (provides workflow executors), 2 SettingsRenderer (custom settings user interface),
/// 4 SecretReference (uses stored secrets), 8 WorkspaceFiles (reads and writes workspace files), 16 Storage (places
/// files in storage), 32 ProjectStructure (reads project structures), 64 HttpClient (calls external HTTP services),
/// 128 OAuth2 (uses OAuth 2.0 connections), 256 ExecutionEvents (records execution events), 512 HostCommand (runs
/// reviewed host commands). For example 193 means WorkflowExecutor, HttpClient and OAuth2. Grants are evaluated per
/// single flag, so a grant for a combined value never takes effect.
/// </summary>
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

/// <summary>
/// Where a plugin comes from, as a JSON integer: 0 Bundled (compiled into the host), 1 LocalPackage (installed from a
/// package file), 2 RemotePackage, 3 ShopCatalog. Values 2 and 3 are defined but not produced by the current host.
/// </summary>
public enum PluginSourceKind
{
    Bundled,
    LocalPackage,
    RemotePackage,
    ShopCatalog
}

/// <summary>
/// Trust level of a plugin, as a JSON integer: 0 Application, 1 Bundled, 2 LocalPackage, 3 RemotePackage,
/// 4 Untrusted. Packages cannot claim 0 or 1, and only bundled plugins may provide custom settings renderers for
/// workflow executors. 4 is also reported when the manifest of an installed plugin can no longer be read.
/// </summary>
public enum PluginTrustLevel
{
    Application,
    Bundled,
    LocalPackage,
    RemotePackage,
    Untrusted
}

/// <summary>
/// Manifest of a plugin: identity, declared capabilities, workflow executors, settings, connection kinds, package and
/// OAuth metadata. It is returned as <c>descriptor</c> of a plugin catalog item.
/// </summary>
/// <param name="Id">Identifier of the plugin.</param>
/// <param name="DisplayName">Display name of the plugin.</param>
/// <param name="Description">Description of the plugin.</param>
/// <param name="Version">Version text declared by the plugin, for example <c>1.0.0</c>.</param>
/// <param name="Vendor">Vendor of the plugin.</param>
/// <param name="SourceKind">
/// Where the plugin comes from, as a JSON integer: 0 Bundled, 1 LocalPackage, 2 RemotePackage, 3 ShopCatalog.
/// </param>
/// <param name="TrustLevel">
/// Trust level, as a JSON integer: 0 Application, 1 Bundled, 2 LocalPackage, 3 RemotePackage, 4 Untrusted.
/// </param>
/// <param name="MinAppVersion">
/// Minimum application version declared by the manifest; informational, the server does not check it.
/// </param>
/// <param name="Capabilities">
/// Declared capabilities, as a JSON integer bit mask: 1 WorkflowExecutor, 2 SettingsRenderer, 4 SecretReference,
/// 8 WorkspaceFiles, 16 Storage, 32 ProjectStructure, 64 HttpClient, 128 OAuth2, 256 ExecutionEvents, 512 HostCommand;
/// 0 for none. A capability must be declared here before it can be granted.
/// </param>
/// <param name="WorkflowExecutors">Workflow executors that the plugin provides; empty when it provides none.</param>
/// <param name="Settings">Plugin-level settings form and settings renderers.</param>
/// <param name="Connections">Connection kinds that the plugin can use.</param>
/// <param name="Package">Package metadata; null when the plugin declares no package.</param>
/// <param name="OAuth2">OAuth 2.0 configuration of the plugin; null when it uses no OAuth connection.</param>
/// <param name="Icon">Icon of the plugin; null means the default icon.</param>
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
    UiIconDescriptor? Icon = null)
{
    /// <summary>Free-form tags of the plugin, for example <c>email</c>, <c>oauth</c> or <c>workflow</c>.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}

public static class PluginDescriptorTags
{
    public const string Docker = "docker";
    public const string Email = "email";
    public const string HostCommand = "host-command";
    public const string OAuth = "oauth";
    public const string Workflow = "workflow";
}

/// <summary>
/// A workflow executor that a plugin provides: a node type that workflows can use, with its settings form, value
/// shapes, default limits and safety metadata.
/// </summary>
/// <param name="ExecutorId">
/// Identifier of the executor, for example <c>gmail.messages-by-label</c>; workflow nodes refer to it.
/// </param>
/// <param name="Name">Display name of the executor.</param>
/// <param name="Description">Description of the executor.</param>
/// <param name="Category">
/// Category of the executor, as a JSON integer: 0 Storage, 1 ProjectStructure, 2 Http, 3 Image, 4 Spreadsheet,
/// 5 Data, 6 Markdown, 7 Human, 8 Utility, 9 Command.
/// </param>
/// <param name="SettingsRendererKey">Key of the settings renderer that the workflow editor uses for the node.</param>
/// <param name="SettingsSchema">Settings form of the executor's node settings.</param>
/// <param name="InputShape">Shape of the value that the executor accepts as input.</param>
/// <param name="ResultShape">Shape of the value that the executor returns.</param>
/// <param name="DefaultPolicy">Default execution limits: timeout, retries and output capture.</param>
public sealed record PluginWorkflowExecutorDescriptor(
    WorkflowExecutorId ExecutorId,
    string Name,
    string Description,
    WorkflowExecutorCategoryKind Category,
    PluginRendererKey SettingsRendererKey,
    ConfigurationSchema SettingsSchema,
    WorkflowValueShape InputShape,
    WorkflowValueShape ResultShape,
    WorkflowExecutorExecutionPolicy DefaultPolicy)
{
    /// <summary>
    /// Default node settings as JSON text: a string whose content is a JSON object such as
    /// <c>{"maxMessages":1}</c>, not a nested JSON object.
    /// </summary>
    public string DefaultSettingsJson { get; init; } = "{}";

    /// <summary>
    /// How the node settings are edited, as a JSON integer: 0 Schema (a form generated from <c>settingsSchema</c>),
    /// 1 CustomRenderer (the renderer named by <c>settingsRendererKey</c>).
    /// </summary>
    public WorkflowExecutorSettingsPresentationMode SettingsPresentationMode { get; init; } =
        WorkflowExecutorSettingsPresentationMode.Schema;

    /// <summary>Whether and how a run preview can simulate the executor without calling external services.</summary>
    public WorkflowExecutorSimulationDescriptor Simulation { get; init; } = WorkflowExecutorSimulationDescriptor.None;

    /// <summary>Capabilities that the executor requires and whether it needs approval.</summary>
    public WorkflowExecutorPermissionPolicy PermissionPolicy { get; init; } = WorkflowExecutorPermissionPolicy.None;

    /// <summary>External effects of the executor and how previews, dry runs and commits behave.</summary>
    public WorkflowExecutorSideEffectDescriptor SideEffects { get; init; } = WorkflowExecutorSideEffectDescriptor.None;

    /// <summary>Whether the executor can run in a deterministic test mode with simulated inputs.</summary>
    public WorkflowExecutorDeterministicTestModeDescriptor DeterministicTestMode { get; init; } = WorkflowExecutorDeterministicTestModeDescriptor.None;

    public static PluginWorkflowExecutorDescriptor FromWorkflowExecutorDescriptor(
        WorkflowExecutorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new PluginWorkflowExecutorDescriptor(
            descriptor.Id,
            descriptor.Name,
            descriptor.Description,
            descriptor.Category,
            new PluginRendererKey(descriptor.SetupRendererKey),
            descriptor.ConfigurationSchema,
            descriptor.InputShape,
            descriptor.ResultShape,
            descriptor.DefaultPolicy)
        {
            DefaultSettingsJson = descriptor.DefaultSettingsJson,
            SettingsPresentationMode = descriptor.SettingsPresentationMode,
            Simulation = descriptor.Simulation,
            PermissionPolicy = descriptor.PermissionPolicy,
            SideEffects = descriptor.SideEffects,
            DeterministicTestMode = descriptor.DeterministicTestMode
        };
    }
}

/// <summary>
/// Package metadata declared in a plugin manifest. The server records it as declared: it does not verify the hash or
/// the signature and does not use the catalog address.
/// </summary>
/// <param name="PackageId">Identifier of the package.</param>
/// <param name="Version">Version text of the package.</param>
/// <param name="MinAppVersion">
/// Minimum application version declared by the package; informational, the server does not check it.
/// </param>
/// <param name="Sha256">Package hash as declared by the manifest; not verified by the server.</param>
/// <param name="Signature">Package signature as declared by the manifest; not verified by the server.</param>
/// <param name="CatalogUri">Catalog address as declared by the manifest; null when none is declared.</param>
public sealed record PluginPackageDescriptor(
    PluginPackageId PackageId,
    string Version,
    string MinAppVersion,
    string Sha256,
    string Signature,
    Uri? CatalogUri = null);
