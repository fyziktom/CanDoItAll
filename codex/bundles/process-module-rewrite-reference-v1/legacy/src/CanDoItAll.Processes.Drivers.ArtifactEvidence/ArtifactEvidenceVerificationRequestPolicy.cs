using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.ArtifactEvidence;

internal static class ArtifactEvidenceVerificationRequestPolicy
{
    public static ProcessDriverDenialReason Validate(
        ArtifactEvidenceVerificationRequest request,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences,
        List<ProcessDriverDiagnostic> diagnostics,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        var verificationRequest = request.VerificationRequest;
        if (verificationRequest.ContractVersion.Major != ProcessDriverContractVersion.Current.Major)
        {
            diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.UnsupportedContractVersion,
                "Artifact evidence verification requires a compatible process driver contract major version.",
                primaryEvidence));

            return ProcessDriverDenialReason.UnsupportedMode;
        }

        if (verificationRequest.PermissionMode == ProcessDriverPermissionMode.Unspecified)
        {
            diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat,
                "Artifact evidence verification request is missing a permission mode.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingPermissionMode;
        }

        if (!ProcessDriverCapabilityScopeRules.IsArtifactEvidenceReadScope(
            verificationRequest.Scope,
            verificationRequest.PermissionMode))
        {
            diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat,
                "Capability scope is not the read-only artifact evidence verification lane.",
                primaryEvidence));

            return ProcessDriverDenialReason.CapabilityScopeDenied;
        }

        foreach (var operation in verificationRequest.RequestedOperations ?? [])
        {
            if (ProcessDriverOperationRules.IsReadonlyVerificationOperation(operation))
            {
                continue;
            }

            var denialReason = ProcessDriverOperationRules.ResolveReadonlyDenialReason(operation);
            diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.MutationAttemptDenied,
                $"Operation {operation} is denied for verification-only artifact evidence inspection.",
                primaryEvidence));

            return denialReason;
        }

        if ((verificationRequest.EvidenceReferences?.Count ?? 0) == 0 || evidenceReferences.Count == 0)
        {
            diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Artifact evidence verification requires at least one supplied Core artifact descriptor reference.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        var suppliedEvidenceReferences = evidenceReferences
            .Concat([request.SuppliedContent.EvidenceReference])
            .ToArray();
        var uriPolicyResult = ProcessDriverEvidencePolicy.ValidateApprovedSuppliedEvidenceUris(suppliedEvidenceReferences);
        if (!uriPolicyResult.Accepted)
        {
            diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.TranscriptUntrusted,
                "Artifact evidence source is not an approved supplied Core descriptor payload reference.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (!ProcessDriverSuppliedEvidenceContentRules.HasExpectedEnvelope(
            request.SuppliedContent,
            ProcessDriverSuppliedEvidenceContentKind.CoreDescriptorPayload,
            ProcessDriverSuppliedEvidenceContentRules.JsonContentType))
        {
            diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Supplied artifact evidence must use the Core descriptor payload envelope and JSON content type.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (!ProcessDriverSuppliedEvidenceContentRules.HasAllowedSize(request.SuppliedContent) ||
            !ProcessDriverSuppliedEvidenceContentRules.HasValidContentHash(request.SuppliedContent))
        {
            diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Supplied artifact evidence must include a bounded payload and valid SHA-256 content hash.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (evidenceReferences.Any(evidence => !ProcessDriverEvidencePolicy.HasValidSha256ContentHash(evidence)) ||
            evidenceReferences.Any(evidence => !ArtifactEvidenceReferenceMatches(evidence)) ||
            !ArtifactEvidenceReferenceMatches(request.SuppliedContent.EvidenceReference) ||
            !SuppliedReferenceIsIncluded(request.SuppliedContent, evidenceReferences) ||
            !ProcessDriverSuppliedEvidenceContentRules.HasEvidenceReferenceHashBinding(request.SuppliedContent))
        {
            diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.EvidenceHashMismatch,
                "Supplied artifact evidence envelope does not match the supplied Core artifact descriptor reference or content hash.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (DescriptorCount(request) == 0)
        {
            diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Artifact evidence verification requires at least one supplied projection or validation descriptor.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (HasProjectionDescriptors(request) &&
            !evidenceReferences.Any(evidence => evidence.CoreDescriptorFamily == ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence))
        {
            diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Artifact projection descriptors require a supplied artifact projection evidence reference.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (request.ValidationRequirements.Count > 0 &&
            !evidenceReferences.Any(evidence => evidence.CoreDescriptorFamily == ProcessDriverCoreDescriptorFamily.ArtifactProjectionValidation))
        {
            diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Artifact validation descriptors require a supplied artifact projection validation reference.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        return ProcessDriverDenialReason.None;
    }

    private static int DescriptorCount(ArtifactEvidenceVerificationRequest request)
    {
        return request.ProjectionLineage.Count +
            request.ProjectionSourceOrder.Count +
            request.ProviderNativeBrowserEvidence.Count +
            request.ValidationRequirements.Count;
    }

    private static bool HasProjectionDescriptors(ArtifactEvidenceVerificationRequest request)
    {
        return request.ProjectionLineage.Count > 0 ||
            request.ProjectionSourceOrder.Count > 0 ||
            request.ProviderNativeBrowserEvidence.Count > 0;
    }

    private static bool ArtifactEvidenceReferenceMatches(ProcessDriverEvidenceReference evidenceReference)
    {
        return evidenceReference.Kind == ProcessDriverEvidenceReferenceKind.CoreDescriptor &&
            evidenceReference.CoreDescriptorFamily is
                ProcessDriverCoreDescriptorFamily.ArtifactProjectionEvidence or
                ProcessDriverCoreDescriptorFamily.ArtifactProjectionValidation;
    }

    private static bool SuppliedReferenceIsIncluded(
        ProcessDriverSuppliedEvidenceContent suppliedContent,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences)
    {
        return evidenceReferences.Any(evidenceReference =>
            EvidenceReferenceMatches(evidenceReference, suppliedContent.EvidenceReference));
    }

    private static bool EvidenceReferenceMatches(
        ProcessDriverEvidenceReference left,
        ProcessDriverEvidenceReference right)
    {
        return left.Kind == right.Kind &&
            left.CoreDescriptorFamily == right.CoreDescriptorFamily &&
            string.Equals(left.Uri.Trim(), right.Uri.Trim(), StringComparison.OrdinalIgnoreCase) &&
            ProcessDriverEvidencePolicy.NormalizeHash(left.ContentHash) ==
            ProcessDriverEvidencePolicy.NormalizeHash(right.ContentHash);
    }
}
