namespace CanDoItAll.Components.BaseLib;

public static class TagTextValueNormalizer
{
    public static IReadOnlyList<string> ParseCsv(string? value, int maxTags = 20)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => Normalize(item))
            .Where(item => item is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(Math.Max(1, maxTags))
            .ToArray();
    }

    public static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? values, int maxTags = 20)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        return values
            .Select(item => Normalize(item))
            .Where(item => item is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(Math.Max(1, maxTags))
            .ToArray();
    }

    public static string ToCsv(IEnumerable<string>? values, int maxTags = 20)
    {
        return string.Join(", ", NormalizeTags(values, maxTags));
    }

    public static string? Normalize(string? value, int maxLength = 48)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var builder = new System.Text.StringBuilder(Math.Min(normalized.Length, maxLength));
        var previousWasSeparator = false;

        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
            }
            else if (character is '-' or '_' or ' ' or '/')
            {
                if (builder.Length > 0 && !previousWasSeparator)
                {
                    builder.Append('-');
                    previousWasSeparator = true;
                }
            }

            if (builder.Length >= maxLength)
            {
                break;
            }
        }

        var result = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(result)
            ? null
            : result;
    }
}
