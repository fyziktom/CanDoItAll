using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace CanDoItAll.Tests.Unit;

public sealed class BrowserUiPreferencesTests {
    [Fact]
    public void Missing_cookies_use_light_theme_and_minimized_menu() {
        var preferences = BrowserUiPreferences.Read(new DefaultHttpContext().Request.Cookies);

        Assert.Equal(UserUiPreferences.Default, preferences);
    }

    [Theory]
    [InlineData("light", "true", UiThemeMode.Light, true)]
    [InlineData("light", "false", UiThemeMode.Light, false)]
    [InlineData("dark", "true", UiThemeMode.Dark, true)]
    [InlineData("dark", "false", UiThemeMode.Dark, false)]
    public void Valid_cookies_restore_both_preferences(string theme, string collapsed, UiThemeMode expectedTheme, bool expectedCollapsed) {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{BrowserUiPreferences.ThemeCookieName}={theme}; {BrowserUiPreferences.MainMenuCollapsedCookieName}={collapsed}";

        Assert.Equal(new UserUiPreferences(expectedTheme, expectedCollapsed), BrowserUiPreferences.Read(context.Request.Cookies));
    }

    [Fact]
    public void A_theme_cookie_preserves_the_missing_menu_default() {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{BrowserUiPreferences.ThemeCookieName}=dark";

        Assert.Equal(new UserUiPreferences(UiThemeMode.Dark, true), BrowserUiPreferences.Read(context.Request.Cookies));
    }

    [Fact]
    public void A_menu_cookie_preserves_the_missing_theme_default() {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{BrowserUiPreferences.MainMenuCollapsedCookieName}=false";

        Assert.Equal(new UserUiPreferences(UiThemeMode.Light, false), BrowserUiPreferences.Read(context.Request.Cookies));
    }

    [Theory]
    [InlineData(BrowserUiPreferences.ThemeCookieName, "Dark")]
    [InlineData(BrowserUiPreferences.ThemeCookieName, "system")]
    [InlineData(BrowserUiPreferences.ThemeCookieName, "1")]
    [InlineData(BrowserUiPreferences.MainMenuCollapsedCookieName, "False")]
    [InlineData(BrowserUiPreferences.MainMenuCollapsedCookieName, "1")]
    [InlineData(BrowserUiPreferences.MainMenuCollapsedCookieName, "yes")]
    public void Malformed_cookie_values_fail_explicitly(string name, string value) {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{name}={value}";

        Assert.Throws<FormatException>(() => BrowserUiPreferences.Read(context.Request.Cookies));
    }
}
