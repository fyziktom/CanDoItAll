namespace CanDoItAll.Modules.Processes;

internal static class ProcessIncompleteImplementationSignalRules
{
    public static string ResolveIncompleteImplementationSummary(
        bool requiresConcreteImplementationProof,
        string? responseText)
    {
        if (!requiresConcreteImplementationProof || string.IsNullOrWhiteSpace(responseText))
        {
            return string.Empty;
        }

        var normalizedResponse = CollapsePromptWhitespace(responseText).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedResponse))
        {
            return string.Empty;
        }

        var defersFeatureImplementation =
            normalizedResponse.Contains("ready for feature implementation", StringComparison.Ordinal) ||
            normalizedResponse.Contains("ready for later feature implementation", StringComparison.Ordinal) ||
            normalizedResponse.Contains("ready for further feature implementation", StringComparison.Ordinal) ||
            normalizedResponse.Contains("next steps for feature implementation", StringComparison.Ordinal) ||
            normalizedResponse.Contains("future feature implementation", StringComparison.Ordinal) ||
            normalizedResponse.Contains("later feature implementation", StringComparison.Ordinal) ||
            (normalizedResponse.Contains("ready for", StringComparison.Ordinal) &&
             normalizedResponse.Contains("implementation", StringComparison.Ordinal) &&
             normalizedResponse.Contains("feature, tests, and migration notes", StringComparison.Ordinal)) ||
            (normalizedResponse.Contains("structured for further", StringComparison.Ordinal) &&
             normalizedResponse.Contains("implementation", StringComparison.Ordinal));

        if (!defersFeatureImplementation &&
            normalizedResponse.Contains("later step", StringComparison.Ordinal) &&
            normalizedResponse.Contains("feature implementation", StringComparison.Ordinal))
        {
            defersFeatureImplementation = true;
        }

        var reportsMissingRequestedBehavior =
            normalizedResponse.Contains("not yet implemented", StringComparison.Ordinal) ||
            normalizedResponse.Contains("still untouched template output", StringComparison.Ordinal) ||
            normalizedResponse.Contains("untouched template output", StringComparison.Ordinal) ||
            (normalizedResponse.Contains("hello, world!", StringComparison.Ordinal) &&
             (normalizedResponse.Contains("still", StringComparison.Ordinal) ||
              normalizedResponse.Contains("template", StringComparison.Ordinal))) ||
            (normalizedResponse.Contains("no required", StringComparison.Ordinal) &&
             normalizedResponse.Contains("present yet", StringComparison.Ordinal)) ||
            (normalizedResponse.Contains("required", StringComparison.Ordinal) &&
             normalizedResponse.Contains("is not present yet", StringComparison.Ordinal));

        var reportsStaticPreviewInsteadOfInteractiveBehavior =
            (normalizedResponse.Contains("static preview", StringComparison.Ordinal) ||
             normalizedResponse.Contains("static layout preview", StringComparison.Ordinal) ||
             normalizedResponse.Contains("layout preview", StringComparison.Ordinal) ||
             normalizedResponse.Contains("layout-only", StringComparison.Ordinal)) &&
            (normalizedResponse.Contains("not full gameplay", StringComparison.Ordinal) ||
             normalizedResponse.Contains("not full game play", StringComparison.Ordinal) ||
             normalizedResponse.Contains("not actual gameplay", StringComparison.Ordinal) ||
             normalizedResponse.Contains("not playable", StringComparison.Ordinal) ||
             normalizedResponse.Contains("not interactive", StringComparison.Ordinal) ||
             normalizedResponse.Contains("no gameplay", StringComparison.Ordinal) ||
             normalizedResponse.Contains("without gameplay", StringComparison.Ordinal) ||
             normalizedResponse.Contains("avoided gameplay", StringComparison.Ordinal) ||
             normalizedResponse.Contains("does not implement gameplay", StringComparison.Ordinal) ||
             normalizedResponse.Contains("does not include gameplay", StringComparison.Ordinal) ||
             normalizedResponse.Contains("not full application behavior", StringComparison.Ordinal) ||
             normalizedResponse.Contains("no input-driven state", StringComparison.Ordinal) ||
             normalizedResponse.Contains("no input driven state", StringComparison.Ordinal));

        if (reportsStaticPreviewInsteadOfInteractiveBehavior)
        {
            return "the response says the step only produced a static or layout preview and did not implement the requested interactive behavior";
        }

        var reportsDeferredExecution =
            !ContainsNegatedDeferredExecutionPhrase(normalizedResponse) &&
            (normalizedResponse.Contains("next required actions", StringComparison.Ordinal) ||
             normalizedResponse.Contains("next implementation steps", StringComparison.Ordinal) ||
             normalizedResponse.Contains("for the next agent or step", StringComparison.Ordinal) ||
             normalizedResponse.Contains("proceeding to implement", StringComparison.Ordinal));

        return defersFeatureImplementation || reportsMissingRequestedBehavior || reportsDeferredExecution
            ? "the response says the step only scaffolded the app and left the requested feature implementation for later work"
            : string.Empty;
    }

    private static bool ContainsNegatedDeferredExecutionPhrase(string normalizedResponse)
    {
        var phrases = new[]
        {
            "no next required actions",
            "no next implementation steps",
            "no further implementation steps",
            "no remaining implementation steps",
            "no implementation steps remain",
            "no follow-up implementation steps",
            "no deferred implementation steps",
            "no later implementation steps"
        };

        return phrases.Any(phrase => normalizedResponse.Contains(phrase, StringComparison.Ordinal));
    }

    private static string CollapsePromptWhitespace(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
