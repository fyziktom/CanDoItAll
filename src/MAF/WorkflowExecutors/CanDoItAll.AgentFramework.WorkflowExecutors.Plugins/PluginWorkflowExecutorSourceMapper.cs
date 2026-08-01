using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;

public static class PluginWorkflowExecutorSourceMapper
{
    private const string ObjectSettingsSchemaJson = "{\"type\":\"object\"}";

    public static WorkflowExecutorSourceDescriptor CreateSource(PluginDescriptor plugin)
    {
        var icon = plugin.Icon ?? UiIconDescriptor.Default;
        if (plugin.SourceKind == PluginSourceKind.Bundled)
        {
            return WorkflowExecutorSourceDescriptor.BundledPlugin(
                plugin.Id.Value,
                plugin.Version,
                plugin.DisplayName,
                icon);
        }

        return CreatePackageSource(plugin);
    }

    public static WorkflowExecutorSourceDescriptor CreatePackageSource(PluginDescriptor plugin)
    {
        var package = plugin.Package
            ?? throw PluginWorkflowExecutorActivationException.MissingPackageMetadata(
                plugin,
                "plugin-descriptor",
                "plugin-source-mapping");
        return WorkflowExecutorSourceDescriptor.Package(
            MapSourceKind(plugin.SourceKind),
            plugin.Id.Value,
            package.PackageId.Value,
            plugin.Version,
            MapTrustLevel(plugin.TrustLevel),
            plugin.DisplayName,
            plugin.Icon ?? UiIconDescriptor.Default);
    }

    public static string ResolveIconName(PluginDescriptor plugin)
        => plugin.Icon is { Kind: UiIconKind.MaterialIcon, Value: { Length: > 0 } value }
            ? value
            : "extension";

    public static WorkflowExecutorDescriptor CreateDescriptor(
        PluginDescriptor plugin,
        PluginWorkflowExecutorDescriptor executor)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(executor);

        return new WorkflowExecutorDescriptor(
            executor.ExecutorId,
            executor.Name,
            executor.Description,
            executor.Category,
            ResolveIconName(plugin),
            executor.SettingsRendererKey.Value,
            executor.InputShape,
            executor.ResultShape,
            ObjectSettingsSchemaJson,
            executor.DefaultSettingsJson,
            executor.DefaultPolicy,
            IsImplemented: true)
        {
            Source = CreateSource(plugin),
            Availability = WorkflowExecutorAvailabilityDescriptor.Available(),
            SettingsSchema = WorkflowExecutorSettingsSchemaDescriptor.JsonSchema(
                executor.SettingsSchema.Version,
                ObjectSettingsSchemaJson),
            ConfigurationSchema = executor.SettingsSchema,
            SettingsPresentationMode = executor.SettingsPresentationMode,
            Simulation = executor.Simulation,
            PermissionPolicy = executor.PermissionPolicy,
            SideEffects = executor.SideEffects,
            DeterministicTestMode = executor.DeterministicTestMode
        };
    }

    public static WorkflowExecutorSourceKind MapSourceKind(PluginSourceKind sourceKind)
        => sourceKind switch
        {
            PluginSourceKind.Bundled => WorkflowExecutorSourceKind.BundledPlugin,
            PluginSourceKind.LocalPackage => WorkflowExecutorSourceKind.LocalPackage,
            PluginSourceKind.RemotePackage or PluginSourceKind.ShopCatalog => WorkflowExecutorSourceKind.RemotePackage,
            _ => WorkflowExecutorSourceKind.RemotePackage
        };

    public static WorkflowExecutorTrustLevel MapTrustLevel(PluginTrustLevel trustLevel)
        => trustLevel switch
        {
            PluginTrustLevel.Application => WorkflowExecutorTrustLevel.Application,
            PluginTrustLevel.Bundled => WorkflowExecutorTrustLevel.BundledPlugin,
            PluginTrustLevel.LocalPackage => WorkflowExecutorTrustLevel.LocalPackage,
            PluginTrustLevel.RemotePackage => WorkflowExecutorTrustLevel.RemotePackage,
            _ => WorkflowExecutorTrustLevel.Untrusted
        };
}
