using System.Text.Json;

namespace CanDoItAll.Mcp.DotNetWatch.Runtime;

internal static class ProjectLaunchSettingsResolver
{
    public static IReadOnlyList<string> ResolveUrls(string projectPath, string? launchProfile)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || string.IsNullOrWhiteSpace(launchProfile))
        {
            return [];
        }

        var normalizedProjectPath = Path.GetFullPath(projectPath);
        var launchSettingsPath = Path.Combine(Path.GetDirectoryName(normalizedProjectPath) ?? string.Empty, "Properties", "launchSettings.json");
        if (!File.Exists(launchSettingsPath))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(launchSettingsPath));
            if (!document.RootElement.TryGetProperty("profiles", out var profiles) || profiles.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            foreach (var profile in profiles.EnumerateObject())
            {
                if (!string.Equals(profile.Name, launchProfile, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (profile.Value.ValueKind != JsonValueKind.Object ||
                    !profile.Value.TryGetProperty("applicationUrl", out var applicationUrlElement) ||
                    applicationUrlElement.ValueKind != JsonValueKind.String)
                {
                    return [];
                }

                var rawValue = applicationUrlElement.GetString();
                if (string.IsNullOrWhiteSpace(rawValue))
                {
                    return [];
                }

                return rawValue
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(static candidate => Uri.TryCreate(candidate, UriKind.Absolute, out _))
                    .ToArray();
            }
        }
        catch
        {
            return [];
        }

        return [];
    }
}
