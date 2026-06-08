using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.BusinessAnalysis;

internal static class BusinessAnalysisVerificationRequestPolicy
{
    public static ProcessDriverDenialReason Validate(
        BusinessAnalysisVerificationRequest request,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences,
        List<ProcessDriverDiagnostic> diagnostics,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        var verificationRequest = request.VerificationRequest;
        if (verificationRequest.ContractVersion.Major != ProcessDriverContractVersion.Current.Major)
        {
            diagnostics.Add(BusinessAnalysisDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.UnsupportedContractVersion,
                "Business analysis verification requires a compatible process driver contract major version.",
                primaryEvidence));

            return ProcessDriverDenialReason.UnsupportedMode;
        }

        if (verificationRequest.PermissionMode == ProcessDriverPermissionMode.Unspecified)
        {
            diagnostics.Add(BusinessAnalysisDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat,
                "Business analysis verification request is missing a permission mode.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingPermissionMode;
        }

        if (!ProcessDriverCapabilityScopeRules.IsBusinessAnalysisReadScope(
            verificationRequest.Scope,
            verificationRequest.PermissionMode))
        {
            diagnostics.Add(BusinessAnalysisDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat,
                "Capability scope is not the read-only business analysis verification lane.",
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
            diagnostics.Add(BusinessAnalysisDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.MutationAttemptDenied,
                $"Operation {operation} is denied for verification-only business analysis inspection.",
                primaryEvidence));

            return denialReason;
        }

        if ((verificationRequest.EvidenceReferences?.Count ?? 0) == 0 || evidenceReferences.Count == 0)
        {
            diagnostics.Add(BusinessAnalysisDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Business analysis verification requires at least one supplied business evidence reference.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        var suppliedEvidenceReferences = evidenceReferences
            .Concat([request.SuppliedContent.EvidenceReference])
            .ToArray();
        var uriPolicyResult = ProcessDriverEvidencePolicy.ValidateApprovedSuppliedEvidenceUris(suppliedEvidenceReferences);
        if (!uriPolicyResult.Accepted)
        {
            diagnostics.Add(BusinessAnalysisDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.TranscriptUntrusted,
                "Business analysis evidence source is not an approved supplied evidence payload reference.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (!ProcessDriverSuppliedEvidenceContentRules.HasExpectedEnvelope(
            request.SuppliedContent,
            ProcessDriverSuppliedEvidenceContentKind.BusinessAnalysisPayload,
            ProcessDriverSuppliedEvidenceContentRules.JsonContentType))
        {
            diagnostics.Add(BusinessAnalysisDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Supplied business analysis evidence must use the business analysis payload envelope and JSON content type.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (!ProcessDriverSuppliedEvidenceContentRules.HasAllowedSize(request.SuppliedContent) ||
            !ProcessDriverSuppliedEvidenceContentRules.HasValidContentHash(request.SuppliedContent))
        {
            diagnostics.Add(BusinessAnalysisDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Supplied business analysis evidence must include a bounded payload and valid SHA-256 content hash.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (evidenceReferences.Any(evidence => !ProcessDriverEvidencePolicy.HasValidSha256ContentHash(evidence)) ||
            !BusinessAnalysisReferenceMatchesSuppliedContent(request.SuppliedContent) ||
            !SuppliedReferenceIsIncluded(request.SuppliedContent, evidenceReferences) ||
            !ProcessDriverSuppliedEvidenceContentRules.HasEvidenceReferenceHashBinding(request.SuppliedContent))
        {
            diagnostics.Add(BusinessAnalysisDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.EvidenceHashMismatch,
                "Supplied business analysis envelope does not match the supplied evidence reference or content hash.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (request.Items.Count == 0)
        {
            diagnostics.Add(BusinessAnalysisDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Business analysis verification requires at least one supplied deliverable or evidence item.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        return ProcessDriverDenialReason.None;
    }

    private static bool BusinessAnalysisReferenceMatchesSuppliedContent(
        ProcessDriverSuppliedEvidenceContent suppliedContent)
    {
        return suppliedContent.EvidenceReference.Kind == ProcessDriverEvidenceReferenceKind.BusinessReadonlyArtifact &&
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
