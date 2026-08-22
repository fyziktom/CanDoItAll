using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

internal sealed class WorkflowLaunchTestAuthorizationScopeResolver :
    IWorkflowLaunchAuthorizationScopeResolver
{
    public WorkflowLaunchAuthorizationScope Resolve(WorkflowLaunchOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        return new WorkflowLaunchAuthorizationScope(
            WorkspaceScopeDescriptor.Project("workflow-launch-test"),
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint);
    }
}
