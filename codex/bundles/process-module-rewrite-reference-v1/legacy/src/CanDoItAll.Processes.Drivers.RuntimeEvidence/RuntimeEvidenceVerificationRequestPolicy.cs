using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.RuntimeEvidence;

internal static class RuntimeEvidenceVerificationRequestPolicy
{
    public static ProcessDriverDenialReason Validate(
        RuntimeEvidenceConsistencyVerificationRequest request,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences,
        List<ProcessDriverDiagnostic> diagnostics,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        var verificationRequest = request.VerificationRequest;
        if (verificationRequest.ContractVersion.Major != ProcessDriverContractVersion.Current.Major)
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.UnsupportedContractVersion,
                "Runtime evidence verification requires a compatible process driver contract major version.",
                primaryEvidence));

            return ProcessDriverDenialReason.UnsupportedMode;
        }

        if (verificationRequest.PermissionMode == ProcessDriverPermissionMode.Unspecified)
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat,
                "Runtime evidence verification request is missing a permission mode.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingPermissionMode;
        }

        if (!ProcessDriverCapabilityScopeRules.IsRuntimeFactsReadScope(
            verificationRequest.Scope,
            verificationRequest.PermissionMode))
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat,
                "Capability scope is not the read-only runtime facts verification lane.",
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
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.MutationAttemptDenied,
                $"Operation {operation} is denied for verification-only runtime evidence inspection.",
                primaryEvidence));

            return denialReason;
        }

        if ((verificationRequest.EvidenceReferences?.Count ?? 0) == 0 || evidenceReferences.Count == 0)
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Runtime evidence verification requires at least one supplied Core descriptor evidence reference.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        var suppliedEvidenceReferences = evidenceReferences
            .Concat([request.SuppliedContent.EvidenceReference])
            .ToArray();
        var uriPolicyResult = ProcessDriverEvidencePolicy.ValidateApprovedSuppliedEvidenceUris(suppliedEvidenceReferences);
        if (!uriPolicyResult.Accepted)
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.TranscriptUntrusted,
                "Runtime evidence source is not an approved supplied process evidence payload reference.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (!ProcessDriverSuppliedEvidenceContentRules.HasExpectedEnvelope(
            request.SuppliedContent,
            ProcessDriverSuppliedEvidenceContentKind.CoreDescriptorPayload,
            ProcessDriverSuppliedEvidenceContentRules.JsonContentType))
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Supplied runtime evidence must use the Core descriptor payload envelope and JSON content type.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (!ProcessDriverSuppliedEvidenceContentRules.HasAllowedSize(request.SuppliedContent))
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Supplied runtime evidence size is missing or exceeds the allowed verification envelope limit.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (evidenceReferences.Any(evidence => !ProcessDriverEvidencePolicy.HasValidSha256ContentHash(evidence)))
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Every runtime evidence reference must include a valid SHA-256 content hash.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (!ProcessDriverSuppliedEvidenceContentRules.HasValidContentHash(request.SuppliedContent))
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Supplied runtime evidence must include a valid SHA-256 content hash.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (!RuntimeEvidenceReferenceMatchesSuppliedContent(request.SuppliedContent) ||
            !SuppliedReferenceIsIncluded(request.SuppliedContent, evidenceReferences) ||
            !ProcessDriverSuppliedEvidenceContentRules.HasEvidenceReferenceHashBinding(request.SuppliedContent))
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.EvidenceHashMismatch,
                "Supplied runtime evidence envelope does not match the supplied Core descriptor evidence reference or content hash.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        return ProcessDriverDenialReason.None;
    }

    private static bool RuntimeEvidenceReferenceMatchesSuppliedContent(
        ProcessDriverSuppliedEvidenceContent suppliedContent)
    {
        return suppliedContent.EvidenceReference.Kind == ProcessDriverEvidenceReferenceKind.CoreDescriptor &&
            suppliedContent.EvidenceReference.CoreDescriptorFamily is not null;
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
