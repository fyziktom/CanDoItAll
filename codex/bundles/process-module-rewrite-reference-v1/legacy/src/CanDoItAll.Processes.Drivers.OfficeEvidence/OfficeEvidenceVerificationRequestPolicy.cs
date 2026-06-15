using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.OfficeEvidence;

internal static class OfficeEvidenceVerificationRequestPolicy
{
    public static ProcessDriverDenialReason Validate(
        OfficeEvidenceVerificationRequest request,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences,
        List<ProcessDriverDiagnostic> diagnostics,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        var verificationRequest = request.VerificationRequest;
        if (verificationRequest.ContractVersion.Major != ProcessDriverContractVersion.Current.Major)
        {
            diagnostics.Add(OfficeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.UnsupportedContractVersion,
                "Office evidence verification requires a compatible process driver contract major version.",
                primaryEvidence));

            return ProcessDriverDenialReason.UnsupportedMode;
        }

        if (verificationRequest.PermissionMode == ProcessDriverPermissionMode.Unspecified)
        {
            diagnostics.Add(OfficeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat,
                "Office evidence verification request is missing a permission mode.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingPermissionMode;
        }

        if (!ProcessDriverCapabilityScopeRules.IsOfficeEvidenceReadScope(
            verificationRequest.Scope,
            verificationRequest.PermissionMode))
        {
            diagnostics.Add(OfficeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat,
                "Capability scope is not the read-only Office evidence verification lane.",
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
            diagnostics.Add(OfficeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.MutationAttemptDenied,
                $"Operation {operation} is denied for verification-only Office evidence inspection.",
                primaryEvidence));

            return denialReason;
        }

        if ((verificationRequest.EvidenceReferences?.Count ?? 0) == 0 || evidenceReferences.Count == 0)
        {
            diagnostics.Add(OfficeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Office evidence verification requires at least one supplied Office evidence reference.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        var suppliedEvidenceReferences = evidenceReferences
            .Concat([request.SuppliedContent.EvidenceReference])
            .ToArray();
        var uriPolicyResult = ProcessDriverEvidencePolicy.ValidateApprovedSuppliedEvidenceUris(suppliedEvidenceReferences);
        if (!uriPolicyResult.Accepted)
        {
            diagnostics.Add(OfficeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.TranscriptUntrusted,
                "Office evidence source is not an approved supplied evidence payload reference.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (!ProcessDriverSuppliedEvidenceContentRules.HasExpectedEnvelope(
            request.SuppliedContent,
            ProcessDriverSuppliedEvidenceContentKind.OfficeEvidencePayload,
            ProcessDriverSuppliedEvidenceContentRules.JsonContentType))
        {
            diagnostics.Add(OfficeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Supplied Office evidence must use the Office evidence payload envelope and JSON content type.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (!ProcessDriverSuppliedEvidenceContentRules.HasAllowedSize(request.SuppliedContent) ||
            !ProcessDriverSuppliedEvidenceContentRules.HasValidContentHash(request.SuppliedContent))
        {
            diagnostics.Add(OfficeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Supplied Office evidence must include a bounded payload and valid SHA-256 content hash.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (evidenceReferences.Any(evidence => !ProcessDriverEvidencePolicy.HasValidSha256ContentHash(evidence)) ||
            !OfficeEvidenceReferenceMatchesSuppliedContent(request.SuppliedContent) ||
            !SuppliedReferenceIsIncluded(request.SuppliedContent, evidenceReferences) ||
            !ProcessDriverSuppliedEvidenceContentRules.HasEvidenceReferenceHashBinding(request.SuppliedContent))
        {
            diagnostics.Add(OfficeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.EvidenceHashMismatch,
                "Supplied Office evidence envelope does not match the supplied evidence reference or content hash.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (request.Items.Count == 0)
        {
            diagnostics.Add(OfficeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Office evidence verification requires at least one supplied email or document item.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        return ProcessDriverDenialReason.None;
    }

    private static bool OfficeEvidenceReferenceMatchesSuppliedContent(
        ProcessDriverSuppliedEvidenceContent suppliedContent)
    {
        return suppliedContent.EvidenceReference.Kind == ProcessDriverEvidenceReferenceKind.OfficeReadonlyArtifact &&
            suppliedContent.EvidenceReference.CoreDescriptorFamily is null;
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
