using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public interface IWorkflowProcessToolSourceAuthority {
    Task<WorkflowLaunchOrigin.ProcessToolInvocation> CaptureAsync(AgentToolSessionAdmission session,
        AgentToolAdmittedInvocation invocation, Guid launchCapabilityId, CancellationToken cancellationToken = default);

    Task<IWorkflowStructureSourceAuthorityLease> AcquireForMutationAsync(WorkflowLaunchOrigin.ProcessToolInvocation origin,
        CancellationToken cancellationToken = default);

    Task<IAsyncDisposable> AcquireDisclosureAsync(WorkflowLaunchOrigin.ProcessToolInvocation origin,
        CancellationToken cancellationToken = default);
}
