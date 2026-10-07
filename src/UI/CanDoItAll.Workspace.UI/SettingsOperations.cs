using CanDoItAll.Modules.Workspace;

namespace CanDoItAll.Workspace.UI;

public enum SettingsAction { SaveDefaults, SaveSecret, DeleteSecret, SaveFile, DeleteFile, UpdatePolicy, ShortenRetention }
public enum SettingsEffect { Pending, Refused, Committed, CommittedWarning, Unknown }

public sealed class SettingsReceipt(Guid origin, SettingsAction action, Guid? recordId = null, WorkspaceFileExtension? extension = null) {
    public Guid Id { get; } = Guid.NewGuid();
    public Guid Origin { get; } = origin;
    public SettingsAction Action { get; } = action;
    public Guid? RecordId { get; set; } = recordId;
    public WorkspaceFileExtension? Extension { get; } = extension;
    public SettingsEffect Effect { get; set; } = SettingsEffect.Pending;
    public SettingsDiagnostic Diagnostic { get; set; }
    public long? PolicyVersion { get; set; }
    public bool IsReviewing { get; set; }
    public bool IsReconciling { get; set; }
    public Guid? ObservedRecordId { get; set; }
    public bool? ObservedExists { get; set; }
    public bool ObservationFailed { get; set; }
    public bool BlocksDispatch => IsReconciling || Effect is SettingsEffect.Pending or SettingsEffect.Unknown;
}

public sealed class SettingsOperationLedger {
    public const int Capacity = 16;
    private readonly List<SettingsReceipt> entries = [];
    public IReadOnlyList<SettingsReceipt> Entries => entries;

    public bool Blocks(Guid origin, Guid? recordId = null, WorkspaceFileExtension? extension = null) => entries.Any(item =>
        item.BlocksDispatch && (item.Origin == origin || recordId.HasValue && item.RecordId == recordId || extension.HasValue && item.Extension == extension));

    public SettingsReceipt? Admit(Guid origin, SettingsAction action, Guid? recordId = null, WorkspaceFileExtension? extension = null) {
        if (Blocks(origin, recordId, extension)) {
            return null;
        }
        if (entries.Count == Capacity) {
            var removable = entries.FindIndex(item => !item.BlocksDispatch && !item.IsReviewing);
            if (removable < 0) {
                return null;
            }
            entries.RemoveAt(removable);
        }
        var receipt = new SettingsReceipt(origin, action, recordId, extension);
        entries.Add(receipt);
        return receipt;
    }

    public static void Complete<T>(SettingsReceipt receipt, SettingsWriteResult<T> result) {
        receipt.Effect = result.State switch {
            SettingsWriteState.Refused => SettingsEffect.Refused,
            SettingsWriteState.Committed => SettingsEffect.Committed,
            SettingsWriteState.CommittedWarning => SettingsEffect.CommittedWarning,
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
        receipt.Diagnostic = result.Diagnostic;
    }
}
