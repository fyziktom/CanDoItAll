using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public interface IWorkflowScheduledSourceAuthorityPolicy {
    Task<IWorkflowScheduledSourceAuthorityLease> AcquireAsync(WorkflowStructureAuthority authority,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowScheduledSourceAuthorityLease : IAsyncDisposable {
    Task RequireForMutationAsync(CancellationToken cancellationToken = default);
}

public sealed class WorkflowScheduledSourceAuthorityException(string message) : UnauthorizedAccessException(message);
