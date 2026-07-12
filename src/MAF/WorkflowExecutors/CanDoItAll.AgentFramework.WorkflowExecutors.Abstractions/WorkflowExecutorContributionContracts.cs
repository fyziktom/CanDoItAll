using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public interface IWorkflowExecutorContribution
{
    WorkflowExecutorDescriptor Descriptor { get; }
}
