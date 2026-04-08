using CanDoItAll.Components.BaseLib;

namespace CanDoItAll.Components.Sandbox;

public sealed class SandboxThemeState
{
    private string themeKey = CadThemes.Light;

    public string ThemeKey => themeKey;

    public event Action? Changed;

    public void SetTheme(string value)
    {
        var normalizedTheme = string.Equals(value, CadThemes.Dark, StringComparison.Ordinal)
            ? CadThemes.Dark
            : CadThemes.Light;

        if (string.Equals(themeKey, normalizedTheme, StringComparison.Ordinal))
        {
            return;
        }

        themeKey = normalizedTheme;
        Changed?.Invoke();
    }
}
