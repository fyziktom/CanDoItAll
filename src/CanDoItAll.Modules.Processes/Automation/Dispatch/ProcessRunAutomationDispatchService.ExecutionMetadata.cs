using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static string BuildProcessInvocationMetadataJson(
        DispatchCandidate candidate,
        ExecutionInvocationPolicy processInvocationPolicy,
        string? projectStructureGroundingSummary,
        string? artifactInspectionGroundingSummary)
    {
        var allowedExternalTargetAliases = ResolveAllowedExternalTargetAliases(
            candidate,
            projectStructureGroundingSummary,
            artifactInspectionGroundingSummary);
        var metadata = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (allowedExternalTargetAliases.Count > 0)
        {
            metadata[ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey] = allowedExternalTargetAliases;
        }

        var readOnlyExternalTargetAliases = ResolveReadOnlyExternalTargetAliases(candidate, allowedExternalTargetAliases);
        if (readOnlyExternalTargetAliases.Count > 0)
        {
            metadata[ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey] = readOnlyExternalTargetAliases;
        }

        var baseMetadataJson = metadata.Count == 0
            ? null
            : JsonSerializer.Serialize(metadata, AgentOutputJson.SerializerOptions);
        return ExecutionInvocationMetadata.Build(baseMetadataJson, processInvocationPolicy);
    }

    private static IReadOnlyList<string> ResolveReadOnlyExternalTargetAliases(
        DispatchCandidate candidate,
        IReadOnlyList<string> allowedExternalTargetAliases)
    {
        if (allowedExternalTargetAliases.Count == 0 ||
            !IsProductReadOnlyValidationStep(candidate))
        {
            return [];
        }

        return allowedExternalTargetAliases;
    }

    private static bool IsProductReadOnlyValidationStep(DispatchCandidate candidate)
    {
        if (RequiresConcreteImplementationProof(candidate) ||
            ContainsProductRepairIntent(candidate))
        {
            return false;
        }

        var stepText = CollapsePromptWhitespace(string.Join(
                ' ',
                candidate.StepRun.Title,
                candidate.StepRun.CurrentExecutorName,
                candidate.WorkBrief?.Title,
                candidate.WorkBrief?.WorkBriefText,
                candidate.WorkBrief?.ExpectedOutcome))
            .ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(stepText))
        {
            return false;
        }

        return RequiresConcreteBrowserProof(candidate) ||
               stepText.Contains("qa", StringComparison.Ordinal) ||
               stepText.Contains("quality", StringComparison.Ordinal) ||
               stepText.Contains("proof", StringComparison.Ordinal) ||
               stepText.Contains("review", StringComparison.Ordinal) ||
               stepText.Contains("security", StringComparison.Ordinal) ||
               stepText.Contains("readiness", StringComparison.Ordinal) ||
               stepText.Contains("approval", StringComparison.Ordinal);
    }

    private static bool ContainsProductRepairIntent(DispatchCandidate candidate)
    {
        var stepText = CollapsePromptWhitespace(string.Join(
                ' ',
                candidate.StepRun.Title,
                candidate.WorkBrief?.Title,
                candidate.WorkBrief?.WorkBriefText,
                candidate.WorkBrief?.ExpectedOutcome,
                candidate.WorkBrief?.AssignmentReason))
            .ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(stepText))
        {
            return false;
        }

        var boundedStepText = $" {stepText} ";
        return boundedStepText.Contains(" repair ", StringComparison.Ordinal) ||
               boundedStepText.Contains(" repairs ", StringComparison.Ordinal) ||
               boundedStepText.Contains(" repaired ", StringComparison.Ordinal) ||
               boundedStepText.Contains(" repairing ", StringComparison.Ordinal) ||
               boundedStepText.Contains(" fix ", StringComparison.Ordinal) ||
               boundedStepText.Contains(" fixes ", StringComparison.Ordinal) ||
               boundedStepText.Contains(" fixed ", StringComparison.Ordinal) ||
               boundedStepText.Contains(" fixing ", StringComparison.Ordinal) ||
               boundedStepText.Contains(" rework ", StringComparison.Ordinal) ||
               stepText.Contains("change requested", StringComparison.Ordinal) ||
               stepText.Contains("changes requested", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ResolveAllowedExternalTargetAliases(
        DispatchCandidate candidate,
        string? projectStructureGroundingSummary,
        string? artifactInspectionGroundingSummary)
    {
        var groundedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddExternalTargetAliasesFromText(groundedAliases, candidate.Run.TriggerReason);
        AddExternalTargetAliasesFromText(groundedAliases, projectStructureGroundingSummary);
        if (groundedAliases.Count > 0)
        {
            return PruneAllowedExternalTargetAliasesForCurrentRun(groundedAliases);
        }

        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in EnumerateCurrentRunExternalTargetSources(
            candidate,
            projectStructureGroundingSummary,
            artifactInspectionGroundingSummary))
        {
            AddExternalTargetAliasesFromText(aliases, source);
        }

        return PruneAllowedExternalTargetAliasesForCurrentRun(aliases);
    }

    internal static IReadOnlyList<string> PruneAllowedExternalTargetAliasesForCurrentRun(IEnumerable<string> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);

        var normalizedAliases = aliases
            .Select(NormalizeExternalTargetAlias)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Where(alias => alias.StartsWith(ExternalTargetAliasRoot + "/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalizedAliases
            .Where(alias => !normalizedAliases.Any(other =>
                !string.Equals(alias, other, StringComparison.OrdinalIgnoreCase) &&
                IsExternalTargetAliasAncestor(alias, other)))
            .Where(alias => !normalizedAliases.Any(other =>
                !string.Equals(alias, other, StringComparison.OrdinalIgnoreCase) &&
                IsAmbiguousExternalTargetPrefixAlias(alias, other)))
            .OrderByDescending(alias => alias.Length)
            .ToArray();
    }

    private static bool IsExternalTargetAliasAncestor(string alias, string other)
        => other.StartsWith(alias + "/", StringComparison.OrdinalIgnoreCase);

    private static bool IsAmbiguousExternalTargetPrefixAlias(string alias, string other)
    {
        if (!other.StartsWith(alias, StringComparison.OrdinalIgnoreCase) ||
            other.Length <= alias.Length)
        {
            return false;
        }

        var suffix = other[alias.Length..];
        return suffix[0] != '/' && suffix.Contains('/', StringComparison.Ordinal);
    }

    private static IEnumerable<string?> EnumerateCurrentRunExternalTargetSources(
        DispatchCandidate candidate,
        string? projectStructureGroundingSummary,
        string? artifactInspectionGroundingSummary)
    {
        yield return candidate.Run.Name;
        yield return candidate.Run.TriggerReason;
        yield return projectStructureGroundingSummary;
        yield return artifactInspectionGroundingSummary;

        if (candidate.WorkBrief is not null)
        {
            yield return candidate.WorkBrief.Title;
            yield return candidate.WorkBrief.WorkBriefText;
            yield return candidate.WorkBrief.HandoffSummary;
            yield return candidate.WorkBrief.AssignmentReason;
            yield return candidate.WorkBrief.ExpectedOutcome;
            yield return candidate.WorkBrief.EvidenceExpectationSummary;
        }

        foreach (var expectedArtifact in candidate.ExpectedArtifacts)
        {
            yield return expectedArtifact.Title;
            yield return expectedArtifact.ValidationRequirementSummary;
            yield return expectedArtifact.AllowedFutureUsageSummary;
        }

        foreach (var artifactInput in candidate.ArtifactInputs)
        {
            yield return artifactInput.SourceStepTitle;
            yield return artifactInput.ExpectedArtifactTitle;
            foreach (var artifact in artifactInput.Artifacts)
            {
                yield return artifact.Title;
                yield return artifact.ManagedStoragePath;
                yield return artifact.ReviewSummary;
                yield return artifact.ProvenanceSummary;
            }
        }
    }

    private static void AddExternalTargetAliasesFromText(
        HashSet<string> aliases,
        string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (Match match in WorkspacePathInToolRequestRegex.Matches(text))
        {
            var path = match.Groups["path"].Value;
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (path.StartsWith(ExternalTargetAliasRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                var alias = NormalizeExternalTargetAlias(path);
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    aliases.Add(alias);
                }

                continue;
            }

        }

        foreach (var candidatePath in EnumerateAbsoluteExternalPathCandidates(text))
        {
            if (TryMapAbsoluteExternalPathToAlias(candidatePath, out var mappedAlias))
            {
                aliases.Add(mappedAlias);
            }
        }
    }

    private static bool TryMapAbsoluteExternalPathToAlias(
        string path,
        out string mappedAlias)
    {
        mappedAlias = string.Empty;
        if (!TryNormalizeAbsoluteExternalPathCandidate(path, out var normalizedPath))
        {
            return false;
        }

        var driveLetter = char.ToUpperInvariant(normalizedPath[0]);
        var remainder = normalizedPath.Length == 3
            ? string.Empty
            : CollapseExternalTargetAliasSeparators(normalizedPath[3..]).Trim('/');
        mappedAlias = string.IsNullOrWhiteSpace(remainder)
            ? $"{ExternalTargetAliasRoot}/{driveLetter}"
            : $"{ExternalTargetAliasRoot}/{driveLetter}/{remainder}";
        return true;
    }

    private static string NormalizeExternalTargetAlias(string? alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return string.Empty;
        }

        var normalizedAlias = alias
            .Replace('\\', '/')
            .Trim()
            .TrimEnd('/', '.', ',', ';', ':', ')', ']', '}');
        normalizedAlias = StripInlinePathAnnotations(normalizedAlias)
            .TrimEnd('/', '.', ',', ';', ':', ')', ']', '}');

        return CollapseExternalTargetAliasSeparators(normalizedAlias);
    }

    private static string CollapseExternalTargetAliasSeparators(string value)
    {
        return Regex.Replace(
            value.Replace('\\', '/'),
            "/{2,}",
            "/",
            RegexOptions.CultureInvariant);
    }
}
