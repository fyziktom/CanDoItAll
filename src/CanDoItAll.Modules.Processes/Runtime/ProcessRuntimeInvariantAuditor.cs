using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessRuntimeInvariantAuditInput(
    ProcessRun Run,
    IReadOnlyList<ProcessStepRun> StepRuns,
    IReadOnlyList<ProcessArtifactExpectation> ArtifactExpectations,
    IReadOnlyList<ProcessArtifactRecord> ArtifactRecords,
    IReadOnlyList<ProcessJournalEntry> JournalEntries);

internal static class ProcessRuntimeInvariantAuditor
{
    internal const string ManualTransitionValidationFailureCode = "manual-transition-validation-failed";

    private static readonly HashSet<string> AliasConflictCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "alias-conflict",
        "read-only-alias-mutation",
        "product-mutation-without-operation",
        "wrong-root-artifact"
    };

    public static IReadOnlyList<ProcessRuntimeInvariantDiagnosticViewModel> Audit(ProcessRuntimeInvariantAuditInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var stepTitlesById = input.StepRuns.ToDictionary(item => item.Id, item => item.Title);
        var expectationsById = input.ArtifactExpectations.ToDictionary(item => item.Id);
        var diagnostics = new List<ProcessRuntimeInvariantDiagnosticViewModel>();

        diagnostics.AddRange(BuildAliasConflictDiagnostics(input.JournalEntries, stepTitlesById));
        diagnostics.AddRange(BuildWeakArtifactRecordDiagnostics(input.ArtifactRecords, expectationsById, stepTitlesById));
        diagnostics.AddRange(BuildBlockedRecoveryStateDiagnostics(input.Run, input.StepRuns));
        diagnostics.AddRange(BuildDuplicateLineageIdentityDiagnostics(input.ArtifactRecords, stepTitlesById));
        diagnostics.AddRange(BuildManualTransitionValidationFailureDiagnostics(input.JournalEntries, stepTitlesById));

        return diagnostics
            .GroupBy(item => $"{item.Kind}:{item.EvidenceKey}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.ObservedAtUtc).First())
            .OrderByDescending(item => ResolveSeverityRank(item.Severity))
            .ThenByDescending(item => item.ObservedAtUtc)
            .ThenBy(item => item.Kind)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static string BuildManualTransitionValidationFailureEvidenceKey(
        Guid stepRunId,
        ProcessStepRunStatus targetStatus,
        IReadOnlyList<string> errorCodes)
    {
        var normalizedCodes = errorCodes.Count == 0
            ? "unknown"
            : string.Join(
                "+",
                errorCodes
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase));
        var hash = ProcessRecoveryRouter.BuildEvidenceFingerprint(
            ProcessStepBlockReasonCode.RuntimeInvariantViolation,
            ProcessStepBlockCause.RuntimeEvidence,
            $"{stepRunId:D}|{targetStatus}|{normalizedCodes}");
        return $"manual-transition-validation:{hash[..32]}";
    }

    private static IReadOnlyList<ProcessRuntimeInvariantDiagnosticViewModel> BuildAliasConflictDiagnostics(
        IReadOnlyList<ProcessJournalEntry> journalEntries,
        IReadOnlyDictionary<Guid, string> stepTitlesById)
    {
        return journalEntries
            .Where(item => item.EventType == ProcessRuntimeEventTypes.RuntimeInvariantViolationRecorded)
            .Select(item => new
            {
                Entry = item,
                Code = ReadJsonString(item.ReplayContextJson, "Code")
            })
            .Where(item => AliasConflictCodes.Contains(item.Code))
            .Select(item => BuildJournalDiagnostic(
                ProcessRuntimeInvariantDiagnosticKind.AliasConflict,
                item.Entry,
                stepTitlesById,
                "Alias conflict",
                "Review the external target alias, operation contract, and mutation boundary before rerunning this step."))
            .ToList();
    }

    private static IReadOnlyList<ProcessRuntimeInvariantDiagnosticViewModel> BuildWeakArtifactRecordDiagnostics(
        IReadOnlyList<ProcessArtifactRecord> artifactRecords,
        IReadOnlyDictionary<Guid, ProcessArtifactExpectation> expectationsById,
        IReadOnlyDictionary<Guid, string> stepTitlesById)
    {
        var diagnostics = new List<ProcessRuntimeInvariantDiagnosticViewModel>();

        foreach (var artifact in artifactRecords.Where(item => item.ArtifactExpectationId.HasValue))
        {
            if (!expectationsById.TryGetValue(artifact.ArtifactExpectationId!.Value, out var expectation))
            {
                continue;
            }

            var reasons = ResolveWeakArtifactReasons(artifact, expectation);
            if (reasons.Count == 0)
            {
                continue;
            }

            diagnostics.Add(new ProcessRuntimeInvariantDiagnosticViewModel(
                ProcessRuntimeInvariantDiagnosticKind.WeakArtifactRecord,
                ProcessConformanceSeverity.High,
                artifact.StepRunId,
                artifact.StepRunId.HasValue ? stepTitlesById.GetValueOrDefault(artifact.StepRunId.Value, string.Empty) : string.Empty,
                artifact.Id,
                JournalEntryId: null,
                "Weak artifact record",
                $"Artifact '{artifact.Title}' does not satisfy expectation '{expectation.Title}': {string.Join("; ", reasons)}.",
                "Replace or approve the artifact record before using it as completion evidence.",
                $"weak-artifact:{artifact.Id:D}",
                artifact.CreatedAtUtc));
        }

        return diagnostics;
    }

    private static IReadOnlyList<ProcessRuntimeInvariantDiagnosticViewModel> BuildBlockedRecoveryStateDiagnostics(
        ProcessRun run,
        IReadOnlyList<ProcessStepRun> stepRuns)
    {
        return stepRuns
            .Where(item => item.Status is ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Failed)
            .Where(item =>
                item.BlockReasonCode == ProcessStepBlockReasonCode.None ||
                item.NextRecoveryAction == ProcessStepRecoveryOption.None ||
                ProcessStepRunBlockState.ResolveRecoveryOptions(item).Count == 0)
            .Select(item => new ProcessRuntimeInvariantDiagnosticViewModel(
                ProcessRuntimeInvariantDiagnosticKind.BlockedRecoveryState,
                ProcessConformanceSeverity.High,
                item.Id,
                item.Title,
                ArtifactRecordId: null,
                JournalEntryId: null,
                "Blocked recovery state is incomplete",
                $"Step '{item.Title}' is {item.Status} but does not have a complete typed block reason, recovery option set, and next recovery action.",
                "Reclassify the block cause and route a typed recovery action before rerunning or completing this step.",
                $"blocked-recovery:{item.Id:D}:{item.BlockReasonCode}:{item.NextRecoveryAction}",
                item.CompletedAtUtc ?? item.StartedAtUtc ?? run.UpdatedAtUtc))
            .ToList();
    }

    private static IReadOnlyList<ProcessRuntimeInvariantDiagnosticViewModel> BuildDuplicateLineageIdentityDiagnostics(
        IReadOnlyList<ProcessArtifactRecord> artifactRecords,
        IReadOnlyDictionary<Guid, string> stepTitlesById)
    {
        return artifactRecords
            .Select(item => new
            {
                Artifact = item,
                Identity = ResolveProjectionIdentity(item)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Identity))
            .GroupBy(item => item.Identity, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group =>
            {
                var artifacts = group
                    .Select(item => item.Artifact)
                    .OrderBy(item => item.CreatedAtUtc)
                    .ToList();
                var first = artifacts[0];
                var titles = string.Join(", ", artifacts.Select(item => item.Title).Distinct(StringComparer.OrdinalIgnoreCase).Take(3));
                return new ProcessRuntimeInvariantDiagnosticViewModel(
                    ProcessRuntimeInvariantDiagnosticKind.DuplicateLineageIdentity,
                    ProcessConformanceSeverity.High,
                    first.StepRunId,
                    first.StepRunId.HasValue ? stepTitlesById.GetValueOrDefault(first.StepRunId.Value, string.Empty) : string.Empty,
                    first.Id,
                    JournalEntryId: null,
                    "Duplicate projection lineage identity",
                    $"Artifacts share projection identity '{Shorten(group.Key)}' but are stored as separate records: {titles}.",
                    "Deduplicate the artifact records or backfill their projection identity hash before using them as completion evidence.",
                    $"duplicate-lineage:{group.Key}",
                    artifacts.Max(item => item.CreatedAtUtc));
            })
            .ToList();
    }

    private static IReadOnlyList<ProcessRuntimeInvariantDiagnosticViewModel> BuildManualTransitionValidationFailureDiagnostics(
        IReadOnlyList<ProcessJournalEntry> journalEntries,
        IReadOnlyDictionary<Guid, string> stepTitlesById)
    {
        return journalEntries
            .Where(item => item.EventType == ProcessRuntimeEventTypes.RuntimeInvariantViolationRecorded)
            .Where(item => string.Equals(
                ReadJsonString(item.ReplayContextJson, "Code"),
                ManualTransitionValidationFailureCode,
                StringComparison.OrdinalIgnoreCase))
            .Select(item => BuildJournalDiagnostic(
                ProcessRuntimeInvariantDiagnosticKind.ManualTransitionValidationFailure,
                item,
                stepTitlesById,
                "Manual transition validation failure",
                "Refresh the run state, select a valid next status, and satisfy required branch or artifact inputs before retrying."))
            .ToList();
    }

    private static ProcessRuntimeInvariantDiagnosticViewModel BuildJournalDiagnostic(
        ProcessRuntimeInvariantDiagnosticKind kind,
        ProcessJournalEntry entry,
        IReadOnlyDictionary<Guid, string> stepTitlesById,
        string fallbackTitle,
        string fallbackRecommendedAction)
    {
        var severity = Enum.TryParse<ProcessConformanceSeverity>(
            ReadJsonString(entry.ReplayContextJson, "Severity"),
            ignoreCase: true,
            out var parsedSeverity)
            ? parsedSeverity
            : ProcessConformanceSeverity.High;
        var recommendedAction = ReadJsonString(entry.ReplayContextJson, "RecommendedAction");
        return new ProcessRuntimeInvariantDiagnosticViewModel(
            kind,
            severity,
            entry.StepRunId,
            entry.StepRunId.HasValue ? stepTitlesById.GetValueOrDefault(entry.StepRunId.Value, string.Empty) : string.Empty,
            ArtifactRecordId: null,
            entry.Id,
            string.IsNullOrWhiteSpace(entry.Title) ? fallbackTitle : entry.Title,
            entry.Description,
            string.IsNullOrWhiteSpace(recommendedAction) ? fallbackRecommendedAction : recommendedAction,
            string.IsNullOrWhiteSpace(entry.CorrelationId) ? entry.Id.ToString("D") : entry.CorrelationId,
            entry.OccurredAtUtc);
    }

    private static IReadOnlyList<string> ResolveWeakArtifactReasons(
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectation expectation)
    {
        var reasons = new List<string>();
        if (!SatisfiesTrustRequirement(artifact.TrustStatus, expectation.TrustRequirement))
        {
            reasons.Add($"trust status {artifact.TrustStatus} does not satisfy {expectation.TrustRequirement}");
        }

        if (artifact.SensitivityLevel < expectation.SensitivityLevel)
        {
            reasons.Add($"sensitivity {artifact.SensitivityLevel} is below required {expectation.SensitivityLevel}");
        }

        return reasons;
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

    private static string ResolveProjectionIdentity(ProcessArtifactRecord artifact)
    {
        if (!string.IsNullOrWhiteSpace(artifact.ProjectionIdentityHash))
        {
            return artifact.ProjectionIdentityHash.Trim();
        }

        try
        {
            var lineage = ProcessArtifactProjectionLineageJson.Deserialize(artifact.ProjectionLineageJson);
            return ProcessArtifactIdentityService.ComputeProjectionIdentityHash(lineage);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string ReadJsonString(string json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(propertyName))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()?.Trim() ?? string.Empty
                    : property.Value.ToString().Trim();
            }
        }
        catch (JsonException)
        {
        }

        return string.Empty;
    }

    private static int ResolveSeverityRank(ProcessConformanceSeverity severity)
    {
        return severity switch
        {
            ProcessConformanceSeverity.Critical => 4,
            ProcessConformanceSeverity.High => 3,
            ProcessConformanceSeverity.Moderate => 2,
            _ => 1
        };
    }

    private static string Shorten(string value)
    {
        return value.Length <= 24
            ? value
            : value[..24];
    }
}
