namespace CanDoItAll.Components.BaseLib;

public static class CadThemes
{
    public const string Light = "light";

    public const string Dark = "dark";

    public static string? ResolveColorScheme(string? themeKey)
    {
        return themeKey switch
        {
            Light => "light",
            Dark => "dark",
            _ => null
        };
    }
}
