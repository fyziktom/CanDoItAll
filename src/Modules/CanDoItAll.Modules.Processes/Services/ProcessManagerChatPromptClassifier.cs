namespace CanDoItAll.Modules.Processes;

internal static class ProcessManagerChatPromptClassifier
{
    private static readonly string[] TelemetryPromptTokens =
    [
        "token",
        "tokens",
        "input tokens",
        "cached input tokens",
        "output tokens",
        "usage",
        "runtime telemetry",
        "cost",
        "status",
        "operator action",
        "operator actions",
        "active",
        "completed",
        "attention"
    ];

    private static readonly string[] ArtifactPromptTokens =
    [
        "artifact",
        "artifacts",
        "file",
        "files",
        "screenshot",
        "screenshots",
        "node",
        "nodes",
        "project structure",
        "inspect",
        "open",
        "read",
        "list",
        "show"
    ];

    private static readonly string[] PreloadedContextOnlyPromptTokens =
    [
        "use only",
        "only use",
        "already in this manager chat context",
        "already in this context",
        "preloaded context",
        "pre loaded context",
        "loaded context",
        "do not use tools",
        "dont use tools",
        "without tools",
        "no tools"
    ];

    public static bool ShouldDisableRuntimeTools(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return false;
        }

        var normalized = NormalizePromptText(prompt);
        var asksForTelemetry = TelemetryPromptTokens.Any(token => ContainsPromptToken(normalized, token));
        if (!asksForTelemetry)
        {
            return false;
        }

        if (ArtifactPromptTokens.Any(token => ContainsPromptToken(normalized, token)))
        {
            return false;
        }

        return PreloadedContextOnlyPromptTokens.Any(token => ContainsPromptToken(normalized, token));
    }

    private static bool ContainsPromptToken(string normalizedPrompt, string token)
        => normalizedPrompt.Contains(NormalizePromptText(token), StringComparison.Ordinal);

    private static string NormalizePromptText(string value)
    {
        var normalized = new string(value
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ')
            .ToArray());

        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        return $" {normalized.Trim()} ";
    }
}
