using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class DefaultWorkflowLaunchAuthorizationScopeResolver :
    IWorkflowLaunchAuthorizationScopeResolver
{
    public WorkflowLaunchAuthorizationScope Resolve(WorkflowLaunchOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (origin.AuthorizationScope is null ||
            !string.Equals(
                origin.AuthorizationPolicyFingerprint,
                WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Workflow launch origin requires a trusted authorization scope and the current response authorization policy.");
        }

        return new WorkflowLaunchAuthorizationScope(
            origin.AuthorizationScope,
            origin.AuthorizationPolicyFingerprint);
    }
}
