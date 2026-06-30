using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;

public sealed class RuntimePackageWorkflowExecutorDescriptorSource(IWorkflowExecutor executor) : IWorkflowExecutorDescriptorSource
{
    public IEnumerable<WorkflowExecutorDescriptor> ListExecutorDescriptors()
    {
        yield return executor.Descriptor;
    }
}
