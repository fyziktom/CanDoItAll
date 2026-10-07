using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceDefaultsOwner(WorkspaceService workspace, IWorkspaceProviderCatalog providers,
    ICanonicalRuntimeDatabase canonical) : IWorkspaceDefaultsOwner {
    private readonly Guid profile = canonical.Profile.Profile.Id;
    private readonly long generation = canonical.Generation;
    public bool IsCurrent => canonical.Profile.Profile.Id == profile && canonical.Generation == generation;
    public Task<WorkspaceSettingsModel> ReadAsync(CancellationToken cancellationToken) => workspace.GetSettingsAsync(cancellationToken);
    public Task<IReadOnlyList<WorkspaceProviderOption>> ProvidersAsync(CancellationToken cancellationToken) => providers.ListAsync(cancellationToken);

    public async Task<SettingsWriteResult<WorkspaceSettingsModel>> SaveAsync(WorkspaceSettingsModel command, CancellationToken cancellationToken) {
        var captured = command.Copy();
        if (!IsCurrent) {
            return new(SettingsWriteState.Refused, captured, SettingsDiagnostic.Retired);
        }
        try {
            return new(SettingsWriteState.Committed, await workspace.SaveSettingsAsync(captured, cancellationToken));
        } catch (WorkspaceSettingsCommittedException committed) {
            return new(SettingsWriteState.CommittedWarning, committed.Saved, committed.Diagnostic);
        }
    }
}
