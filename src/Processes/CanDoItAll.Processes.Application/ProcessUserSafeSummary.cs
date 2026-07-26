using System.Text.Json;

namespace CanDoItAll.Processes.Application;

internal static class ProcessUserSafeSummary
{
    public const int MaximumRecoveryContextLength = 900;

    public static string NormalizeForRecoveryContext(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= MaximumRecoveryContextLength
            ? normalized
            : normalized[..(MaximumRecoveryContextLength - 3)] + "...";
    }

    public static string QuoteForRecoveryContext(string? value)
    {
        var normalized = NormalizeForRecoveryContext(value);
        return normalized.Length == 0
            ? string.Empty
            : JsonSerializer.Serialize(normalized);
    }
}
