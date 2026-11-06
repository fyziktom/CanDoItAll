using CanDoItAll.Modules.Workspace;

namespace CanDoItAll.Workspace.UI;

public static class SettingsReceiptText {
    public static string Diagnostic(SettingsDiagnostic diagnostic) => diagnostic switch {
        SettingsDiagnostic.None => string.Empty,
        SettingsDiagnostic.Validation => "Check the submitted values.",
        SettingsDiagnostic.Missing => "The selected record is missing.",
        SettingsDiagnostic.ReferenceBlocked => "Existing references prevent deletion.",
        SettingsDiagnostic.Conflict => "This secret changed after it was opened. Select it again to review its current metadata.",
        SettingsDiagnostic.CleanupPending => "Saved; payload cleanup needs attention.",
        SettingsDiagnostic.ActivityPending => "Saved; activity recording needs attention.",
        SettingsDiagnostic.LoggingPending => "Saved; diagnostic logging needs attention.",
        SettingsDiagnostic.Retired => "The original editor lifetime has ended.",
        SettingsDiagnostic.ReadBackPending => "Current state could not be refreshed.",
        SettingsDiagnostic.Unknown => "The write outcome is unknown. Observe current state before taking further action.",
        _ => throw new ArgumentOutOfRangeException(nameof(diagnostic))
    };
}
