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
        var resolvedExternalTargetAliases = ResolveExternalTargetAliases(
            candidate,
            projectStructureGroundingSummary,
            artifactInspectionGroundingSummary);
        var allowExternalTargetMutation = AllowsExternalTargetMutation(candidate, projectStructureGroundingSummary);
        var allowedExternalTargetAliases = allowExternalTargetMutation
            ? ResolveMutableExternalTargetAliases(candidate, resolvedExternalTargetAliases)
            : [];
        var browserProofGroundingText = string.Join(
            ' ',
            projectStructureGroundingSummary,
            artifactInspectionGroundingSummary);
        var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [ExecutionInvocationMetadata.ProcessBrowserToolsAllowedMetadataKey] = RequiresConcreteBrowserProof(candidate, browserProofGroundingText)
        };
        if (IsDotNetSolutionSetupScaffoldMutationStep(candidate))
        {
            metadata[ExecutionInvocationMetadata.ProcessScaffoldToolOnlyMetadataKey] = true;
        }

        if (allowedExternalTargetAliases.Count > 0)
        {
            metadata[ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey] = allowedExternalTargetAliases;
        }

        var readOnlyExternalTargetAliases = ResolveReadOnlyExternalTargetAliases(
            candidate,
            resolvedExternalTargetAliases,
            allowedExternalTargetAliases,
            allowExternalTargetMutation);
        if (readOnlyExternalTargetAliases.Count > 0)
        {
            metadata[ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey] = readOnlyExternalTargetAliases;
        }

        var baseMetadataJson = metadata.Count == 0
            ? null
            : JsonSerializer.Serialize(metadata, AgentOutputJson.SerializerOptions);
        baseMetadataJson = ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
            baseMetadataJson,
            ResolveContextWorkspaceScope(candidate));
        var cooperationMetadataJson = ExecutionInvocationMetadata.ApplyProcessCooperation(
            baseMetadataJson,
            candidate.CooperationMetadata);
        return ExecutionInvocationMetadata.Build(cooperationMetadataJson, processInvocationPolicy);
    }

    private static WorkspaceScopeDescriptor? ResolveContextWorkspaceScope(DispatchCandidate candidate)
    {
        ProcessProjectStructureContextFormatter.TryParse(candidate.Run.TriggerReason, out var projectStructureContext);
        var projectId = projectStructureContext?.ProjectId is { } contextProjectId && contextProjectId != Guid.Empty
            ? contextProjectId
            : candidate.Run.ProjectId;

        return projectId is { } resolvedProjectId && resolvedProjectId != Guid.Empty
            ? WorkspaceScopeDescriptor.Project(resolvedProjectId.ToString("D"))
            : null;
    }

    private static IReadOnlyList<string> ResolveReadOnlyExternalTargetAliases(
        DispatchCandidate candidate,
        IReadOnlyList<string> resolvedExternalTargetAliases,
        IReadOnlyList<string> allowedExternalTargetAliases,
        bool allowExternalTargetMutation)
    {
        if (resolvedExternalTargetAliases.Count == 0)
        {
            return [];
        }

        var scopedExternalTargetAliases = PreferCurrentRunExternalTargetAliases(candidate, resolvedExternalTargetAliases);
        if (allowExternalTargetMutation)
        {
            return scopedExternalTargetAliases
                .Where(IsNonProductExternalTargetAlias)
                .Where(alias => !IsAliasCoveredByAny(alias, allowedExternalTargetAliases))
                .ToArray();
        }

        return IsProductReadOnlyValidationStep(candidate)
            ? scopedExternalTargetAliases
            : [];
    }

    private static bool AllowsExternalTargetMutation(
        DispatchCandidate candidate,
        string? projectStructureGroundingSummary)
        => RequiresConcreteImplementationProof(candidate) ||
           ContainsProductRepairIntent(candidate) ||
           IsDotNetSolutionSetupScaffoldMutationStep(candidate) ||
           LooksLikeExternalArtifactDestination(candidate, projectStructureGroundingSummary);

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
               stepText.Contains("scope", StringComparison.Ordinal) ||
               stepText.Contains("intake", StringComparison.Ordinal) ||
               stepText.Contains("boundary", StringComparison.Ordinal) ||
               stepText.Contains("planning", StringComparison.Ordinal) ||
               stepText.Contains("architecture", StringComparison.Ordinal) ||
               stepText.Contains("architect", StringComparison.Ordinal) ||
               stepText.Contains("source-of-truth", StringComparison.Ordinal) ||
               stepText.Contains("canonical", StringComparison.Ordinal) ||
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

    private static IReadOnlyList<string> ResolveExternalTargetAliases(
        DispatchCandidate candidate,
        string? projectStructureGroundingSummary,
        string? artifactInspectionGroundingSummary)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddExternalTargetAliasesFromText(aliases, candidate.Run.TriggerReason);
        AddExternalTargetAliasesFromText(aliases, projectStructureGroundingSummary);
        foreach (var source in EnumerateCurrentRunExternalTargetSources(
            candidate,
            projectStructureGroundingSummary,
            artifactInspectionGroundingSummary))
        {
            AddExternalTargetAliasesFromText(aliases, source);
        }

        return PruneAllowedExternalTargetAliasesForCurrentRun(aliases);
    }

    private static IReadOnlyList<string> ResolveMutableExternalTargetAliases(
        DispatchCandidate candidate,
        IReadOnlyList<string> aliases)
    {
        var mutableAliases = aliases
            .Where(alias => !IsNonProductExternalTargetAlias(alias))
            .ToList();
        if (mutableAliases.Count == 0)
        {
            return [];
        }

        var preferredAliases = mutableAliases
            .Where(IsPreferredProductExternalTargetAlias)
            .ToList();
        var candidateAliases = preferredAliases.Count > 0 ? preferredAliases : mutableAliases;
        var currentRunTokens = ResolveCurrentRunExternalTargetAliasTokens(candidate);
        if (currentRunTokens.Count > 0)
        {
            var currentRunAliases = candidateAliases
                .Where(alias => currentRunTokens.Any(token => alias.Contains(token, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (currentRunAliases.Count > 0)
            {
                candidateAliases = currentRunAliases;
            }
        }

        return candidateAliases
            .Where(alias => !candidateAliases.Any(other =>
                !string.Equals(alias, other, StringComparison.OrdinalIgnoreCase) &&
                IsExternalTargetAliasAncestor(alias, other) &&
                !IsLikelyExternalTargetFileAlias(other)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(alias => alias.Length)
            .ToArray();
    }

    private static IReadOnlyList<string> PreferCurrentRunExternalTargetAliases(
        DispatchCandidate candidate,
        IReadOnlyList<string> aliases)
    {
        var currentRunTokens = ResolveCurrentRunExternalTargetAliasTokens(candidate);
        if (currentRunTokens.Count == 0)
        {
            return aliases;
        }

        var currentRunAliases = aliases
            .Where(alias => currentRunTokens.Any(token => alias.Contains(token, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        return currentRunAliases.Length > 0
            ? currentRunAliases
            : aliases;
    }

    private static IReadOnlyList<string> ResolveCurrentRunExternalTargetAliasTokens(DispatchCandidate candidate)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddCurrentRunAliasTokens(tokens, candidate.Run.Name);
        AddCurrentRunAliasTokens(tokens, candidate.Run.TriggerReason);
        return tokens.ToArray();
    }

    private static void AddCurrentRunAliasTokens(HashSet<string> tokens, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (Match match in Regex.Matches(
                     text,
                     @"(?<!\d)(?<token>\d{8}[-_]\d{4,6})(?!\d)",
                     RegexOptions.CultureInvariant))
        {
            var token = match.Groups["token"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(token))
            {
                tokens.Add(token);
            }
        }

        foreach (Match match in Regex.Matches(
                     text,
                     @"(?i)\b(?<token>[a-z][a-z0-9]+(?:[-_][a-z0-9]+){2,}[-_]\d{8}[-_]\d{4,6})\b",
                     RegexOptions.CultureInvariant))
        {
            var token = match.Groups["token"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(token))
            {
                tokens.Add(token);
            }
        }
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
            .Where(alias => !IsLikelyExternalTargetFileAlias(alias) ||
                            !normalizedAliases.Any(other =>
                                !string.Equals(alias, other, StringComparison.OrdinalIgnoreCase) &&
                                IsExternalTargetAliasAncestor(other, alias)))
            .Where(alias => IsPreferredProductExternalTargetAlias(alias) ||
                            !normalizedAliases.Any(other =>
                !string.Equals(alias, other, StringComparison.OrdinalIgnoreCase) &&
                IsExternalTargetAliasAncestor(alias, other) &&
                !IsLikelyExternalTargetFileAlias(other)))
            .Where(alias => !normalizedAliases.Any(other =>
                !string.Equals(alias, other, StringComparison.OrdinalIgnoreCase) &&
                IsAmbiguousExternalTargetPrefixAlias(alias, other)))
            .OrderByDescending(alias => alias.Length)
            .ToArray();
    }

    private static bool IsAliasCoveredByAny(string alias, IReadOnlyCollection<string> roots)
        => roots.Any(root =>
            string.Equals(alias, root, StringComparison.OrdinalIgnoreCase) ||
            alias.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));

    private static bool IsPreferredProductExternalTargetAlias(string alias)
    {
        var leaf = alias.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();
        return string.Equals(leaf, "product", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(leaf, "app", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(leaf, "source", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(leaf, "src", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNonProductExternalTargetAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return false;
        }

        var segments = alias.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(segment =>
            string.Equals(segment, "project-structure-backup", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "agent-evidence", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "api-snapshots", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "launch-plan", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "observation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "process-definition", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "process-definition-corrected", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "project-structure-mutations", StringComparison.OrdinalIgnoreCase));
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

    private static bool IsLikelyExternalTargetFileAlias(string alias)
    {
        var lastSlashIndex = alias.LastIndexOf('/');
        if (lastSlashIndex < 0 || lastSlashIndex >= alias.Length - 1)
        {
            return false;
        }

        var leaf = alias[(lastSlashIndex + 1)..];
        return leaf.StartsWith(".", StringComparison.Ordinal) ||
               leaf.Contains('.');
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
        normalizedAlias = StripEscapedLineBreakPathAnnotations(normalizedAlias)
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
