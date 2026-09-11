using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

internal sealed class AcceptingWorkflowProviderInputAdmission : IWorkflowProviderInputAdmission {
    public ValueTask RequireAsync(WorkflowDefinition definition, WorkflowNode node, WorkflowNodeInput input,
        CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
