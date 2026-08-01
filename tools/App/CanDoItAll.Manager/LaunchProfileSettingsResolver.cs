using System.Text.Json;

namespace CanDoItAll.Manager;

/* codex-capsule
kind: helper
name: LaunchProfileSettingsResolver
summary: Reads launchSettings.json for the selected launch profile and derives application and runtime probe URLs.
owns: launch-profile-url-resolution
deps: launchSettings.json
risks: missing-profile, malformed-launch-settings
tests: unit:LaunchProfileSettingsResolverTests
inputs: project path, launch profile name
outputs: application urls, readiness probe urls
*/
public static class LaunchProfileSettingsResolver
{
    public static IReadOnlyList<string> ResolveRuntimeProbeUrls(string projectPath, string? launchProfile)
        => ResolveApplicationUrls(projectPath, launchProfile)
            .Select(url => $"{url.TrimEnd('/')}/_dev/runtime")
            .ToArray();

    public static IReadOnlyList<string> ResolveApplicationUrls(string projectPath, string? launchProfile)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return [];
        }

        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath));
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return [];
        }

        var launchSettingsPath = Path.Combine(projectDirectory, "Properties", "launchSettings.json");
        if (!File.Exists(launchSettingsPath))
        {
            return [];
        }

        using var document = JsonDocument.Parse(File.ReadAllText(launchSettingsPath));
        if (!document.RootElement.TryGetProperty("profiles", out var profiles) || profiles.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var profile = TryGetProfile(profiles, launchProfile) ?? TryGetFirstProfileWithUrls(profiles);
        if (profile is null || !profile.Value.TryGetProperty("applicationUrl", out var applicationUrlElement))
        {
            return [];
        }

        var applicationUrl = applicationUrlElement.GetString();
        if (string.IsNullOrWhiteSpace(applicationUrl))
        {
            return [];
        }

        return applicationUrl
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .ToArray();
    }

    private static JsonElement? TryGetProfile(JsonElement profiles, string? launchProfile)
    {
        if (!string.IsNullOrWhiteSpace(launchProfile) && profiles.TryGetProperty(launchProfile, out var selectedProfile))
        {
            return selectedProfile;
        }

        return null;
    }

    private static JsonElement? TryGetFirstProfileWithUrls(JsonElement profiles)
    {
        foreach (var profile in profiles.EnumerateObject())
        {
            if (profile.Value.TryGetProperty("applicationUrl", out _))
            {
                return profile.Value;
            }
        }

        return null;
    }
}
