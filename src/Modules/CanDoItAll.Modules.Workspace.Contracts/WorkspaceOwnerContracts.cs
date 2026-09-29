using CanDoItAll.Modules.Security;

namespace CanDoItAll.Modules.Workspace;

public enum WorkspaceSection { Workspace, DataSources, Storage, Files, ProviderHistory, Secrets, Providers, ApiAccess }
public enum SettingsWriteState { Refused, Committed, CommittedWarning }
public enum SettingsDiagnostic { None, Validation, Missing, ReferenceBlocked, CleanupPending, ActivityPending, LoggingPending, Retired, ReadBackPending, Unknown }

public sealed record SettingsWriteResult<T>(SettingsWriteState State, T Value, SettingsDiagnostic Diagnostic = SettingsDiagnostic.None);

public interface IWorkspaceDefaultsOwner {
    bool IsCurrent { get; }
    Task<WorkspaceSettingsModel> ReadAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkspaceProviderOption>> ProvidersAsync(CancellationToken cancellationToken);
    Task<SettingsWriteResult<WorkspaceSettingsModel>> SaveAsync(WorkspaceSettingsModel command, CancellationToken cancellationToken);
}

public interface IWorkspaceSecretsOwner {
    bool IsCurrent { get; }
    Task<IReadOnlyList<SecretListItem>> ListAsync(CancellationToken cancellationToken);
    Task<SecretEditorModel?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<SettingsWriteResult<Guid>> SaveAsync(SecretEditorModel command, CancellationToken cancellationToken);
    Task<SettingsWriteResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public readonly record struct WorkspaceFileExtension(string Value);
public sealed record WorkspaceFilePreference(WorkspaceFileExtension Extension, string ExecutablePath, bool RequiresRebind);
public sealed record WorkspaceFileCommand(WorkspaceFileExtension Extension, string ExecutablePath);

public interface IWorkspaceFilesOwner {
    WorkspaceFileCommand Capture(string extension, string executablePath);
    Task<IReadOnlyList<WorkspaceFilePreference>> ListAsync(CancellationToken cancellationToken);
    Task<SettingsWriteResult<WorkspaceFileExtension>> SaveAsync(WorkspaceFileCommand command, CancellationToken cancellationToken);
    Task<SettingsWriteResult<WorkspaceFileExtension>> DeleteAsync(WorkspaceFileExtension extension, CancellationToken cancellationToken);
}

public sealed class WorkspaceSettingsCommittedException(WorkspaceSettingsModel saved, SettingsDiagnostic diagnostic)
    : Exception("Workspace defaults were committed; a secondary action needs attention.") {
    public WorkspaceSettingsModel Saved { get; } = saved;
    public SettingsDiagnostic Diagnostic { get; } = diagnostic;
}
