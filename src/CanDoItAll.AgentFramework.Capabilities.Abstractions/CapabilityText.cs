namespace CanDoItAll.AgentFramework.Capabilities.Abstractions;

public static class CapabilityText
{
    public static bool TryParseEnum<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        var normalized = NormalizeEnumText(value);
        foreach (var enumValue in Enum.GetValues<TEnum>())
        {
            if (string.Equals(NormalizeEnumText(enumValue.ToString()), normalized, StringComparison.OrdinalIgnoreCase))
            {
                result = enumValue;
                return true;
            }
        }

        result = default;
        return false;
    }

    public static string ToTemplateText<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var text = value.ToString();
        return string.Concat(text.Select((character, index) =>
            index > 0 && char.IsUpper(character)
                ? "-" + char.ToLowerInvariant(character)
                : char.ToLowerInvariant(character).ToString()));
    }

    private static string NormalizeEnumText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
    }
}
