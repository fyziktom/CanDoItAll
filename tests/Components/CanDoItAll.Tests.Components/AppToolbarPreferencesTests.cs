using Bunit;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class AppToolbarPreferencesTests : BunitContext {
    private readonly UserUiPreferencesTestState preferences = new();

    public AppToolbarPreferencesTests() {
        Services.AddScoped<AppToolbarState>();
        Services.AddSingleton<TooltipService>();
        Services.AddSingleton<IUserUiPreferencesState>(preferences);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Theme_toggle_updates_only_theme_and_preserves_the_menu_preference() {
        preferences.Current = new UserUiPreferences(UiThemeMode.Dark, false);
        var cut = Render<AppToolbar>(parameters => parameters.Add(component => component.PageText, "Dashboard"));

        Assert.Equal("true", cut.Find("button[aria-label='Toggle color theme']").GetAttribute("aria-pressed"));
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Toggle color theme']").ClickAsync());

        Assert.Equal(new UserUiPreferencesPatch(Theme: UiThemeMode.Light), Assert.Single(preferences.Updates));
        Assert.Equal(new UserUiPreferences(UiThemeMode.Light, false), preferences.Current);
        cut.WaitForAssertion(() => Assert.Equal("false", cut.Find("button[aria-label='Toggle color theme']").GetAttribute("aria-pressed")));
    }

    [Fact]
    public async Task Owner_notifications_update_theme_and_prevent_edits_until_ready_and_idle() {
        preferences.IsReady = false;
        var cut = Render<AppToolbar>(parameters => parameters.Add(component => component.PageText, "Dashboard"));

        Assert.True(cut.Find("button[aria-label='Toggle color theme']").HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Toggle color theme']").ClickAsync());
        Assert.Empty(preferences.Updates);

        await cut.InvokeAsync(() => {
            preferences.IsReady = true;
            preferences.IsBusy = true;
            preferences.Current = new UserUiPreferences(UiThemeMode.Dark, true);
            preferences.NotifyChanged();
        });
        cut.WaitForAssertion(() => {
            Assert.True(cut.Find("button[aria-label='Toggle color theme']").HasAttribute("disabled"));
            Assert.Equal("true", cut.Find("button[aria-label='Toggle color theme']").GetAttribute("aria-pressed"));
        });

        await cut.InvokeAsync(() => {
            preferences.IsBusy = false;
            preferences.NotifyChanged();
        });
        cut.WaitForAssertion(() => Assert.False(cut.Find("button[aria-label='Toggle color theme']").HasAttribute("disabled")));
    }

    [Fact]
    public async Task A_failed_preference_write_keeps_the_saved_theme_visible_and_disables_further_changes() {
        preferences.Current = new UserUiPreferences(UiThemeMode.Dark, false);
        preferences.Failure = UserUiPreferencesFailure.Save;
        var cut = Render<AppToolbar>(parameters => parameters.Add(component => component.PageText, "Dashboard"));

        Assert.True(cut.Find("button[aria-label='Toggle color theme']").HasAttribute("disabled"));
        Assert.Equal("true", cut.Find("button[aria-label='Toggle color theme']").GetAttribute("aria-pressed"));
        await cut.InvokeAsync(() => cut.Find("button[aria-label='Toggle color theme']").ClickAsync());

        Assert.Empty(preferences.Updates);
        Assert.Equal(new UserUiPreferences(UiThemeMode.Dark, false), preferences.Current);
    }
}
