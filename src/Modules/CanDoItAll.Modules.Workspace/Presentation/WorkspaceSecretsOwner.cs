using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Security;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceSecretsOwner(SecretService secrets, ICanonicalRuntimeDatabase canonical) : IWorkspaceSecretsOwner {
    private readonly Guid profile = canonical.Profile.Profile.Id;
    private readonly long generation = canonical.Generation;
    public bool IsCurrent => canonical.Profile.Profile.Id == profile && canonical.Generation == generation;
    public Task<IReadOnlyList<SecretListItem>> ListAsync(CancellationToken cancellationToken) => secrets.ListForPickerAsync(cancellationToken);
    public Task<SecretEditorModel?> GetAsync(Guid id, CancellationToken cancellationToken) => secrets.GetAsync(id, cancellationToken);

    public async Task<SettingsWriteResult<Guid>> SaveAsync(SecretEditorModel command, CancellationToken cancellationToken) {
        if (!IsCurrent) {
            return new(SettingsWriteState.Refused, command.Id ?? Guid.Empty, SettingsDiagnostic.Retired);
        }
        try {
            var result = await secrets.SaveEditorAsync(command, cancellationToken);
            return result.IsSuccess ? new(SettingsWriteState.Committed, result.Value)
                : new(SettingsWriteState.Refused, command.Id ?? Guid.Empty, SettingsDiagnostic.Validation);
        } catch (SecretCommittedException committed) {
            return Known(committed);
        }
    }

    public async Task<SettingsWriteResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken) {
        if (!IsCurrent) {
            return new(SettingsWriteState.Refused, id, SettingsDiagnostic.Retired);
        }
        try {
            await secrets.DeleteAsync(id, cancellationToken);
            return new(SettingsWriteState.Committed, id);
        } catch (SecretDeletionBlockedException) {
            return new(SettingsWriteState.Refused, id, SettingsDiagnostic.ReferenceBlocked);
        } catch (SecretCommittedException committed) {
            return Known(committed);
        }
    }

    private static SettingsWriteResult<Guid> Known(SecretCommittedException committed) => new(
        SettingsWriteState.CommittedWarning, committed.SecretId,
        committed.Stage == SecretCommittedStage.Metadata ? SettingsDiagnostic.CleanupPending : SettingsDiagnostic.ActivityPending);
}
