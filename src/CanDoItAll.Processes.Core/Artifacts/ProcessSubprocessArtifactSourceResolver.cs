using System.Text.RegularExpressions;

namespace CanDoItAll.Processes.Core.Artifacts;

public static class ProcessSubprocessArtifactSourceResolver
{
    private static readonly Regex SubprocessOutputMappingRegex = new(
        @"\b(?:subprocess[-_\s]*(?:child[-_\s]*)?(?:expectation|artifact|output)[-_\s]*(?:id|key)|child[-_\s]*expectation[-_\s]*id)\s*[:=]\s*[`""']?(?<value>[0-9A-Fa-f-]{32,36})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static ProcessArtifactRecordSnapshot? ResolveSourceArtifact(
        IReadOnlyList<ProcessArtifactRecordSnapshot> childArtifacts,
        IReadOnlyList<ProcessArtifactExpectationSnapshot> parentExpectations,
        ProcessArtifactExpectationSnapshot expectation,
        out string diagnostic)
    {
        var result = DiagnoseSourceArtifact(childArtifacts, parentExpectations, expectation);
        diagnostic = result.Message;

        return result.SourceArtifact;
    }

    public static ProcessSubprocessArtifactSourceDiagnostic DiagnoseSourceArtifact(
        IReadOnlyList<ProcessArtifactRecordSnapshot> childArtifacts,
        IReadOnlyList<ProcessArtifactExpectationSnapshot> parentExpectations,
        ProcessArtifactExpectationSnapshot expectation)
    {
        ArgumentNullException.ThrowIfNull(childArtifacts);
        ArgumentNullException.ThrowIfNull(parentExpectations);

        if (!TryBuildSubprocessOutputMappingIndex(parentExpectations, out var mappingsByParentExpectationId, out var diagnostic))
        {
            return ProcessSubprocessArtifactSourceDiagnostic.None(
                ProcessSubprocessArtifactSourceDiagnosticReason.AmbiguousMapping,
                diagnostic);
        }

        if (mappingsByParentExpectationId.Count > 0)
        {
            if (!mappingsByParentExpectationId.TryGetValue(expectation.Id, out var mapping))
            {
                return ProcessSubprocessArtifactSourceDiagnostic.None(
                    ProcessSubprocessArtifactSourceDiagnosticReason.MissingExplicitMapping,
                    $"Parent artifact expectation '{expectation.Title}' has no explicit subprocess child expectation mapping.");
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
                    return ProcessSubprocessArtifactSourceDiagnostic.FromSource(
                        mappedArtifacts[0],
                        ProcessSubprocessArtifactSourceDiagnosticReason.LegacyTextMapping,
                        mappedArtifacts.Count,
                        "Using legacy subprocess child expectation text mapping from artifact requirement summaries; move this mapping to the explicit subprocess child expectation field.");
                }

                return ProcessSubprocessArtifactSourceDiagnostic.FromSource(
                    mappedArtifacts[0],
                    ProcessSubprocessArtifactSourceDiagnosticReason.LatestEligibleMappedArtifact,
                    mappedArtifacts.Count,
                    string.Empty);
            }

            return ProcessSubprocessArtifactSourceDiagnostic.None(
                ProcessSubprocessArtifactSourceDiagnosticReason.MissingEligibleMappedArtifact,
                $"Subprocess child expectation '{mapping.ChildExpectationId:D}' did not produce an eligible artifact for parent expectation '{expectation.Title}'.");
        }

        var eligibleParentExpectations = parentExpectations
            .Where(IsCompletionProjectionAllowed)
            .ToList();
        var eligibleChildArtifacts = childArtifacts
            .Where(artifact => IsSubprocessSourceArtifactEligible(artifact, expectation))
            .ToList();
        if (eligibleParentExpectations.Count == 1 && eligibleChildArtifacts.Count == 1)
        {
            return ProcessSubprocessArtifactSourceDiagnostic.FromSource(
                eligibleChildArtifacts[0],
                ProcessSubprocessArtifactSourceDiagnosticReason.LegacySameKindFallback,
                eligibleChildArtifacts.Count,
                "Using legacy same-kind subprocess artifact fallback because no explicit subprocess child expectation mapping is configured.");
        }

        if (eligibleParentExpectations.Count > 1 || eligibleChildArtifacts.Count > 1)
        {
            return ProcessSubprocessArtifactSourceDiagnostic.None(
                ProcessSubprocessArtifactSourceDiagnosticReason.AmbiguousFallback,
                "Subprocess artifact projection is ambiguous; explicit subprocess child expectation mapping is required when multiple parent expectations or child artifacts can match.",
                eligibleChildArtifacts.Count);
        }

        return ProcessSubprocessArtifactSourceDiagnostic.None(
            ProcessSubprocessArtifactSourceDiagnosticReason.MissingEligibleMappedArtifact,
            string.Empty,
            eligibleChildArtifacts.Count);
    }

    public static IReadOnlyList<ProcessSubprocessOutputArtifactMapping> ResolveOutputArtifactMappings(
        IReadOnlyList<ProcessArtifactExpectationSnapshot> parentExpectations)
    {
        ArgumentNullException.ThrowIfNull(parentExpectations);

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

    public static bool IsCompletionProjectionAllowed(ProcessArtifactExpectationSnapshot expectation)
    {
        return expectation.TrustRequirement is
            ProcessCoreArtifactTrustRequirement.None or
            ProcessCoreArtifactTrustRequirement.ReviewRequired or
            ProcessCoreArtifactTrustRequirement.ApprovalRequired;
    }

    private static bool TryBuildSubprocessOutputMappingIndex(
        IReadOnlyList<ProcessArtifactExpectationSnapshot> parentExpectations,
        out Dictionary<Guid, ProcessSubprocessOutputArtifactMapping> mappingsByParentExpectationId,
        out string diagnostic)
    {
        mappingsByParentExpectationId = [];
        var parentExpectationIdsByChildExpectationId = new Dictionary<Guid, Guid>();
        var childExpectationIdsByParent = new Dictionary<Guid, HashSet<Guid>>();
        var diagnostics = new List<string>();
        foreach (var mapping in ResolveOutputArtifactMappings(parentExpectations))
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

    private static IReadOnlyList<Guid> ResolveLegacySubprocessChildExpectationIds(ProcessArtifactExpectationSnapshot expectation)
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

    private static bool IsSubprocessSourceArtifactEligible(
        ProcessArtifactRecordSnapshot artifact,
        ProcessArtifactExpectationSnapshot expectation)
    {
        return artifact.ArtifactKind == expectation.ArtifactKind &&
               artifact.SensitivityLevel >= expectation.SensitivityLevel &&
               ProcessArtifactExpectationSatisfactionRules.SatisfiesTrustRequirement(artifact.TrustStatus, expectation.TrustRequirement);
    }
}

public enum ProcessSubprocessArtifactSourceDiagnosticReason
{
    LatestEligibleMappedArtifact,
    LegacyTextMapping,
    LegacySameKindFallback,
    AmbiguousMapping,
    MissingExplicitMapping,
    MissingEligibleMappedArtifact,
    AmbiguousFallback
}

public readonly record struct ProcessSubprocessArtifactSourceDiagnostic(
    ProcessArtifactRecordSnapshot? SourceArtifact,
    ProcessSubprocessArtifactSourceDiagnosticReason Reason,
    string Message,
    int EligibleArtifactCount)
{
    public bool HasSourceArtifact => SourceArtifact is not null;

    public static ProcessSubprocessArtifactSourceDiagnostic FromSource(
        ProcessArtifactRecordSnapshot sourceArtifact,
        ProcessSubprocessArtifactSourceDiagnosticReason reason,
        int eligibleArtifactCount,
        string message)
    {
        return new ProcessSubprocessArtifactSourceDiagnostic(
            sourceArtifact,
            reason,
            message,
            eligibleArtifactCount);
    }

    public static ProcessSubprocessArtifactSourceDiagnostic None(
        ProcessSubprocessArtifactSourceDiagnosticReason reason,
        string message,
        int eligibleArtifactCount = 0)
    {
        return new ProcessSubprocessArtifactSourceDiagnostic(
            null,
            reason,
            message,
            eligibleArtifactCount);
    }
}

public sealed record ProcessSubprocessOutputArtifactMapping(
    Guid ParentExpectationId,
    Guid ChildExpectationId,
    bool IsLegacyTextMapping);
