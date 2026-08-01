using System.Text.Json;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessExecutorSpecializationPolicy
{
    internal static IReadOnlyList<string> Resolve(
        IReadOnlyDictionary<string, string> launchVariables)
    {
        if (!launchVariables.TryGetValue(
                ProcessRuntimeLaunchVariables.ExecutorPreferredSpecializationTags,
                out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            var tags = JsonSerializer.Deserialize<string[]>(value);
            if (tags is not null)
            {
                return Normalize(tags);
            }
        }
        catch (JsonException)
        {
        }

        return Normalize(value.Split(
            [',', ';', '|'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string> tags)
        => tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
