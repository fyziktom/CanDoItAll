using CanDoItAll.SharedKernel;

namespace CanDoItAll.Web.Infrastructure;

public static class BrowserUiPreferences {
    public const string ThemeCookieName = "cda.ui-theme.v1";
    public const string MainMenuCollapsedCookieName = "cda.ui-main-menu-collapsed.v1";

    public static UserUiPreferences Read(IRequestCookieCollection cookies) {
        ArgumentNullException.ThrowIfNull(cookies);

        var theme = cookies.TryGetValue(ThemeCookieName, out var themeValue)
            ? themeValue switch {
                "light" => UiThemeMode.Light,
                "dark" => UiThemeMode.Dark,
                _ => throw new FormatException("The browser theme preference is malformed.")
            }
            : UserUiPreferences.Default.Theme;
        var collapsed = cookies.TryGetValue(MainMenuCollapsedCookieName, out var collapsedValue)
            ? collapsedValue switch {
                "true" => true,
                "false" => false,
                _ => throw new FormatException("The browser main menu preference is malformed.")
            }
            : UserUiPreferences.Default.MainMenuCollapsed;

        return new UserUiPreferences(theme, collapsed);
    }
}
