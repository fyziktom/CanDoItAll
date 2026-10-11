namespace CanDoItAll.Application.UI.Profiles;

public sealed record DatabaseProfileDisplay(
    Guid Id,
    string DisplayName,
    string Provider,
    string Source,
    string Descriptor,
    bool IsActive,
    bool IsPendingRestartActivation,
    bool IsRuntimeLocked);

public sealed record DatabaseSelectionDisplay(
    string DisplayName,
    string Provider,
    string Source,
    string Resolution,
    string Descriptor,
    string WorkspaceRoot,
    bool IsRuntimeLocked,
    Guid? PendingRestartProfileId,
    string PendingRestartDisplayName) {
    public bool HasPendingRestartActivation => PendingRestartProfileId.HasValue;
}

public readonly record struct DatabaseProfileIntent(Guid OpeningId, Guid ProfileId);

public enum DatabaseProfileMessageTone {
    Error,
    RestartRequired
}

public sealed record DatabaseProfileMessage(string Text, DatabaseProfileMessageTone Tone);

public sealed record DatabaseProfileDialogPresentation(
    Guid OpeningId,
    bool IsOpen,
    bool IsStartupMode,
    DatabaseSelectionDisplay? DatabaseSelection,
    IReadOnlyList<DatabaseProfileDisplay> DatabaseProfiles,
    Guid? SelectedDatabaseProfileId,
    bool DatabaseDialogBusy,
    bool SelectedDialogProfileIsActive,
    bool SelectedDialogProfileIsPendingRestart,
    bool CanSwitchSelectedProfile,
    DatabaseProfileMessage? Message);

public sealed record DatabaseFlyoutDetails(string Server, string Database, string User);
