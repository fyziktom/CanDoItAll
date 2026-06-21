using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.ArtifactEvidence;

internal static class ArtifactEvidenceDiagnosticRules
{
    public static IReadOnlyList<ProcessDriverDiagnostic> Evaluate(
        ArtifactEvidenceVerificationRequest request,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        var diagnostics = new List<ProcessDriverDiagnostic>();

        AddProjectionLineageDiagnostics(request, primaryEvidence, diagnostics);
        AddProjectionSourceOrderDiagnostics(request, primaryEvidence, diagnostics);
        AddProviderNativeBrowserDiagnostics(request, primaryEvidence, diagnostics);
        AddValidationRequirementDiagnostics(request, primaryEvidence, diagnostics);
        AddProjectionContradictionDiagnostics(request, primaryEvidence, diagnostics);
        AddArtifactSatisfactionDiagnostics(request, primaryEvidence, diagnostics);

        return diagnostics;
    }

    private static void AddProjectionLineageDiagnostics(
        ArtifactEvidenceVerificationRequest request,
        ProcessDriverEvidenceReference primaryEvidence,
        List<ProcessDriverDiagnostic> diagnostics)
    {
        foreach (var lineage in request.ProjectionLineage)
        {
            if (lineage.SourceKind == ProcessCoreArtifactProjectionSourceKind.Unknown)
            {
                diagnostics.Add(CreateMissingEvidenceDiagnostic(
                    "Artifact projection lineage descriptor is missing a known source kind.",
                    primaryEvidence));
            }

            if (!ProcessDriverEvidencePolicy.IsSha256(lineage.ContentHash))
            {
                diagnostics.Add(CreateMissingEvidenceDiagnostic(
                    "Artifact projection lineage descriptor is missing a valid content hash.",
                    primaryEvidence));
            }

            if (!ProcessDriverEvidencePolicy.IsSha256(lineage.ProjectionIdentityHash))
            {
                diagnostics.Add(CreateMissingEvidenceDiagnostic(
                    "Artifact projection lineage descriptor is missing a valid projection identity hash.",
                    primaryEvidence));
            }

            if (!lineage.HasRuntimeSource && !lineage.HasRecordOnlySource && !lineage.HasRecoveryLineage)
            {
                diagnostics.Add(CreateMissingEvidenceDiagnostic(
                    "Artifact projection lineage descriptor is missing runtime, record-only, or recovery source classification.",
                    primaryEvidence));
            }
        }
    }

    private static void AddProjectionSourceOrderDiagnostics(
        ArtifactEvidenceVerificationRequest request,
        ProcessDriverEvidenceReference primaryEvidence,
        List<ProcessDriverDiagnostic> diagnostics)
    {
        foreach (var sourceOrder in request.ProjectionSourceOrder)
        {
            if (sourceOrder.SourceKind == ProcessCoreArtifactProjectionSourceKind.Unknown)
            {
                diagnostics.Add(CreateMissingEvidenceDiagnostic(
                    "Artifact projection source order descriptor is missing a known source kind.",
                    primaryEvidence));
            }

            if (sourceOrder.ProducerKind == ProcessCoreArtifactProducerKind.Unknown)
            {
                diagnostics.Add(CreateMissingEvidenceDiagnostic(
                    "Artifact projection source order descriptor is missing a known producer kind.",
                    primaryEvidence));
            }

            if (sourceOrder.ProjectionOrder <= 0 || sourceOrder.ProjectionOrder == int.MaxValue)
            {
                diagnostics.Add(CreateMissingEvidenceDiagnostic(
                    "Artifact projection source order descriptor is missing a bounded projection order.",
                    primaryEvidence));
            }
        }
    }

    private static void AddProviderNativeBrowserDiagnostics(
        ArtifactEvidenceVerificationRequest request,
        ProcessDriverEvidenceReference primaryEvidence,
        List<ProcessDriverDiagnostic> diagnostics)
    {
        foreach (var evidence in request.ProviderNativeBrowserEvidence)
        {
            if (evidence.EvidenceKind == ProcessCoreProviderNativeBrowserEvidenceKind.Unknown)
            {
                diagnostics.Add(CreateMissingEvidenceDiagnostic(
                    "Provider-native browser evidence descriptor is missing a known evidence kind.",
                    primaryEvidence));
            }

            if (string.IsNullOrWhiteSpace(evidence.ToolName))
            {
                diagnostics.Add(CreateMissingEvidenceDiagnostic(
                    "Provider-native browser evidence descriptor is missing tool metadata.",
                    primaryEvidence));
            }

            if (!evidence.CanSatisfyRequiredArtifact)
            {
                diagnostics.Add(CreateMissingEvidenceDiagnostic(
                    "Provider-native browser evidence descriptor cannot satisfy a required artifact from the supplied metadata.",
                    primaryEvidence));
            }
        }
    }

    private static void AddValidationRequirementDiagnostics(
        ArtifactEvidenceVerificationRequest request,
        ProcessDriverEvidenceReference primaryEvidence,
        List<ProcessDriverDiagnostic> diagnostics)
    {
        foreach (var requirement in request.ValidationRequirements)
        {
            if (requirement.ExpectationId == Guid.Empty)
            {
                diagnostics.Add(CreateMissingEvidenceDiagnostic(
                    "Artifact validation requirement descriptor is missing an expectation id.",
                    primaryEvidence));
            }

            if (string.IsNullOrWhiteSpace(requirement.Title))
            {
                diagnostics.Add(CreateMissingEvidenceDiagnostic(
                    "Artifact validation requirement descriptor is missing title metadata.",
                    primaryEvidence));
            }

            if (requirement.IsRequired &&
                string.IsNullOrWhiteSpace(requirement.ValidationRequirementSummary))
            {
                diagnostics.Add(CreateMissingEvidenceDiagnostic(
                    "Required artifact validation descriptor is missing validation requirement summary metadata.",
                    primaryEvidence));
            }
        }
    }

    private static void AddProjectionContradictionDiagnostics(
        ArtifactEvidenceVerificationRequest request,
        ProcessDriverEvidenceReference primaryEvidence,
        List<ProcessDriverDiagnostic> diagnostics)
    {
        if (request.ProjectionSourceOrder.Count > 0 &&
            !ProcessArtifactProjectionEvidenceDescriptorRules.IsDefaultProjectionOrder(
                request.ProjectionSourceOrder.Select(source => source.SourceKind).ToArray()))
        {
            diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.ProjectionOrderDrift,
                "Artifact projection source order differs from the Core default projection order.",
                primaryEvidence));
        }

        var duplicateSourceKinds = request.ProjectionSourceOrder
            .GroupBy(source => source.SourceKind)
            .Where(group => group.Key != ProcessCoreArtifactProjectionSourceKind.Unknown && group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateSourceKinds.Length > 0)
        {
            diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.ProjectionOrderDrift,
                "Artifact projection source order contains duplicate source kinds.",
                primaryEvidence));
        }

        var lineageSourceKinds = request.ProjectionLineage
            .Select(lineage => lineage.SourceKind)
            .Where(sourceKind => sourceKind != ProcessCoreArtifactProjectionSourceKind.Unknown)
            .ToHashSet();
        var missingLineageSourceKinds = request.ProjectionSourceOrder
            .Select(sourceOrder => sourceOrder.SourceKind)
            .Where(sourceKind => sourceKind != ProcessCoreArtifactProjectionSourceKind.Unknown)
            .Where(sourceKind => !lineageSourceKinds.Contains(sourceKind))
            .Distinct()
            .ToArray();
        if (missingLineageSourceKinds.Length > 0)
        {
            diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.ArtifactLineageMissing,
                "Artifact projection source order references a source kind without supplied lineage evidence.",
                primaryEvidence));
        }
    }

    private static void AddArtifactSatisfactionDiagnostics(
        ArtifactEvidenceVerificationRequest request,
        ProcessDriverEvidenceReference primaryEvidence,
        List<ProcessDriverDiagnostic> diagnostics)
    {
        if (request.ExpectedArtifacts.Count == 0 || request.ArtifactRecords.Count == 0)
        {
            return;
        }

        foreach (var artifact in request.ArtifactRecords)
        {
            var match = ProcessArtifactExpectationMatcher.DiagnoseStrongExpectedArtifactMatch(
                request.ExpectedArtifacts,
                artifact.ArtifactKind,
                expectation =>
                    artifact.ArtifactExpectationId == expectation.Id ||
                    string.Equals(artifact.Title, expectation.Title, StringComparison.OrdinalIgnoreCase));
            if (!match.MatchedArtifactId.HasValue)
            {
                diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                    ProcessDriverDiagnosticSeverity.Error,
                    ProcessDriverDiagnosticCategory.ArtifactSatisfactionInconsistent,
                    "Artifact record does not map to exactly one supplied validation expectation.",
                    primaryEvidence));

                continue;
            }

            var expectation = request.ExpectedArtifacts.First(item => item.Id == match.MatchedArtifactId.Value);
            var satisfaction = ProcessArtifactExpectationSatisfactionRules.Diagnose(artifact, expectation);
            if (satisfaction.IsSatisfied)
            {
                continue;
            }

            diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ResolveSatisfactionCategory(satisfaction.Reason),
                ResolveSatisfactionMessage(satisfaction.Reason),
                primaryEvidence));
        }
    }

    private static ProcessDriverDiagnosticCategory ResolveSatisfactionCategory(
        ProcessArtifactExpectationSatisfactionReason reason)
    {
        return reason is
            ProcessArtifactExpectationSatisfactionReason.SensitivityTooLow or
            ProcessArtifactExpectationSatisfactionReason.TrustRequirementNotSatisfied
            ? ProcessDriverDiagnosticCategory.ArtifactTrustSensitivityMismatch
            : ProcessDriverDiagnosticCategory.ArtifactSatisfactionInconsistent;
    }

    private static string ResolveSatisfactionMessage(
        ProcessArtifactExpectationSatisfactionReason reason)
    {
        return reason switch
        {
            ProcessArtifactExpectationSatisfactionReason.SensitivityTooLow =>
                "Artifact record sensitivity level does not satisfy the supplied validation expectation.",
            ProcessArtifactExpectationSatisfactionReason.TrustRequirementNotSatisfied =>
                "Artifact record trust status does not satisfy the supplied validation expectation.",
            ProcessArtifactExpectationSatisfactionReason.ArtifactKindMismatch =>
                "Artifact record kind does not satisfy the supplied validation expectation.",
            ProcessArtifactExpectationSatisfactionReason.ExpectationIdMismatch =>
                "Artifact record expectation id contradicts the supplied validation expectation.",
            ProcessArtifactExpectationSatisfactionReason.TitleMismatch =>
                "Artifact record title does not satisfy the supplied validation expectation.",
            _ =>
                "Artifact record does not satisfy the supplied validation expectation."
        };
    }

    private static ProcessDriverDiagnostic CreateMissingEvidenceDiagnostic(
        string message,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        return ArtifactEvidenceDiagnosticFactory.Create(
            ProcessDriverDiagnosticSeverity.Warning,
            ProcessDriverDiagnosticCategory.InsufficientProof,
            message,
            primaryEvidence);
    }
}
