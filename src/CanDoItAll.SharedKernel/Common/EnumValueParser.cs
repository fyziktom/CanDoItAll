namespace CanDoItAll.SharedKernel;

public static class EnumValueParser
{
    public static TEnum? ParseNullable<TEnum>(string? value)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalizedValue = Normalize(value);
        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(Normalize(candidate.ToString()), normalizedValue, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    public static TEnum ParseOrDefault<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum
    {
        return ParseNullable<TEnum>(value) ?? fallback;
    }

    private static string Normalize(string value)
    {
        var buffer = new char[value.Length];
        var count = 0;
        foreach (var character in value)
        {
            if (character == '-' || character == '_' || char.IsWhiteSpace(character))
            {
                continue;
            }

            buffer[count] = character;
            count++;
        }

        return new string(buffer, 0, count);
    }
}
