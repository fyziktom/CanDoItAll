using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;

public sealed class PluginWorkflowExecutorDescriptorSource(
    IEnumerable<ICanDoItAllPlugin> plugins,
    IPluginWorkflowExecutorGrantEvaluator grantEvaluator) : IWorkflowExecutorDescriptorSource
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
            PluginWorkflowExecutorSourceMapper.ResolveIconName(plugin),
            executor.SettingsRendererKey.Value,
            executor.InputShape,
            executor.ResultShape,
            SettingsSchemaJson: "{}",
            DefaultSettingsJson: "{}",
            executor.DefaultPolicy,
            IsImplemented: true)
        {
            Source = PluginWorkflowExecutorSourceMapper.CreateSource(plugin),
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
}
