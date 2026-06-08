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
        var diagnosticSummary = ProcessDriverRedactionPolicy.Redact(
            string.Join(" ", diagnostics.Select(diagnostic => diagnostic.Message)),
            ProcessDriverRedactionPolicy.DefaultMaxAuditSummaryLength).RedactedText;
        var factKind = accepted
            ? ProcessDriverAuditFactKind.DiagnosticReturned
            : ProcessDriverAuditFactKind.OperationDenied;

        return requestedOperations
            .Select(operation => new ProcessDriverAuditFact(
                CreateStableAuditId(request, operation, denialReason),
                request.RequestedAt,
                factKind,
                request.VerificationRequest.CallerContext,
                request.VerificationRequest.PermissionMode,
                request.VerificationRequest.Scope,
                operation,
                denialReason,
                redaction,
                diagnosticSummary,
                ProcessDriverEvidencePolicy.ComputeSha256(diagnosticSummary)))
            .ToArray();
    }

    private static Guid CreateStableAuditId(
        TranscriptVerificationAlphaRequest request,
        ProcessDriverOperation operation,
        ProcessDriverDenialReason denialReason)
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
            ProcessDriverEvidencePolicy.NormalizeHash(request.TranscriptReference.TranscriptHash));
        var bytes = Convert.FromHexString(ProcessDriverEvidencePolicy.ComputeSha256(material));

        return new Guid(bytes[..16]);
    }
}
