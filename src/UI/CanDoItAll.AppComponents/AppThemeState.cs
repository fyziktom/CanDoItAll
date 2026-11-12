using CanDoItAll.Components.BaseLib;
using Microsoft.JSInterop;

namespace CanDoItAll.AppComponents;

/// <summary>
/// Per-circuit theme selection. Owns the app's own JS interop that stamps <c>data-ui-theme</c> and
/// <c>color-scheme</c> directly on <c>document.documentElement</c>, so the whole app (not just a
/// styled subtree) follows the active theme. This is deliberately not routed through BaseLib's
/// <c>ThemeHost</c>, which is a div-scoped styling primitive, not a document-level theme switcher.
/// </summary>
public sealed class AppThemeState(IJSRuntime js)
{
    public string ThemeKey { get; private set; } = CadThemes.Light;

    public event Action? Changed;

    public async Task SetThemeKeyAsync(string themeKey)
    {
        if (string.Equals(ThemeKey, themeKey, StringComparison.Ordinal))
        {
            return;
        }

        ThemeKey = themeKey;
        await js.InvokeVoidAsync("CanDoItAll.appTheme.apply", themeKey);
        Changed?.Invoke();
    }
}
