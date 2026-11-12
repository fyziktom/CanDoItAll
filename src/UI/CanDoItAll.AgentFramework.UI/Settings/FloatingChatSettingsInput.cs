using System.Globalization;

namespace CanDoItAll.AgentFramework.UI.Settings;

public sealed record FloatingChatSettingsInput(string Retention, string ActiveLimit, string PreparedLimit,
    bool Adaptive, string PreparedRetention, DateTimeOffset? UpdatedAtUtc = null) {
    public static FloatingChatSettingsInput From(FloatingChatSettingsValues value) => new(
        value.HiddenActiveChatRetentionMinutes.ToString(CultureInfo.InvariantCulture),
        value.MaximumActiveChats.ToString(CultureInfo.InvariantCulture),
        value.MaximumPreparedAgents.ToString(CultureInfo.InvariantCulture), value.AdaptivePreparationEnabled,
        value.PreparedResourceIdleRetentionMinutes.ToString(CultureInfo.InvariantCulture), value.UpdatedAtUtc);

    public FloatingChatSettingsValues? Parse(DateTimeOffset? version, FloatingChatSettingsLimits limits) =>
        int.TryParse(Retention, NumberStyles.Integer, CultureInfo.InvariantCulture, out var retention) && retention >= 1 && retention <= limits.MaximumRetentionMinutes &&
        int.TryParse(ActiveLimit, NumberStyles.Integer, CultureInfo.InvariantCulture, out var active) && active >= 1 && active <= limits.MaximumActiveChats &&
        int.TryParse(PreparedLimit, NumberStyles.Integer, CultureInfo.InvariantCulture, out var prepared) && prepared >= 0 && prepared <= limits.MaximumPreparedAgents &&
        int.TryParse(PreparedRetention, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idle) && idle >= 1 && idle <= limits.MaximumRetentionMinutes
            ? new(retention, active, prepared, Adaptive, idle, version) : null;

    public static SettingsDraftState<FloatingChatSettingsInput> CreateEditor() => new(value => value with { UpdatedAtUtc = null }, Fields);

    private static IReadOnlyList<SettingsMergeField<FloatingChatSettingsInput>> Fields(FloatingChatSettingsInput b, FloatingChatSettingsInput l, FloatingChatSettingsInput r) => [
        new SettingsMergeField<FloatingChatSettingsInput, string>("Hidden chat retention", b, l, r, value => value.Retention, (value, field) => value with { Retention = field }),
        new SettingsMergeField<FloatingChatSettingsInput, string>("Maximum active chats", b, l, r, value => value.ActiveLimit, (value, field) => value with { ActiveLimit = field }),
        new SettingsMergeField<FloatingChatSettingsInput, string>("Maximum prepared agents", b, l, r, value => value.PreparedLimit, (value, field) => value with { PreparedLimit = field }),
        new SettingsMergeField<FloatingChatSettingsInput, bool>("Adaptive preparation", b, l, r, value => value.Adaptive, (value, field) => value with { Adaptive = field }),
        new SettingsMergeField<FloatingChatSettingsInput, string>("Prepared metadata retention", b, l, r, value => value.PreparedRetention, (value, field) => value with { PreparedRetention = field })
    ];
}
