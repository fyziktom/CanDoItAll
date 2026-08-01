using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.AgentFramework.Capabilities.Abstractions;

public static class CapabilityNameRules
{
    internal static readonly Regex LowerKebabRegex = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    internal static readonly Regex LowerSnakeRegex = new("^[a-z][a-z0-9_]{0,127}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    internal static readonly Regex McpToolRegex = new("^[A-Za-z0-9_.-]{1,128}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    internal static readonly Regex ImplementationKeyRegex = new("^[a-z0-9]+(?:[._-][a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    internal static readonly Regex PascalIdentifierRegex = new("^[A-Z][A-Za-z0-9]{0,127}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    internal static readonly Regex StableIdRegex = new("^[A-Za-z0-9_.:-]{1,128}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static bool TryCreateKebab(string? value, out CapabilityKey key)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (LowerKebabRegex.IsMatch(normalized))
        {
            key = new CapabilityKey(normalized);
            return true;
        }

        key = default;
        return false;
    }

    internal static string NormalizeLowerKebab(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder(value.Length);
        var pendingSeparator = false;
        foreach (var character in value.Trim())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(character);
                pendingSeparator = false;
            }
            else if (character is >= 'A' and <= 'Z')
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else if (builder.Length > 0)
            {
                pendingSeparator = true;
            }
        }

        return builder.ToString();
    }
}
