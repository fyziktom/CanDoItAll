using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class NullWorkflowEventSink : IWorkflowEventSink
{
    public Task PublishAsync(WorkflowEventRecord workflowEvent, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
