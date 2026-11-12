using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceFilesOwner(IFileApplicationPreferenceService preferences) : IWorkspaceFilesOwner {
    public WorkspaceFileCommand Capture(string extension, string executablePath) => new(new(new FileApplicationExtension(extension).Value), executablePath);

    public async Task<IReadOnlyList<WorkspaceFilePreference>> ListAsync(CancellationToken cancellationToken) =>
        (await preferences.ListAsync(cancellationToken)).Select(item => new WorkspaceFilePreference(
            new(item.Extension.Value), item.ExecutablePath, item.RequiresRebind)).ToArray();

    public async Task<SettingsWriteResult<WorkspaceFileExtension>> SaveAsync(WorkspaceFileCommand command, CancellationToken cancellationToken) {
        try {
            await preferences.SaveAsync(new(new(command.Extension.Value), command.ExecutablePath), cancellationToken);
            return new(SettingsWriteState.Committed, command.Extension);
        } catch (FileApplicationPreferenceCommittedException committed) {
            return new(SettingsWriteState.CommittedWarning, new(committed.Extension.Value), SettingsDiagnostic.LoggingPending);
        } catch (Exception exception) when (exception is ArgumentException or FileNotFoundException) {
            return new(SettingsWriteState.Refused, command.Extension, SettingsDiagnostic.Validation);
        }
    }

    public async Task<SettingsWriteResult<WorkspaceFileExtension>> DeleteAsync(WorkspaceFileExtension extension, CancellationToken cancellationToken) {
        try {
            await preferences.DeleteAsync(new(extension.Value), cancellationToken);
            return new(SettingsWriteState.Committed, extension);
        } catch (FileApplicationPreferenceCommittedException committed) {
            return new(SettingsWriteState.CommittedWarning, new(committed.Extension.Value), SettingsDiagnostic.LoggingPending);
        }
    }
}
