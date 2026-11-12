using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessAuthoringAdmissionPolicy(ICanonicalRuntimeDatabase database, ProjectWriteAdmissionService projects)
    : IProcessAuthoringAdmissionPolicy, IProcessAuthoringContext {
    private ProcessAuthoringUiAuthority? uiAuthority;
    internal ProcessAuthoringUiAuthority? UiAuthority => uiAuthority;
    public const string LocalCallerId = "local-process-authoring-operator";
    public Guid DatabaseProfileId => database.Profile.Profile.Id;
    public string CallerId => uiAuthority?.CallerId ?? LocalCallerId;

    internal void BindUiAuthority(ProcessAuthoringUiAuthority authority) {
        if (uiAuthority is not null || string.IsNullOrWhiteSpace(authority.CallerId) || authority.CallerId.Length > 200) {
            throw new InvalidOperationException("A bounded original UI caller can be bound only once to an authoring scope.");
        }
        uiAuthority = authority;
    }

    public async Task<ProcessAuthoringAddress> CaptureReadAddressAsync(ProcessWorkspaceShellScope scope, ProcessDefinitionCatalogItemKey key, CancellationToken cancellationToken) {
        if (uiAuthority is { } ui) {
            await ui.Authorize(false, cancellationToken);
        }
        if (scope.Kind == ProcessWorkspaceScopeKind.Global && scope.ProjectId is null) {
            return new(DatabaseProfileId, Guid.Empty, Guid.Empty, key.Value);
        }
        if (scope.Kind != ProcessWorkspaceScopeKind.Project || scope.ProjectId is not { } projectId) {
            throw new ArgumentException("Authoring requires an explicit global or project scope.");
        }
        var captured = await projects.CaptureAsync(projectId, cancellationToken)
            ?? throw new ProcessAuthoringProjectUnavailableException(projectId);
        return new(captured.DatabaseProfileId, captured.ProjectId, captured.LifetimeId, key.Value);
    }

    public async Task RequireAsync(ProcessAuthoringAddress address, string? callerId, bool underMutationGate, CancellationToken cancellationToken) {
        address.Validate();
        if (address.DatabaseProfileId != database.Profile.Profile.Id || callerId is not null && callerId != CallerId) {
            throw new InvalidOperationException("The authoring caller or captured database profile is no longer current.");
        }
        if (uiAuthority is { } ui) {
            var inheritedRead = callerId is null && address.ProjectId == Guid.Empty;
            if (ui.Scope.Id != address.DatabaseProfileId || ui.Scope.ActivationGeneration != database.Generation ||
                !inheritedRead && ui.ProjectId.GetValueOrDefault() != address.ProjectId) {
                throw new UnauthorizedAccessException("The original UI authority belongs to a different Process scope.");
            }
            await ui.Authorize(callerId is not null, cancellationToken);
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
