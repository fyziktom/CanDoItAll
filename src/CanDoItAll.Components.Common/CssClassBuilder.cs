namespace CanDoItAll.Components.Common;

public static class CssClassBuilder
{
    public static string Join(params string?[] values)
    {
        return string.Join(
            " ",
            values
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!.Trim()));
    }

    public static string? JoinStyles(params string?[] values)
    {
        var merged = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim().TrimEnd(';'))
            .ToArray();

        if (merged.Length == 0)
        {
            return null;
        }

        return string.Join("; ", merged);
    }
}

