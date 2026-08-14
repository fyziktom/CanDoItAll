using System.Text.RegularExpressions;

namespace CanDoItAll.SharedKernel;

public static partial class SensitiveTextRedactor
{
    private static readonly HashSet<string> SensitiveArgumentNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "api-key",
        "api_key",
        "apikey",
        "authorization",
        "password",
        "secret",
        "token"
    };

    public static string Redact(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return SecretAssignmentPattern()
            .Replace(BearerPattern().Replace(input, "Bearer [REDACTED]"), "$1$2[REDACTED]");
    }

    public static IReadOnlyList<string> RedactArguments(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var redacted = new string[arguments.Count];
        var redactNextValue = false;
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index] ?? string.Empty;
            if (redactNextValue)
            {
                redacted[index] = "[REDACTED]";
                redactNextValue = false;
                continue;
            }

            redacted[index] = Redact(argument);
            redactNextValue = IsStandaloneSensitiveOption(argument);
        }

        return redacted;
    }

    public static bool ContainsSecretBearingArguments(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.Any(argument =>
        {
            var candidate = argument?.Trim() ?? string.Empty;
            if (candidate.Length == 0)
            {
                return false;
            }

            if (IsStandaloneSensitiveOption(candidate) ||
                candidate.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase) ||
                candidate.Contains("Authorization:", StringComparison.OrdinalIgnoreCase) ||
                candidate.Contains("Authorization=", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var option = candidate.TrimStart('-', '/');
            var separatorIndex = option.IndexOfAny(['=', ':', ' ', '\t']);
            var optionName = separatorIndex >= 0 ? option[..separatorIndex] : option;
            return IsSensitiveArgumentName(optionName);
        });
    }

    private static bool IsStandaloneSensitiveOption(string argument)
    {
        var trimmed = argument.Trim();
        if (trimmed.Length < 2 || trimmed[0] is not ('-' or '/'))
        {
            return false;
        }

        var name = trimmed.TrimStart('-', '/');
        if (name.Length == 0 || name.IndexOfAny(['=', ':']) >= 0)
        {
            return false;
        }

        return IsSensitiveArgumentName(name);
    }

    private static bool IsSensitiveArgumentName(string name)
    {
        return SensitiveArgumentNames.Contains(name) ||
               SensitiveArgumentNames.Any(sensitiveName =>
                   name.EndsWith($"-{sensitiveName}", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith($"_{sensitiveName}", StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(
        "(?i)(api[_-]?key|token|secret|password|authorization)(\\\"?\\s*[:=]\\s*\\\"?)([^\\s\\\",}]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex SecretAssignmentPattern();

    [GeneratedRegex(
        "(?i)Bearer\\s+[^\\s\\\",}]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex BearerPattern();
}
