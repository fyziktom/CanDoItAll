using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessResponseTextArtifactSatisfactionRules
{
    public static bool IsConversationalNonArtifactResponse(string normalizedResponse)
    {
        if (string.IsNullOrWhiteSpace(normalizedResponse))
        {
            return true;
        }

        var value = normalizedResponse.ToLowerInvariant();
        return value.Contains("ready to help", StringComparison.Ordinal) ||
               value.Contains("please let me know", StringComparison.Ordinal) ||
               value.Contains("let me know what", StringComparison.Ordinal) ||
               value.Contains("what specific", StringComparison.Ordinal) ||
               value.Contains("specific area or step", StringComparison.Ordinal) ||
               value.Contains("how can i help", StringComparison.Ordinal) ||
               value.Contains("i can help with", StringComparison.Ordinal) ||
               value.Contains("provide more details", StringComparison.Ordinal) ||
               value.Contains("please provide", StringComparison.Ordinal) ||
               value.Contains("need more information", StringComparison.Ordinal) ||
               value.Contains("not enough information", StringComparison.Ordinal) ||
               value.Contains("cannot proceed without", StringComparison.Ordinal) ||
               value.Contains("unable to proceed without", StringComparison.Ordinal);
    }

    public static bool CanProjectResponseTextArtifactWithoutDeclaredPath(
        ProcessRunAutomationDispatchService.DispatchArtifactExpectation expectedArtifact)
    {
        return expectedArtifact.ArtifactKind is ProcessArtifactKind.Brief
            or ProcessArtifactKind.Checklist
            or ProcessArtifactKind.Prompt
            or ProcessArtifactKind.Transcript ||
               IsPathlessResponseProjectableDeliverable(expectedArtifact) ||
               IsPathlessResponseProjectableEvidence(expectedArtifact);
    }

    public static string BuildFallbackResponseTextArtifactRelativePath(
        string currentRunManagedArtifactRoot,
        int stepSequence,
        ProcessRunAutomationDispatchService.DispatchArtifactExpectation expectedArtifact)
    {
        var expectedSlug = FileSafeSlugBuilder.Build(expectedArtifact.Title);
        if (string.IsNullOrWhiteSpace(expectedSlug))
        {
            expectedSlug = "artifact";
        }

        return WorkspaceScopeDescriptor.NormalizeRelativePath(
            Path.Combine(
                currentRunManagedArtifactRoot,
                $"{stepSequence + 1:00}-{expectedSlug}.md"));
    }

    private static bool IsPathlessResponseProjectableDeliverable(
        ProcessRunAutomationDispatchService.DispatchArtifactExpectation expectedArtifact)
    {
        if (expectedArtifact.ArtifactKind != ProcessArtifactKind.Deliverable)
        {
            return false;
        }

        var normalizedTitle = CollapsePromptWhitespace(expectedArtifact.Title).ToLowerInvariant();
        var normalizedValidation = CollapsePromptWhitespace(expectedArtifact.ValidationRequirementSummary).ToLowerInvariant();
        return normalizedTitle.Contains("change set", StringComparison.Ordinal) ||
               normalizedValidation.Contains("change set", StringComparison.Ordinal);
    }

    private static bool IsPathlessResponseProjectableEvidence(
        ProcessRunAutomationDispatchService.DispatchArtifactExpectation expectedArtifact)
    {
        if (expectedArtifact.ArtifactKind != ProcessArtifactKind.Evidence)
        {
            return false;
        }

        var normalizedTitle = CollapsePromptWhitespace(expectedArtifact.Title).ToLowerInvariant();
        var normalizedValidation = CollapsePromptWhitespace(expectedArtifact.ValidationRequirementSummary).ToLowerInvariant();
        return normalizedTitle.Contains("note", StringComparison.Ordinal) ||
               normalizedTitle.Contains("review", StringComparison.Ordinal) ||
               normalizedTitle.Contains("evidence index", StringComparison.Ordinal) ||
               normalizedTitle.Contains("result index", StringComparison.Ordinal) ||
               normalizedTitle.Contains("receipt", StringComparison.Ordinal) ||
               normalizedTitle.Contains("handoff", StringComparison.Ordinal) ||
               normalizedTitle.Contains("browser navigation", StringComparison.Ordinal) ||
               normalizedTitle.Contains("console evidence", StringComparison.Ordinal) ||
               normalizedTitle.Contains("evidence pack", StringComparison.Ordinal) ||
               normalizedTitle.Contains("snapshot", StringComparison.Ordinal) ||
               normalizedTitle.Contains("decision record", StringComparison.Ordinal) ||
               normalizedTitle.Contains("handoff packet", StringComparison.Ordinal) ||
               normalizedTitle.Contains("regression", StringComparison.Ordinal) ||
               normalizedValidation.Contains("evidence index", StringComparison.Ordinal) ||
               normalizedValidation.Contains("raw record pointers", StringComparison.Ordinal) ||
               normalizedValidation.Contains("validation evidence", StringComparison.Ordinal) ||
               normalizedValidation.Contains("runtime/api/browser evidence", StringComparison.Ordinal) ||
               normalizedValidation.Contains("accepted issues", StringComparison.Ordinal) ||
               normalizedValidation.Contains("rejected concerns", StringComparison.Ordinal) ||
               normalizedValidation.Contains("residual risk", StringComparison.Ordinal);
    }

    private static string CollapsePromptWhitespace(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
