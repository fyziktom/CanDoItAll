using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal static class WorkflowSubprocessArtifactMapper
{
    private static readonly Regex WorkflowOutputMappingRegex = new(
        @"\b(?:workflow[-_\s]*(?:output|artifact|node)[-_\s]*(?:id|key)|workflowOutputId|workflowNodeId)\s*[:=]\s*[`""']?(?<value>[A-Za-z0-9_.:/-]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SubprocessOutputMappingRegex = new(
        @"\b(?:subprocess[-_\s]*(?:child[-_\s]*)?(?:expectation|artifact|output)[-_\s]*(?:id|key)|child[-_\s]*expectation[-_\s]*id)\s*[:=]\s*[`""']?(?<value>[0-9A-Fa-f-]{32,36})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static ProcessArtifactExpectation? ResolveWorkflowArtifactExpectation(
        IReadOnlyList<ProcessArtifactExpectation> expectations,
        IReadOnlyList<WorkflowArtifactRecord> workflowArtifacts,
        ProcessArtifactKind artifactKind,
        WorkflowArtifactRecord artifact,
        out string diagnostic)
    {
        diagnostic = string.Empty;
        if (!TryResolveWorkflowOutputArtifactMappings(expectations, out var mappings, out diagnostic))
        {
            return null;
        }

        if (mappings.Count > 0)
        {
            var matchedMappings = mappings
                .Where(mapping => mapping.Matches(artifact))
                .ToList();
            var mappedExpectationIds = matchedMappings
                .Select(mapping => mapping.ProcessArtifactExpectationId)
                .Distinct()
                .ToList();
            if (mappedExpectationIds.Count == 1)
            {
                var expectation = expectations.SingleOrDefault(item => item.Id == mappedExpectationIds[0]);
                if (expectation is null)
                {
                    diagnostic = $"Workflow output mapping references missing process artifact expectation '{mappedExpectationIds[0]:D}'.";
                    return null;
                }

                var matchedMapping = matchedMappings[0];
                var matchingArtifactCount = workflowArtifacts.Count(matchedMapping.Matches);
                if (matchingArtifactCount > 1)
                {
                    diagnostic = $"Workflow output mapping for process artifact expectation '{mappedExpectationIds[0]:D}' matches multiple workflow artifacts; configure a more specific workflow output id or name.";
                    return null;
                }

                if (matchedMappings.Any(mapping => mapping.IsLegacyTextMapping))
                {
                    diagnostic = "Using legacy workflow output text mapping from artifact requirement summaries; move this mapping to explicit workflow output fields.";
                }

                return expectation;
            }

            diagnostic = mappedExpectationIds.Count > 1
                ? $"Workflow artifact '{artifact.Id}' matches multiple process artifact expectations through explicit output mappings."
                : $"Workflow artifact '{artifact.Id}' has no explicit workflow output mapping.";
            return null;
        }

        var eligibleExpectations = expectations
            .Where(expectation => expectation.ArtifactKind == artifactKind)
            .ToList();
        var eligibleArtifacts = workflowArtifacts
            .Where(item => MapWorkflowArtifactKind(item.Kind) == artifactKind)
            .ToList();
        if (eligibleExpectations.Count == 1 &&
            eligibleArtifacts.Count == 1 &&
            eligibleArtifacts[0].Id == artifact.Id)
        {
            diagnostic = "Using legacy same-kind workflow artifact fallback because no explicit workflow output mapping is configured.";
            return eligibleExpectations[0];
        }

        if (eligibleExpectations.Count > 1 || eligibleArtifacts.Count > 1)
        {
            diagnostic = "Workflow artifact mapping is ambiguous; explicit workflow output mapping is required when multiple required expectations or workflow artifacts share a process artifact kind.";
        }

        return null;
    }

    public static IReadOnlyList<ProcessWorkflowOutputArtifactMapping> ResolveWorkflowOutputArtifactMappings(
        IReadOnlyList<ProcessArtifactExpectation> expectations)
    {
        var mappings = new List<ProcessWorkflowOutputArtifactMapping>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var expectation in expectations)
        {
            var explicitMapping = CreateExplicitWorkflowOutputArtifactMapping(expectation);
            if (explicitMapping.IsConfigured)
            {
                if (seen.Add($"{expectation.Id:D}|{explicitMapping.MappingKey}"))
                {
                    mappings.Add(explicitMapping);
                }

                continue;
            }

            foreach (var outputId in ResolveLegacyWorkflowOutputIds(expectation))
            {
                var mapping = new ProcessWorkflowOutputArtifactMapping(
                    expectation.Id,
                    outputId,
                    string.Empty,
                    null,
                    IsLegacyTextMapping: true);
                if (seen.Add($"{expectation.Id:D}|{mapping.MappingKey}"))
                {
                    mappings.Add(mapping);
                }
            }
        }

        return mappings;
    }

    public static ProcessArtifactRecord? ResolveSubprocessSourceArtifact(
        IReadOnlyList<ProcessArtifactRecord> childArtifacts,
        IReadOnlyList<ProcessArtifactExpectation> parentExpectations,
        ProcessArtifactExpectation expectation,
        out string diagnostic)
    {
        diagnostic = string.Empty;
        if (!TryBuildSubprocessOutputMappingIndex(parentExpectations, out var mappingsByParentExpectationId, out diagnostic))
        {
            return null;
        }

        if (mappingsByParentExpectationId.Count > 0)
        {
            if (!mappingsByParentExpectationId.TryGetValue(expectation.Id, out var mapping))
            {
                diagnostic = $"Parent artifact expectation '{expectation.Title}' has no explicit subprocess child expectation mapping.";
                return null;
            }

            var mappedArtifacts = childArtifacts
                .Where(artifact =>
                    artifact.ArtifactExpectationId == mapping.ChildExpectationId &&
                    IsSubprocessSourceArtifactEligible(artifact, expectation))
                .OrderByDescending(artifact => artifact.CreatedAtUtc)
                .ToList();
            if (mappedArtifacts.Count > 0)
            {
                if (mapping.IsLegacyTextMapping)
                {
                    diagnostic = "Using legacy subprocess child expectation text mapping from artifact requirement summaries; move this mapping to the explicit subprocess child expectation field.";
                }

                return mappedArtifacts[0];
            }

            diagnostic = $"Subprocess child expectation '{mapping.ChildExpectationId:D}' did not produce an eligible artifact for parent expectation '{expectation.Title}'.";
            return null;
        }

        var eligibleParentExpectations = parentExpectations
            .Where(IsSubprocessCompletionProjectionAllowed)
            .ToList();
        var eligibleChildArtifacts = childArtifacts
            .Where(artifact => IsSubprocessSourceArtifactEligible(artifact, expectation))
            .ToList();
        if (eligibleParentExpectations.Count == 1 && eligibleChildArtifacts.Count == 1)
        {
            diagnostic = "Using legacy same-kind subprocess artifact fallback because no explicit subprocess child expectation mapping is configured.";
            return eligibleChildArtifacts[0];
        }

        if (eligibleParentExpectations.Count > 1 || eligibleChildArtifacts.Count > 1)
        {
            diagnostic = "Subprocess artifact projection is ambiguous; explicit subprocess child expectation mapping is required when multiple parent expectations or child artifacts can match.";
        }

        return null;
    }

    public static IReadOnlyList<ProcessSubprocessOutputArtifactMapping> ResolveSubprocessOutputArtifactMappings(
        IReadOnlyList<ProcessArtifactExpectation> parentExpectations)
    {
        var mappings = new List<ProcessSubprocessOutputArtifactMapping>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var expectation in parentExpectations)
        {
            if (expectation.SubprocessChildArtifactExpectationId is { } explicitChildExpectationId &&
                explicitChildExpectationId != Guid.Empty)
            {
                var mapping = new ProcessSubprocessOutputArtifactMapping(
                    expectation.Id,
                    explicitChildExpectationId,
                    IsLegacyTextMapping: false);
                var explicitKey = $"{mapping.ParentExpectationId:D}|{mapping.ChildExpectationId:D}";
                if (seen.Add(explicitKey))
                {
                    mappings.Add(mapping);
                }

                continue;
            }

            foreach (var childExpectationId in ResolveLegacySubprocessChildExpectationIds(expectation))
            {
                var mapping = new ProcessSubprocessOutputArtifactMapping(
                    expectation.Id,
                    childExpectationId,
                    IsLegacyTextMapping: true);
                var key = $"{mapping.ParentExpectationId:D}|{mapping.ChildExpectationId:D}";
                if (seen.Add(key))
                {
                    mappings.Add(mapping);
                }
            }
        }

        return mappings;
    }

    internal static IReadOnlyList<string> ResolveWorkflowArtifactOutputIds(WorkflowArtifactRecord artifact)
    {
        var values = new List<string>
        {
            artifact.Id.Value.ToString("D"),
            artifact.Id.Value.ToString("N")
        };
        if (artifact.NodeId.HasValue)
        {
            values.Add(artifact.NodeId.Value.Value);
        }

        return values
            .Select(NormalizeWorkflowOutputId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static string NormalizeWorkflowOutputId(string value)
    {
        return value
            .Trim()
            .Trim('`', '\'', '"')
            .TrimEnd('.', ',', ';');
    }

    internal static ProcessArtifactKind MapWorkflowArtifactKind(WorkflowArtifactKind artifactKind)
    {
        return artifactKind switch
        {
            WorkflowArtifactKind.Text or WorkflowArtifactKind.Json => ProcessArtifactKind.Deliverable,
            WorkflowArtifactKind.File or WorkflowArtifactKind.Image or WorkflowArtifactKind.Binary => ProcessArtifactKind.Evidence,
            WorkflowArtifactKind.ToolReceipt => ProcessArtifactKind.Transcript,
            _ => ProcessArtifactKind.Other
        };
    }

    private static bool TryResolveWorkflowOutputArtifactMappings(
        IReadOnlyList<ProcessArtifactExpectation> expectations,
        out IReadOnlyList<ProcessWorkflowOutputArtifactMapping> mappings,
        out string diagnostic)
    {
        mappings = ResolveWorkflowOutputArtifactMappings(expectations);
        var diagnostics = new List<string>();
        foreach (var group in mappings
            .GroupBy(mapping => mapping.MappingKey, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(mapping => mapping.ProcessArtifactExpectationId).Distinct().Count() > 1))
        {
            diagnostics.Add($"Workflow output mapping '{group.Key}' maps to multiple process artifact expectations.");
        }

        foreach (var group in mappings
            .GroupBy(mapping => mapping.ProcessArtifactExpectationId)
            .Where(group => group.Count() > 1))
        {
            diagnostics.Add($"Process artifact expectation '{group.Key:D}' maps to multiple workflow outputs.");
        }

        diagnostic = string.Join(" ", diagnostics.Distinct(StringComparer.OrdinalIgnoreCase));
        if (diagnostics.Count == 0)
        {
            return true;
        }

        mappings = [];
        return false;
    }

    private static ProcessWorkflowOutputArtifactMapping CreateExplicitWorkflowOutputArtifactMapping(ProcessArtifactExpectation expectation)
    {
        return new ProcessWorkflowOutputArtifactMapping(
            expectation.Id,
            NormalizeWorkflowOutputId(expectation.WorkflowOutputId),
            NormalizeWorkflowOutputId(expectation.WorkflowOutputName),
            expectation.WorkflowOutputKind,
            IsLegacyTextMapping: false);
    }

    private static IReadOnlyList<string> ResolveLegacyWorkflowOutputIds(ProcessArtifactExpectation expectation)
    {
        var text = string.Join(
            '\n',
            expectation.ValidationRequirementSummary,
            expectation.AllowedFutureUsageSummary);
        return WorkflowOutputMappingRegex
            .Matches(text)
            .Select(match => NormalizeWorkflowOutputId(match.Groups["value"].Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryBuildSubprocessOutputMappingIndex(
        IReadOnlyList<ProcessArtifactExpectation> parentExpectations,
        out Dictionary<Guid, ProcessSubprocessOutputArtifactMapping> mappingsByParentExpectationId,
        out string diagnostic)
    {
        mappingsByParentExpectationId = [];
        var parentExpectationIdsByChildExpectationId = new Dictionary<Guid, Guid>();
        var childExpectationIdsByParent = new Dictionary<Guid, HashSet<Guid>>();
        var diagnostics = new List<string>();
        foreach (var mapping in ResolveSubprocessOutputArtifactMappings(parentExpectations))
        {
            if (!childExpectationIdsByParent.TryGetValue(mapping.ParentExpectationId, out var childExpectationIds))
            {
                childExpectationIds = [];
                childExpectationIdsByParent[mapping.ParentExpectationId] = childExpectationIds;
            }

            childExpectationIds.Add(mapping.ChildExpectationId);
            if (parentExpectationIdsByChildExpectationId.TryGetValue(mapping.ChildExpectationId, out var existingParentExpectationId) &&
                existingParentExpectationId != mapping.ParentExpectationId)
            {
                diagnostics.Add($"Subprocess child expectation '{mapping.ChildExpectationId:D}' maps to multiple parent artifact expectations.");
                continue;
            }

            parentExpectationIdsByChildExpectationId[mapping.ChildExpectationId] = mapping.ParentExpectationId;
            mappingsByParentExpectationId[mapping.ParentExpectationId] = mapping;
        }

        foreach (var item in childExpectationIdsByParent.Where(item => item.Value.Count > 1))
        {
            diagnostics.Add($"Parent artifact expectation '{item.Key:D}' maps to multiple subprocess child expectations.");
        }

        diagnostic = string.Join(" ", diagnostics.Distinct(StringComparer.OrdinalIgnoreCase));
        if (diagnostics.Count == 0)
        {
            return true;
        }

        mappingsByParentExpectationId.Clear();
        return false;
    }

    private static IReadOnlyList<Guid> ResolveLegacySubprocessChildExpectationIds(ProcessArtifactExpectation expectation)
    {
        var text = string.Join(
            '\n',
            expectation.ValidationRequirementSummary,
            expectation.AllowedFutureUsageSummary);
        return SubprocessOutputMappingRegex
            .Matches(text)
            .Select(match => match.Groups["value"].Value.Trim().Trim('`', '\'', '"').TrimEnd('.', ',', ';'))
            .Where(value => Guid.TryParse(value, out _))
            .Select(Guid.Parse)
            .Distinct()
            .ToList();
    }

    internal static bool IsSubprocessCompletionProjectionAllowed(ProcessArtifactExpectation expectation)
    {
        return expectation.TrustRequirement is
            ProcessArtifactTrustRequirement.None or
            ProcessArtifactTrustRequirement.ReviewRequired or
            ProcessArtifactTrustRequirement.ApprovalRequired;
    }

    private static bool IsSubprocessSourceArtifactEligible(
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectation expectation)
    {
        return artifact.ArtifactKind == expectation.ArtifactKind &&
               artifact.SensitivityLevel >= expectation.SensitivityLevel &&
               SatisfiesTrustRequirement(artifact.TrustStatus, expectation.TrustRequirement);
    }

    private static bool SatisfiesTrustRequirement(
        ProcessArtifactTrustStatus trustStatus,
        ProcessArtifactTrustRequirement trustRequirement)
    {
        return trustRequirement switch
        {
            ProcessArtifactTrustRequirement.None => true,
            ProcessArtifactTrustRequirement.ReviewRequired => trustStatus is
                ProcessArtifactTrustStatus.ReviewRequired or
                ProcessArtifactTrustStatus.Approved or
                ProcessArtifactTrustStatus.TrustedSource,
            ProcessArtifactTrustRequirement.HumanApproved => trustStatus == ProcessArtifactTrustStatus.Approved,
            ProcessArtifactTrustRequirement.ApprovalRequired => trustStatus == ProcessArtifactTrustStatus.Approved,
            ProcessArtifactTrustRequirement.TrustedSource => trustStatus == ProcessArtifactTrustStatus.TrustedSource,
            _ => false
        };
    }
}

internal sealed record ProcessWorkflowOutputArtifactMapping(
    Guid ProcessArtifactExpectationId,
    string WorkflowOutputId,
    string WorkflowOutputName,
    WorkflowArtifactKind? WorkflowOutputKind,
    bool IsLegacyTextMapping)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(WorkflowOutputId) ||
        !string.IsNullOrWhiteSpace(WorkflowOutputName) ||
        WorkflowOutputKind.HasValue;

    public string MappingKey => string.Join(
        "|",
        WorkflowSubprocessArtifactMapper.NormalizeWorkflowOutputId(WorkflowOutputId),
        WorkflowSubprocessArtifactMapper.NormalizeWorkflowOutputId(WorkflowOutputName),
        WorkflowOutputKind?.ToString() ?? string.Empty);

    public bool Matches(WorkflowArtifactRecord artifact)
    {
        if (!IsConfigured)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(WorkflowOutputId) &&
            !WorkflowSubprocessArtifactMapper.ResolveWorkflowArtifactOutputIds(artifact).Contains(WorkflowSubprocessArtifactMapper.NormalizeWorkflowOutputId(WorkflowOutputId), StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(WorkflowOutputName) &&
            !string.Equals(WorkflowSubprocessArtifactMapper.NormalizeWorkflowOutputId(artifact.Name), WorkflowSubprocessArtifactMapper.NormalizeWorkflowOutputId(WorkflowOutputName), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !WorkflowOutputKind.HasValue || artifact.Kind == WorkflowOutputKind.Value;
    }
}

internal sealed record ProcessSubprocessOutputArtifactMapping(
    Guid ParentExpectationId,
    Guid ChildExpectationId,
    bool IsLegacyTextMapping);
