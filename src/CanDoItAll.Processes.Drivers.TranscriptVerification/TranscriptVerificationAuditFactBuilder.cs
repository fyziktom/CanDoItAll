using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.TranscriptVerification;

internal static class TranscriptVerificationAuditFactBuilder
{
    public static IReadOnlyList<ProcessDriverAuditFact> CreateAuditFacts(
        TranscriptVerificationAlphaRequest request,
        IReadOnlyList<ProcessDriverDiagnostic> diagnostics,
        ProcessDriverRedactionDescriptor redaction,
        bool accepted,
        ProcessDriverDenialReason denialReason)
    {
        var requestedOperations = request.VerificationRequest.RequestedOperations.Count == 0
            ? [ProcessDriverOperation.InspectExistingEvidence]
            : request.VerificationRequest.RequestedOperations;
        var diagnosticSummary = ProcessDriverRedactionPolicy.RedactDiagnosticSummary(
            string.Join(" ", diagnostics.Select(diagnostic => diagnostic.Message))).RedactedText;
        var factKind = accepted
            ? ProcessDriverAuditFactKind.DiagnosticReturned
            : ProcessDriverAuditFactKind.OperationDenied;
        var auditEvidenceReferences = CreateAuditEvidenceReferences(request);

        return requestedOperations
            .Select(operation => new ProcessDriverAuditFact(
                CreateStableAuditId(request, operation, denialReason, auditEvidenceReferences),
                request.RequestedAt,
                factKind,
                request.VerificationRequest.CallerContext,
                request.VerificationRequest.PermissionMode,
                request.VerificationRequest.Scope,
                request.VerificationRequest.Scope.Kind,
                operation,
                auditEvidenceReferences,
                denialReason,
                redaction,
                diagnosticSummary,
                ProcessDriverEvidencePolicy.ComputeSha256(diagnosticSummary)))
            .ToArray();
    }

    private static Guid CreateStableAuditId(
        TranscriptVerificationAlphaRequest request,
        ProcessDriverOperation operation,
        ProcessDriverDenialReason denialReason,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences)
    {
        var material = string.Join(
            "|",
            request.RequestedAt.ToUnixTimeMilliseconds(),
            request.VerificationRequest.CallerContext,
            request.VerificationRequest.PermissionMode,
            request.VerificationRequest.Scope.Kind,
            operation,
            denialReason,
            request.TranscriptReference.Uri,
            ProcessDriverEvidencePolicy.NormalizeHash(request.TranscriptReference.TranscriptHash),
            string.Join(",", evidenceReferences.Select(CreateEvidenceId)));
        var bytes = Convert.FromHexString(ProcessDriverEvidencePolicy.ComputeSha256(material));

        return new Guid(bytes[..16]);
    }

    private static IReadOnlyList<ProcessDriverEvidenceReference> CreateAuditEvidenceReferences(
        TranscriptVerificationAlphaRequest request)
    {
        return ProcessDriverEvidencePolicy.NormalizeEvidenceReferences(
            request.VerificationRequest.EvidenceReferences
                .Concat([request.SuppliedContent.EvidenceReference])
                .ToArray());
    }

    private static string CreateEvidenceId(ProcessDriverEvidenceReference evidenceReference)
    {
        return string.Join(
            ":",
            evidenceReference.Kind,
            evidenceReference.Uri.Trim(),
            ProcessDriverEvidencePolicy.NormalizeHash(evidenceReference.ContentHash),
            evidenceReference.CoreDescriptorFamily);
    }
}
