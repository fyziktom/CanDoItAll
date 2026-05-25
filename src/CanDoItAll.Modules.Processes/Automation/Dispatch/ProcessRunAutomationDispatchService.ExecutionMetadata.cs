using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    internal enum ProcessStepExecutionBoundary
    {
        ArtifactOnly,
        AnalysisDesign,
        DecisionReview,
        ProductMutation,
        RuntimeValidation,
        ExternalAction,
        Recovery
    }

    private sealed record ProcessStepExecutionBoundaryDescriptor(
        ProcessStepExecutionBoundary Boundary,
        AgentWorkspaceToolProfileKind WorkspaceToolProfile,
        bool AllowsProductMutation,
        string Summary);

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
        var executionBoundary = ResolveProcessStepExecutionBoundary(candidate);
        var allowExternalTargetMutation = AllowsExternalTargetMutation(candidate, executionBoundary, projectStructureGroundingSummary);
        var allowedExternalTargetAliases = allowExternalTargetMutation
            ? ResolveMutableExternalTargetAliases(candidate, resolvedExternalTargetAliases)
            : [];
        var browserProofGroundingText = string.Join(
            ' ',
            projectStructureGroundingSummary,
            artifactInspectionGroundingSummary);
        var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [ExecutionInvocationMetadata.ProcessBrowserToolsAllowedMetadataKey] = RequiresConcreteBrowserProof(candidate, browserProofGroundingText),
            [ExecutionInvocationMetadata.ProcessStepExecutionBoundaryMetadataKey] = executionBoundary.Boundary.ToString()
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
            ResolveBoundaryAwareCooperationMetadata(candidate.CooperationMetadata, executionBoundary));
        return ExecutionInvocationMetadata.Build(cooperationMetadataJson, processInvocationPolicy);
    }

    private static AgentProcessCooperationMetadata ResolveBoundaryAwareCooperationMetadata(
        AgentProcessCooperationMetadata cooperationMetadata,
        ProcessStepExecutionBoundaryDescriptor executionBoundary)
    {
        var summary = $"{cooperationMetadata.Summary.Trim()} Execution boundary: {executionBoundary.Summary}";
        return cooperationMetadata with
        {
            WorkspaceToolProfile = executionBoundary.WorkspaceToolProfile,
            Summary = summary.Trim()
        };
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
        ProcessStepExecutionBoundaryDescriptor executionBoundary,
        string? projectStructureGroundingSummary)
    {
        if ((executionBoundary.Boundary is ProcessStepExecutionBoundary.ArtifactOnly or
                ProcessStepExecutionBoundary.AnalysisDesign or
                ProcessStepExecutionBoundary.ProductMutation) &&
            LooksLikeExternalArtifactDestination(candidate, projectStructureGroundingSummary))
        {
            return true;
        }

        return executionBoundary.AllowsProductMutation &&
               (RequiresConcreteImplementationProof(candidate) ||
                ContainsProductRepairIntent(candidate) ||
                IsDotNetSolutionSetupScaffoldMutationStep(candidate) ||
                executionBoundary.Boundary == ProcessStepExecutionBoundary.ProductMutation);
    }

    private static ProcessStepExecutionBoundaryDescriptor ResolveProcessStepExecutionBoundary(DispatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var stepText = CollapsePromptWhitespace(string.Join(
                ' ',
                candidate.StepRun.Title,
                candidate.StepRun.CurrentExecutorName,
                candidate.StepDefinition.Key,
                candidate.StepDefinition.Title,
                candidate.StepDefinition.InputContractSummary,
                candidate.StepDefinition.OutputContractSummary,
                candidate.StepDefinition.EvidenceContractSummary,
                candidate.WorkBrief?.Title,
                candidate.WorkBrief?.WorkBriefText,
                candidate.WorkBrief?.HandoffSummary,
                candidate.WorkBrief?.AssignmentReason,
                candidate.WorkBrief?.ExpectedOutcome,
                candidate.WorkBrief?.EvidenceExpectationSummary,
                string.Join(" ", candidate.ExpectedArtifacts.Select(item => $"{item.ArtifactKind} {item.Title} {item.ValidationRequirementSummary} {item.AllowedFutureUsageSummary}"))))
            .ToLowerInvariant();

        if (candidate.StepRun.StepKind == ProcessStepKind.Subprocess)
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.ExternalAction,
                AgentWorkspaceToolProfileKind.ReadOnly,
                AllowsProductMutation: false,
                "Subprocess parent step observes child process state and records process artifacts.");
        }

        if (IsDotNetSolutionSetupScaffoldMutationStep(candidate))
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.ProductMutation,
                AgentWorkspaceToolProfileKind.SoftwareDevelopment,
                AllowsProductMutation: true,
                "Step is an explicit product scaffold or setup mutation step.");
        }

        if (LooksLikeAnalysisOrDesignBoundary(candidate, stepText))
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.AnalysisDesign,
                AgentWorkspaceToolProfileKind.ArchitectureReview,
                AllowsProductMutation: false,
                "Architecture, planning, scope, or analysis step may create managed process artifacts but not mutate product targets.");
        }

        if (RequiresConcreteBrowserProof(candidate, stepText) || LooksLikeRuntimeValidationBoundary(candidate, stepText))
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.RuntimeValidation,
                AgentWorkspaceToolProfileKind.QualityValidation,
                AllowsProductMutation: false,
                "Validation step may inspect, run, and record evidence but must route defects instead of mutating product targets.");
        }

        if (candidate.StepRun.StepKind is ProcessStepKind.Decision or ProcessStepKind.Approval or ProcessStepKind.Review)
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.DecisionReview,
                AgentWorkspaceToolProfileKind.BusinessAnalysis,
                AllowsProductMutation: false,
                "Decision, approval, or review step may record governed dispositions without product mutation.");
        }

        if (LooksLikeRecoveryBoundary(stepText))
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.Recovery,
                AgentWorkspaceToolProfileKind.SoftwareDevelopment,
                AllowsProductMutation: true,
                "Recovery or repair execution step may perform scoped product mutation.");
        }

        if (RequiresConcreteImplementationProof(candidate) ||
            LooksLikeProductMutationBoundary(candidate, stepText))
        {
            return new ProcessStepExecutionBoundaryDescriptor(
                ProcessStepExecutionBoundary.ProductMutation,
                AgentWorkspaceToolProfileKind.SoftwareDevelopment,
                AllowsProductMutation: true,
                "Implementation step is allowed to mutate grounded product targets.");
        }

        return new ProcessStepExecutionBoundaryDescriptor(
            ProcessStepExecutionBoundary.ArtifactOnly,
            AgentWorkspaceToolProfileKind.BusinessAnalysis,
            AllowsProductMutation: false,
            "Step may create managed process artifacts only.");
    }

    private static bool LooksLikeAnalysisOrDesignBoundary(
        DispatchCandidate candidate,
        string stepText)
    {
        if (candidate.StepRun.StepKind == ProcessStepKind.Start)
        {
            return true;
        }

        if (candidate.StepRun.StepKind == ProcessStepKind.Work &&
            LooksLikeProductMutationBoundary(candidate, stepText))
        {
            return false;
        }

        if (candidate.ExpectedArtifacts.Any(item => item.ArtifactKind is ProcessArtifactKind.Decision or ProcessArtifactKind.Brief) &&
            ContainsAnyToken(
                stepText,
                "architecture",
                "architect",
                "adr",
                "design",
                "scope",
                "planning",
                "plan",
                "intake",
                "analysis",
                "source-of-truth",
                "source of truth",
                "canonical",
                "boundary",
                "strategy"))
        {
            return true;
        }

        return ContainsAnyToken(
                   stepText,
                   "architecture review",
                   "architecture decision",
                   "design review",
                   "scope packet",
                   "slice boundary",
                   "planning packet",
                   "intake packet",
                   "source-of-truth",
                   "source of truth") &&
               !LooksLikeProductMutationBoundary(candidate, stepText, requireStrongSignal: true);
    }

    private static bool LooksLikeRuntimeValidationBoundary(
        DispatchCandidate candidate,
        string stepText)
    {
        return candidate.StepRun.StepKind == ProcessStepKind.Review &&
               ContainsAnyToken(
                   stepText,
                   "qa",
                   "quality",
                   "validation",
                   "validate",
                   "test",
                   "proof",
                   "browser",
                   "runtime",
                   "smoke",
                   "inspection",
                   "review");
    }

    private static bool LooksLikeRecoveryBoundary(string stepText)
    {
        return ContainsAnyToken(
            stepText,
            "repair implementation",
            "repair step",
            "rework implementation",
            "remediation implementation",
            "fix implementation");
    }

    private static bool LooksLikeProductMutationBoundary(
        DispatchCandidate candidate,
        string stepText,
        bool requireStrongSignal = false)
    {
        var hasMutationVerb = ContainsAnyToken(
            stepText,
            "implement",
            "implementation",
            "build",
            "create",
            "generate",
            "scaffold",
            "code",
            "write product",
            "change product",
            "modify product",
            "repair",
            "fix",
            "rework");
        if (!hasMutationVerb)
        {
            return false;
        }

        if (candidate.ExpectedArtifacts.Any(item => item.ArtifactKind == ProcessArtifactKind.Deliverable))
        {
            return true;
        }

        if (requireStrongSignal)
        {
            return false;
        }

        return candidate.StepRun.StepKind is ProcessStepKind.Work or ProcessStepKind.Delivery;
    }

    private static bool ContainsAnyToken(string text, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
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
