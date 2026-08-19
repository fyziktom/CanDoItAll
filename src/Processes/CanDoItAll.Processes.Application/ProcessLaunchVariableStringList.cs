using System.Text.Json;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Application;

public static class ProcessLaunchVariableStringList
{
    public static bool TryParse(
        string? value,
        out IReadOnlyList<string> items)
    {
        items = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<string[]>(trimmed);
                if (parsed is null)
                {
                    return false;
                }

                items = Normalize(parsed);
                return items.Count > 0;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        items = Normalize(trimmed.Split(
            [';', ',', '\n', '\r'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return items.Count > 0;
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string?> values)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .ToArray();
}
