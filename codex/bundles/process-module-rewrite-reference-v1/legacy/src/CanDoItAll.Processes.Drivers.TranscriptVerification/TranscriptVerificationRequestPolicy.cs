using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.TranscriptVerification;

internal static class TranscriptVerificationRequestPolicy
{
    public static ProcessDriverDenialReason Validate(
        TranscriptVerificationAlphaRequest request,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences,
        List<ProcessDriverDiagnostic> diagnostics,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        var verificationRequest = request.VerificationRequest;
        if (verificationRequest.PermissionMode == ProcessDriverPermissionMode.Unspecified)
        {
            diagnostics.Add(CreatePolicyDiagnostic(
                ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat,
                "Verification request is missing a permission mode.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingPermissionMode;
        }

        if (!ProcessDriverCapabilityScopeRules.IsDotNetRustTranscriptVerificationScope(
            verificationRequest.Scope,
            verificationRequest.PermissionMode))
        {
            diagnostics.Add(CreatePolicyDiagnostic(
                ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat,
                "Capability scope is not the read-only .NET/Rust transcript verification lane.",
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
            diagnostics.Add(CreatePolicyDiagnostic(
                ProcessDriverDiagnosticCategory.MutationAttemptDenied,
                $"Operation {operation} is denied for verification-only transcript inspection.",
                primaryEvidence));

            return denialReason;
        }

        if ((verificationRequest.EvidenceReferences?.Count ?? 0) == 0)
        {
            diagnostics.Add(CreatePolicyDiagnostic(
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Verification requires at least one supplied evidence reference.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        var suppliedEvidenceReferences = evidenceReferences
            .Concat([request.SuppliedContent.EvidenceReference])
            .ToArray();
        var uriPolicyResult = ProcessDriverEvidencePolicy.ValidateApprovedSuppliedEvidenceUris(
            request.TranscriptReference,
            suppliedEvidenceReferences);
        if (!uriPolicyResult.Accepted)
        {
            diagnostics.Add(CreatePolicyDiagnostic(
                ProcessDriverDiagnosticCategory.TranscriptUntrusted,
                "Evidence source is not an approved supplied process evidence payload reference.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (string.IsNullOrWhiteSpace(request.TranscriptText))
        {
            diagnostics.Add(CreatePolicyDiagnostic(
                ProcessDriverDiagnosticCategory.TranscriptMissing,
                "Verification requires supplied transcript content.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (!ProcessDriverSuppliedEvidenceContentRules.HasExpectedEnvelope(
            request.SuppliedContent,
            ProcessDriverSuppliedEvidenceContentKind.TranscriptText,
            ProcessDriverSuppliedEvidenceContentRules.PlainTextContentType))
        {
            diagnostics.Add(CreatePolicyDiagnostic(
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Supplied transcript evidence must use the transcript text envelope and plain text content type.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (!ProcessDriverSuppliedEvidenceContentRules.HasAllowedSize(request.SuppliedContent))
        {
            diagnostics.Add(CreatePolicyDiagnostic(
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Supplied transcript evidence size is missing or exceeds the allowed verification envelope limit.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (evidenceReferences.Any(evidence => !ProcessDriverEvidencePolicy.HasValidSha256ContentHash(evidence)))
        {
            diagnostics.Add(CreatePolicyDiagnostic(
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Every evidence reference must include a valid SHA-256 content hash.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (!ProcessDriverSuppliedEvidenceContentRules.HasValidContentHash(request.SuppliedContent))
        {
            diagnostics.Add(CreatePolicyDiagnostic(
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Supplied transcript evidence must include a valid SHA-256 content hash.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (!TranscriptReferenceMatchesSuppliedContent(request) ||
            !ProcessDriverSuppliedEvidenceContentRules.HasEvidenceReferenceHashBinding(request.SuppliedContent) ||
            !ProcessDriverSuppliedEvidenceContentRules.HashMatchesSuppliedPayload(
                request.SuppliedContent,
                request.TranscriptText))
        {
            diagnostics.Add(CreatePolicyDiagnostic(
                ProcessDriverDiagnosticCategory.EvidenceHashMismatch,
                "Supplied transcript evidence envelope does not match the supplied transcript reference or content hash.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (!ProcessDriverEvidencePolicy.TranscriptHashMatches(request.TranscriptReference, request.TranscriptText))
        {
            diagnostics.Add(CreatePolicyDiagnostic(
                ProcessDriverDiagnosticCategory.EvidenceHashMismatch,
                "Transcript content does not match the supplied transcript hash.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        return ProcessDriverDenialReason.None;
    }

    private static bool TranscriptReferenceMatchesSuppliedContent(TranscriptVerificationAlphaRequest request)
    {
        var suppliedReference = request.SuppliedContent.EvidenceReference;

        return suppliedReference.Kind == ProcessDriverEvidenceReferenceKind.CommandTranscript &&
            suppliedReference.CoreDescriptorFamily == ProcessDriverCoreDescriptorFamily.ExecutionEvidence &&
            string.Equals(
                suppliedReference.Uri.Trim(),
                request.TranscriptReference.Uri.Trim(),
                StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessDriverDiagnostic CreatePolicyDiagnostic(
        ProcessDriverDiagnosticCategory category,
        string message,
        ProcessDriverEvidenceReference evidenceReference)
    {
        return TranscriptVerificationDiagnosticFactory.CreateWithoutRedaction(
            ProcessDriverDiagnosticSeverity.Error,
            category,
            message,
            evidenceReference);
    }
}
