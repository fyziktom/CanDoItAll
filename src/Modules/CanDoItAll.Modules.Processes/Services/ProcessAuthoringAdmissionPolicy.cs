using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessAuthoringAdmissionPolicy(ICanonicalRuntimeDatabase database, ProjectWriteAdmissionService projects)
    : IProcessAuthoringAdmissionPolicy, IProcessAuthoringContext {
    public const string LocalCallerId = "local-process-authoring-operator";
    public Guid DatabaseProfileId => database.Profile.Profile.Id;
    public string CallerId => LocalCallerId;

    public async Task<ProcessAuthoringAddress> CaptureReadAddressAsync(ProcessWorkspaceShellScope scope, ProcessDefinitionCatalogItemKey key, CancellationToken cancellationToken) {
        if (scope.Kind == ProcessWorkspaceScopeKind.Global && scope.ProjectId is null) {
            return new(DatabaseProfileId, Guid.Empty, Guid.Empty, key.Value);
        }
        if (scope.Kind != ProcessWorkspaceScopeKind.Project || scope.ProjectId is not { } projectId) {
            throw new ArgumentException("Authoring requires an explicit global or project scope.");
        }
        var captured = await projects.CaptureAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException("The authoring project no longer exists.");
        return new(captured.DatabaseProfileId, captured.ProjectId, captured.LifetimeId, key.Value);
    }

    public async Task RequireAsync(ProcessAuthoringAddress address, string? callerId, bool underMutationGate, CancellationToken cancellationToken) {
        address.Validate();
        if (address.DatabaseProfileId != database.Profile.Profile.Id || callerId is not null && callerId != LocalCallerId) {
            throw new InvalidOperationException("The authoring caller or captured database profile is no longer current.");
        }
        if (address.ProjectId == Guid.Empty) {
            return;
        }
        ProjectWriteAdmission admission = new(address.DatabaseProfileId, address.ProjectId, address.ProjectLifetimeId);
        if (underMutationGate) {
            await projects.RequireUnderMutationGateAsync(admission, cancellationToken);
        } else {
            await projects.RequireCurrentAsync(admission, cancellationToken);
        }
    }
}
