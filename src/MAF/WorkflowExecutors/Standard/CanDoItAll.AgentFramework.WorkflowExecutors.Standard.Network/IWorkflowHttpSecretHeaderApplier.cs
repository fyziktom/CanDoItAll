using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;

public interface IWorkflowHttpSecretHeaderApplier {
    Task<WorkflowHttpSecretUse> ApplyAsync(WorkflowExecutorExecutionContext context, WorkflowNodeInput input,
        HttpRequestMessage request, CancellationToken cancellationToken = default);
}
