using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class NullWorkflowEventSink : IWorkflowEventSink
{
    public Task PublishAsync(WorkflowEventRecord workflowEvent, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
