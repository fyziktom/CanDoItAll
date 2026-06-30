using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;

public sealed class RuntimePackageWorkflowExecutor(
    IWorkflowExecutor inner,
    PluginDescriptor pluginDescriptor,
    Type implementationType) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor
    {
        get
        {
            var descriptor = inner.Descriptor;
            return descriptor with
            {
                Source = CreateSource()
            };
        }
    }

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
        => inner.ExecuteAsync(context, input, cancellationToken);

    private WorkflowExecutorSourceDescriptor CreateSource()
    {
        if (pluginDescriptor.Package is null)
        {
            throw PluginWorkflowExecutorActivationException.MissingPackageMetadata(
                pluginDescriptor,
                implementationType);
        }

        return PluginWorkflowExecutorSourceMapper.CreatePackageSource(pluginDescriptor);
    }
}
