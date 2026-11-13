using System.ComponentModel;
using System.Text.Json.Serialization;

namespace CanDoItAll.SharedKernel;

[Description("Application theme: 0 Light, 1 Dark.")]
public enum UiThemeMode {
    Light,
    Dark
}

[Description("Personal UI preferences. New users start with the light theme and minimized main menu.")]
public sealed record UserUiPreferences(
    [property: Description("Saved application theme.")] UiThemeMode Theme = UiThemeMode.Light,
    [property: Description("Whether the main navigation is minimized.")] bool MainMenuCollapsed = true) {
    public static UserUiPreferences Default { get; } = new();

    public void Validate() {
        if (!Enum.IsDefined(Theme)) {
            throw new ArgumentOutOfRangeException(nameof(Theme), "The UI theme is unsupported.");
        }
    }

    public UserUiPreferences Apply(UserUiPreferencesPatch patch) {
        ArgumentNullException.ThrowIfNull(patch);
        Validate();
        patch.Validate();
        return new(patch.Theme ?? Theme, patch.MainMenuCollapsed ?? MainMenuCollapsed);
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Description("A personal UI preference patch. Omitted or null fields retain their saved values.")]
public sealed record UserUiPreferencesPatch(
    [property: Description("New application theme, or null to retain the saved theme.")] UiThemeMode? Theme = null,
    [property: Description("New minimized-main-menu state, or null to retain the saved state.")] bool? MainMenuCollapsed = null) {
    public void Validate() {
        if (Theme is { } theme && !Enum.IsDefined(theme)) {
            throw new ArgumentOutOfRangeException(nameof(Theme), "The UI theme is unsupported.");
        }
    }
}
