using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public enum WorkflowStructureAuthorityUse {
    Admission,
    TaskOutput,
    AssetOutput,
    Schedule,
    Disclosure,
    StructureAdmission,
    StatusProjection
}

public interface IWorkflowStructureLaunchPreparation {
    Task<WorkflowStructureAuthority> PrepareLaunchAsync(WorkflowStructureAuthority authority, WorkflowDefinition definition,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowStructureSourceAuthorityPolicy {
    Task<IWorkflowStructureSourceAuthorityLease> AcquireAsync(WorkflowStructureAuthority authority,
        WorkflowStructureAuthorityUse use, WorkflowProjectLifetime? target = null, CancellationToken cancellationToken = default);
}

public interface IWorkflowStructureSourceAuthorityLease : IAsyncDisposable {
    Task RequireForMutationAsync(CancellationToken cancellationToken = default);
}
