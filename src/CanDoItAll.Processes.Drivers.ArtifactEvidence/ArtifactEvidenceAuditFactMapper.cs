using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.ArtifactEvidence;

internal static class ArtifactEvidenceAuditFactMapper
{
    public static IReadOnlyList<ProcessDriverAuditFact> CreateAuditFacts(
        ArtifactEvidenceVerificationRequest request,
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

    private static IReadOnlyList<ProcessDriverEvidenceReference> CreateAuditEvidenceReferences(
        ArtifactEvidenceVerificationRequest request)
    {
        return ProcessDriverEvidencePolicy.NormalizeEvidenceReferences(
            request.VerificationRequest.EvidenceReferences
                .Concat([request.SuppliedContent.EvidenceReference])
                .ToArray());
    }

    private static Guid CreateStableAuditId(
        ArtifactEvidenceVerificationRequest request,
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
            string.Join(",", evidenceReferences.Select(CreateEvidenceId)),
            string.Join(",", request.ProjectionLineage.Select(CreateLineageId)),
            string.Join(",", request.ProjectionSourceOrder.Select(CreateSourceOrderId)),
            string.Join(",", request.ProviderNativeBrowserEvidence.Select(CreateProviderNativeBrowserId)),
            string.Join(",", request.ValidationRequirements.Select(requirement => requirement.ExpectationId)),
            string.Join(",", request.ExpectedArtifacts.Select(expectation => expectation.Id)),
            string.Join(",", request.ArtifactRecords.Select(record => record.Id)));
        var bytes = Convert.FromHexString(ProcessDriverEvidencePolicy.ComputeSha256(material));

        return new Guid(bytes[..16]);
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

    private static string CreateLineageId(ProcessArtifactProjectionLineageDescriptor lineage)
    {
        return string.Join(
            ":",
            lineage.SourceKind,
            ProcessDriverEvidencePolicy.NormalizeHash(lineage.ContentHash),
            ProcessDriverEvidencePolicy.NormalizeHash(lineage.ProjectionIdentityHash));
    }

    private static string CreateSourceOrderId(ProcessArtifactProjectionSourceOrderDescriptor sourceOrder)
    {
        return string.Join(
            ":",
            sourceOrder.SourceKind,
            sourceOrder.ProducerKind,
            sourceOrder.ProjectionOrder);
    }

    private static string CreateProviderNativeBrowserId(ProcessProviderNativeBrowserEvidenceDescriptor evidence)
    {
        return string.Join(
            ":",
            evidence.EvidenceKind,
            evidence.ToolName,
            evidence.HasDeclaredPath,
            evidence.HasMatchedOutput,
            evidence.CanSatisfyRequiredArtifact);
    }
}
