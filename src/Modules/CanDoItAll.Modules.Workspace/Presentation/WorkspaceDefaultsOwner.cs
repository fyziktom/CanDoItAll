using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceDefaultsOwner(WorkspaceService workspace, IWorkspaceProviderCatalog providers,
    ICanonicalRuntimeDatabase canonical) : IWorkspaceDefaultsOwner {
    private readonly Guid profile = canonical.Profile.Profile.Id;
    private readonly long generation = canonical.Generation;
    public bool IsCurrent => canonical.Profile.Profile.Id == profile && canonical.Generation == generation;
    public Task<WorkspaceSettingsSnapshot> ReadAsync(CancellationToken cancellationToken) => workspace.ReadSettingsSnapshotAsync(cancellationToken);
    public Task<WorkspaceSettingsSnapshotV2> ReadV2Async(CancellationToken cancellationToken) => workspace.ReadSettingsSnapshotV2Async(cancellationToken);
    public Task<IReadOnlyList<WorkspaceProviderOption>> ProvidersAsync(CancellationToken cancellationToken) => providers.ListAsync(cancellationToken);

    public Task<WorkspaceSettingsSaveOutcome> SaveAsync(ReplaceWorkspaceSettingsCommand command, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(command);
        if (!IsCurrent) {
            return Task.FromResult<WorkspaceSettingsSaveOutcome>(new WorkspaceSettingsSaveOutcome.Rejected(WorkspaceSettingsRejectionReason.OwnerRetired));
        }
        return workspace.SaveSettingsAsync(command, cancellationToken);
    }

    public Task<WorkspaceSettingsSaveOutcomeV2> SaveV2Async(ReplaceWorkspaceSettingsV2Command command, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(command);
        if (!IsCurrent) {
            return Task.FromResult<WorkspaceSettingsSaveOutcomeV2>(new WorkspaceSettingsSaveOutcomeV2.Rejected(WorkspaceSettingsRejectionReason.OwnerRetired));
        }
        return workspace.SaveSettingsV2Async(command, cancellationToken);
    }
}
