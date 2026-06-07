using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactExpectationResolver
{
    public static ProcessArtifactExpectationSnapshot? ResolveArtifactExpectation(
        IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts,
        string? projectStructureContractText,
        ProcessAutomationExecutionArtifact artifact,
        string? artifactTextContent = null)
    {
        ArgumentNullException.ThrowIfNull(expectedArtifacts);
        ArgumentNullException.ThrowIfNull(artifact);

        var governedArtifacts = ProcessProjectStructureArtifactPathRules.ResolveProjectStructureRequiredArtifactPaths(
            projectStructureContractText,
            ProcessConcreteProductPathRules.TryMapWorkspacePathForPrompt);
        if (governedArtifacts.Count > 0)
        {
            foreach (var expectedArtifact in expectedArtifacts)
            {
                if (ProcessProjectStructureArtifactPathRules.TryResolveProjectStructureExpectedArtifactPath(
                        expectedArtifact,
                        governedArtifacts,
                        out var governedPath) &&
                    ProcessProjectStructureArtifactPathRules.ArtifactPathMatchesGovernedProjectStructurePath(
                        artifact.RelativePath,
                        governedPath,
                        ProcessConcreteProductPathRules.TryMapWorkspacePathForPrompt))
                {
                    return expectedArtifact;
                }
            }

            var ungovernedExpectedArtifacts = expectedArtifacts
                .Where(item => !ProcessProjectStructureArtifactPathRules.TryResolveProjectStructureExpectedArtifactPath(
                    item,
                    governedArtifacts,
                    out _))
                .ToList();
            if (ungovernedExpectedArtifacts.Count == 0)
            {
                return null;
            }

            var ungovernedMatchedExpectationId = MatchExpectedArtifactId(
                ungovernedExpectedArtifacts,
                artifact,
                artifactTextContent);
            return ungovernedMatchedExpectationId.HasValue
                ? ungovernedExpectedArtifacts.FirstOrDefault(item => item.Id == ungovernedMatchedExpectationId.Value)
                : null;
        }

        var matchedExpectationId = MatchExpectedArtifactId(expectedArtifacts, artifact, artifactTextContent);
        return matchedExpectationId.HasValue
            ? expectedArtifacts.FirstOrDefault(item => item.Id == matchedExpectationId.Value)
            : null;
    }

    public static Guid? MatchExpectedArtifactId(
        IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts,
        ProcessAutomationExecutionArtifact artifact,
        string? artifactTextContent = null)
    {
        if (expectedArtifacts.Count == 0 ||
            ProcessArtifactKindClassificationRules.IsTransientExecutionArtifact(artifact))
        {
            return null;
        }

        var relativePath = artifact.RelativePath.Replace('\\', '/');
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(relativePath);
        var displayName = ProcessArtifactProjectionPlanner.BuildArtifactTitle(artifact);
        var displaySlug = FileSafeSlugBuilder.Build(displayName);
        var fileSlug = FileSafeSlugBuilder.Build(fileNameWithoutExtension);
        var expectedKind = ProcessArtifactKindClassificationRules.ResolveProcessArtifactKind(artifact, null);
        var expectedArtifactsById = expectedArtifacts.ToDictionary(item => item.Id);
        var strongMatchedExpectationId = ProcessArtifactExpectationMatcher.MatchStrongExpectedArtifactId(
            expectedArtifacts,
            expectedKind,
            item => MatchesExpectedArtifact(
                expectedArtifactsById[item.Id],
                artifact,
                relativePath,
                displayName,
                displaySlug,
                fileSlug));
        if (strongMatchedExpectationId.HasValue)
        {
            return strongMatchedExpectationId.Value;
        }

        var validationExpectations = expectedArtifacts.ToList();
        var providerNativeVisualMatches = validationExpectations
            .Select(item => new
            {
                Expectation = item,
                Score = ScoreProviderNativeVisualArtifactExpectation(item, artifact, relativePath, displayName)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Expectation.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (providerNativeVisualMatches.Count == 1 ||
            providerNativeVisualMatches.Count > 1 &&
            providerNativeVisualMatches[0].Score > providerNativeVisualMatches[1].Score)
        {
            return providerNativeVisualMatches[0].Expectation.Id;
        }

        var contentMatches = validationExpectations
            .Where(item => IsManagedNarrativeArtifactFallbackMatch(
                validationExpectations,
                item,
                artifact,
                relativePath,
                displayName,
                artifactTextContent))
            .ToList();
        if (contentMatches.Count == 1)
        {
            return contentMatches[0].Id;
        }

        if (contentMatches.Count > 1)
        {
            var kindMatches = contentMatches
                .Where(item => item.ArtifactKind == expectedKind)
                .ToList();
            if (kindMatches.Count == 1)
            {
                return kindMatches[0].Id;
            }
        }

        return null;
    }

    public static bool WorkspaceWrittenFileMatchesExpectedArtifact(
        IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string path,
        string content)
    {
        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalizedPath) ||
            ShouldIgnoreProductSourceForNarrativeExpectation(expectedArtifact, normalizedPath))
        {
            return false;
        }

        var syntheticArtifact = new ProcessAutomationExecutionArtifact(
            Guid.Empty,
            Guid.Empty,
            "generated-output",
            Path.GetFileNameWithoutExtension(normalizedPath),
            normalizedPath,
            ProcessArtifactKindClassificationRules.GuessContentTypeFromPath(normalizedPath),
            "workspace_write_file",
            "Workspace file written by the agent.",
            DateTimeOffset.MinValue);
        var matchedExpectationId = MatchExpectedArtifactId(expectedArtifacts, syntheticArtifact, content);
        return matchedExpectationId == expectedArtifact.Id;
    }

    private static bool MatchesExpectedArtifact(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        ProcessAutomationExecutionArtifact artifact,
        string relativePath,
        string displayName,
        string displaySlug,
        string fileSlug)
    {
        if (ShouldIgnoreProductSourceForNarrativeExpectation(expectedArtifact, relativePath))
        {
            return false;
        }

        if (ProcessArtifactPathValidationRules.TryExtractExpectedArtifactRelativePath(
                expectedArtifact.ValidationRequirementSummary,
                out var expectedRelativePath))
        {
            return string.Equals(
                ProcessArtifactPathValidationRules.NormalizeManagedRelativePathForComparison(expectedRelativePath),
                ProcessArtifactPathValidationRules.NormalizeManagedRelativePathForComparison(relativePath),
                StringComparison.OrdinalIgnoreCase);
        }

        if (ProcessArtifactProviderNativeVisualValidationRules.IsProviderNativeBrowserOutputArtifact(artifact))
        {
            return false;
        }

        if (string.Equals(expectedArtifact.Title, displayName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var expectedSlug = FileSafeSlugBuilder.Build(expectedArtifact.Title);
        return string.Equals(expectedSlug, displaySlug, StringComparison.Ordinal) ||
               string.Equals(expectedSlug, fileSlug, StringComparison.Ordinal) ||
               relativePath.Contains(expectedSlug, StringComparison.OrdinalIgnoreCase) ||
               ProcessArtifactTextMatchRules.MatchesExpectedArtifactByTitleTokens(
                   expectedArtifact.Title,
                   relativePath,
                   displayName);
    }

    private static int ScoreProviderNativeVisualArtifactExpectation(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        ProcessAutomationExecutionArtifact artifact,
        string relativePath,
        string displayName)
    {
        if (ShouldIgnoreProductSourceForNarrativeExpectation(expectedArtifact, relativePath) ||
            ProcessArtifactPathValidationRules.TryExtractExpectedArtifactRelativePath(
                expectedArtifact.ValidationRequirementSummary,
                out _))
        {
            return 0;
        }

        return ProcessArtifactProviderNativeVisualValidationRules.ScoreProviderNativeVisualArtifactExpectation(
            expectedArtifact,
            artifact,
            relativePath,
            displayName);
    }

    private static bool IsManagedNarrativeArtifactFallbackMatch(
        IReadOnlyList<ProcessArtifactExpectationSnapshot> expectedArtifacts,
        ProcessArtifactExpectationSnapshot expectedArtifact,
        ProcessAutomationExecutionArtifact artifact,
        string relativePath,
        string displayName,
        string? artifactTextContent)
    {
        if (!IsNarrativeEvidenceArtifactExpectation(expectedArtifact) ||
            ProcessArtifactProviderNativeVisualValidationRules.IsProviderNativeBrowserOutputArtifact(artifact) ||
            !IsManagedRunTextArtifactPath(relativePath) ||
            expectedArtifacts.Count(IsNarrativeEvidenceArtifactExpectation) != 1)
        {
            return false;
        }

        var observedText = CollapsePromptWhitespace($"{relativePath} {displayName} {artifactTextContent}").ToLowerInvariant();
        if (!ProcessArtifactTextMatchRules.ContainsNarrativeArtifactSignal(observedText))
        {
            return false;
        }

        var expectedText = CollapsePromptWhitespace(
            $"{expectedArtifact.Title} {expectedArtifact.ValidationRequirementSummary}").ToLowerInvariant();
        return ProcessArtifactTextMatchRules.SharesNarrativeArtifactPurpose(expectedText, observedText);
    }

    private static bool ShouldIgnoreProductSourceForNarrativeExpectation(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string normalizedPath)
    {
        return IsLikelyProductSourceOrProjectFileName(ResolvePromptFileName(normalizedPath)) &&
               IsNarrativeEvidenceArtifactExpectation(expectedArtifact) &&
               !ProcessArtifactPathValidationRules.ExpectedArtifactExplicitlyTargetsPath(expectedArtifact, normalizedPath);
    }

    private static bool IsNarrativeEvidenceArtifactExpectation(ProcessArtifactExpectationSnapshot expectedArtifact)
    {
        var text = CollapsePromptWhitespace($"{expectedArtifact.Title} {expectedArtifact.ValidationRequirementSummary}");
        return text.Contains("change set", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("checklist", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("summary", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("brief", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("report", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("notes", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("evidence", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("rollout", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("migration", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManagedRunTextArtifactPath(string relativePath)
    {
        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(normalizedPath) ||
            !ProcessManagedArtifactPathClassificationRules.IsTextReadableManagedArtifactPath(normalizedPath))
        {
            return false;
        }

        var segments = normalizedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(segment => string.Equals(segment, "process-runs", StringComparison.OrdinalIgnoreCase)) &&
               segments.Any(IsManagedEvidenceRootSegment);
    }

    private static bool IsManagedEvidenceRootSegment(string segment)
    {
        return string.Equals(segment, "artifacts", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "output", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "integration-map", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "data", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolvePromptFileName(string path)
    {
        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return string.Empty;
        }

        var segments = normalizedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length == 0 ? string.Empty : segments[^1];
    }

    private static bool IsLikelyProductSourceOrProjectFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return ProcessConcreteProductPathRules.IsCodeOrProjectExtension(extension);
    }

    private static string CollapsePromptWhitespace(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
