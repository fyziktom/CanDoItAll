using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Processes.Application;

internal static class ProcessActorIdentityPolicy
{
    internal const int MaximumLength = 128;

    public static string Normalize(string? value, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);

        var sanitized = ProcessPublicReceiptTextPolicy.Sanitize(value).Trim();
        var normalized = new string(sanitized
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.'
                ? character
                : '-')
            .ToArray())
            .Trim('-');
        if (normalized.Length == 0)
        {
            return fallback;
        }

        return normalized.Length <= MaximumLength
            ? normalized
            : normalized[..MaximumLength].TrimEnd('-');
    }
}
