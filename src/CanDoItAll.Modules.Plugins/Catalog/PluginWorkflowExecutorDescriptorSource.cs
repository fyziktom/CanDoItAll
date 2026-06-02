using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

public sealed class PluginWorkflowExecutorDescriptorSource(
    IEnumerable<ICanDoItAllPlugin> plugins,
    PluginGrantEvaluator grantEvaluator) : IWorkflowExecutorDescriptorSource
{
    public IEnumerable<WorkflowExecutorDescriptor> ListExecutorDescriptors()
        => plugins
            .Select(plugin => plugin.Descriptor)
            .SelectMany(plugin => plugin.WorkflowExecutors.Select(executor => CreateDescriptor(plugin, executor)));

    private WorkflowExecutorDescriptor CreateDescriptor(
        PluginDescriptor plugin,
        PluginWorkflowExecutorDescriptor executor)
        => new(
            executor.ExecutorId,
            executor.Name,
            executor.Description,
            executor.Category,
            ResolveIconName(plugin),
            executor.SettingsRendererKey.Value,
            executor.InputShape,
            executor.ResultShape,
            SettingsSchemaJson: "{}",
            DefaultSettingsJson: "{}",
            executor.DefaultPolicy,
            IsImplemented: true)
        {
            Source = CreateSource(plugin),
            Availability = ResolveAvailability(plugin),
            ConfigurationSchema = executor.SettingsSchema,
            SettingsSchema = WorkflowExecutorSettingsSchemaDescriptor.JsonSchema(
                executor.SettingsSchema.Version,
                "{}"),
            PermissionPolicy = executor.PermissionPolicy,
            SideEffects = executor.SideEffects,
            DeterministicTestMode = executor.DeterministicTestMode
        };

    private WorkflowExecutorAvailabilityDescriptor ResolveAvailability(PluginDescriptor plugin)
    {
        var workflowGrant = grantEvaluator.Evaluate(plugin.Id, PluginCapabilityKind.WorkflowExecutor);
        if (!workflowGrant.Allowed)
        {
            return WorkflowExecutorAvailabilityDescriptor.Unavailable(
                workflowGrant.Kind.ToString(),
                workflowGrant.Message);
        }

        if (plugin.Capabilities.HasFlag(PluginCapabilityKind.OAuth2))
        {
            var oauthGrant = grantEvaluator.Evaluate(plugin.Id, PluginCapabilityKind.OAuth2);
            if (!oauthGrant.Allowed)
            {
                return WorkflowExecutorAvailabilityDescriptor.Unavailable(
                    oauthGrant.Kind.ToString(),
                    oauthGrant.Message);
            }
        }

        return WorkflowExecutorAvailabilityDescriptor.Available();
    }

    private static WorkflowExecutorSourceDescriptor CreateSource(PluginDescriptor plugin)
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

        return WorkflowExecutorSourceDescriptor.Package(
            MapSourceKind(plugin.SourceKind),
            plugin.Id.Value,
            plugin.Package?.PackageId.Value ?? string.Empty,
            plugin.Version,
            MapTrustLevel(plugin.TrustLevel),
            plugin.DisplayName,
            icon);
    }

    private static string ResolveIconName(PluginDescriptor plugin)
        => plugin.Icon is { Kind: UiIconKind.MaterialIcon, Value: { Length: > 0 } value }
            ? value
            : "extension";

    private static WorkflowExecutorSourceKind MapSourceKind(PluginSourceKind sourceKind)
        => sourceKind switch
        {
            PluginSourceKind.Bundled => WorkflowExecutorSourceKind.BundledPlugin,
            PluginSourceKind.LocalPackage => WorkflowExecutorSourceKind.LocalPackage,
            PluginSourceKind.RemotePackage or PluginSourceKind.ShopCatalog => WorkflowExecutorSourceKind.RemotePackage,
            _ => WorkflowExecutorSourceKind.RemotePackage
        };

    private static WorkflowExecutorTrustLevel MapTrustLevel(PluginTrustLevel trustLevel)
        => trustLevel switch
        {
            PluginTrustLevel.Application => WorkflowExecutorTrustLevel.Application,
            PluginTrustLevel.Bundled => WorkflowExecutorTrustLevel.BundledPlugin,
            PluginTrustLevel.LocalPackage => WorkflowExecutorTrustLevel.LocalPackage,
            PluginTrustLevel.RemotePackage => WorkflowExecutorTrustLevel.RemotePackage,
            _ => WorkflowExecutorTrustLevel.Untrusted
        };
}
