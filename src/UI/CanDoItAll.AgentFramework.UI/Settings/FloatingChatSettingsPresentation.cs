namespace CanDoItAll.AgentFramework.UI.Settings;

public sealed record FloatingChatSettingsValues(int HiddenActiveChatRetentionMinutes = 10, int MaximumActiveChats = 12,
    int MaximumPreparedAgents = 0, bool AdaptivePreparationEnabled = true, int PreparedResourceIdleRetentionMinutes = 10,
    DateTimeOffset? UpdatedAtUtc = null);
public sealed record FloatingChatSettingsLimits(int MaximumRetentionMinutes, int MaximumActiveChats, int MaximumPreparedAgents);
public enum FloatingChatSettingsStatus { Loading, Ready, LoadFailed, Saving, Saved, ValidationFailed, SaveFailed, SavedWithRuntimeWarning, Conflict, Unknown, AccessChanged }
public enum FloatingChatSettingsAction { Save, Reload, Compare }
public sealed record FloatingChatSettingsIntent(long Generation, FloatingChatSettingsAction Action, FloatingChatSettingsValues? Submission = null);
public sealed record FloatingChatSettingsPresentation(long Generation = 0, FloatingChatSettingsValues? Source = null,
    FloatingChatSettingsLimits? Limits = null, FloatingChatSettingsStatus Status = FloatingChatSettingsStatus.Loading) {
    public SettingsDraftState<FloatingChatSettingsInput> Editor { get; init; } = FloatingChatSettingsInput.CreateEditor();
    public bool IsBusy => Status is FloatingChatSettingsStatus.Loading or FloatingChatSettingsStatus.Saving;
    public string StatusText => Status switch {
        FloatingChatSettingsStatus.Loading => "Loading",
        FloatingChatSettingsStatus.Ready => "Ready",
        FloatingChatSettingsStatus.LoadFailed => "Floating chat settings could not be loaded.",
        FloatingChatSettingsStatus.Saving => "Saving",
        FloatingChatSettingsStatus.Saved => "Saved",
        FloatingChatSettingsStatus.ValidationFailed => "Review the retention and capacity limits before saving.",
        FloatingChatSettingsStatus.SaveFailed => "Floating chat settings could not be saved.",
        FloatingChatSettingsStatus.SavedWithRuntimeWarning => "Saved with runtime application warning. The current circuit could not apply the settings.",
        FloatingChatSettingsStatus.Conflict => "Saved settings changed. Compare current settings; your draft is retained.",
        FloatingChatSettingsStatus.Unknown => "The save outcome is unconfirmed. Read current settings and review before starting a new save.",
        FloatingChatSettingsStatus.AccessChanged => "Original editing access changed. Your draft is retained; reopen with current access.",
        _ => throw new ArgumentOutOfRangeException(nameof(Status))
    };
    public string StatusTone => Status switch {
        FloatingChatSettingsStatus.LoadFailed or FloatingChatSettingsStatus.SaveFailed or FloatingChatSettingsStatus.ValidationFailed => "danger",
        FloatingChatSettingsStatus.SavedWithRuntimeWarning or FloatingChatSettingsStatus.Conflict or FloatingChatSettingsStatus.Unknown or FloatingChatSettingsStatus.AccessChanged => "warning",
        FloatingChatSettingsStatus.Ready or FloatingChatSettingsStatus.Saved => "success",
        _ => "neutral"
    };
}
