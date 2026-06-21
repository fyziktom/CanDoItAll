using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessTranscriptVerificationPreflightPolicy
{
    public static ProcessTranscriptVerificationPreflightDenial? Validate(
        ProcessTranscriptVerificationReadOnlyEvidencePayload payload,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences,
        IReadOnlyList<ProcessDriverOperation> requestedOperations,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        if (payload.PermissionMode == ProcessDriverPermissionMode.Unspecified)
        {
            return CreateDenial(
                payload,
                requestedOperations,
                ProcessDriverDenialReason.MissingPermissionMode,
                ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat,
                "Verification request is missing a permission mode.",
                evidenceReferences,
                primaryEvidence);
        }

        if (!ProcessDriverCapabilityScopeRules.IsDotNetRustTranscriptVerificationScope(payload.Scope, payload.PermissionMode))
        {
            return CreateDenial(
                payload,
                requestedOperations,
                ProcessDriverDenialReason.CapabilityScopeDenied,
                ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat,
                "Capability scope is not the process read-only .NET/Rust transcript verification lane.",
                evidenceReferences,
                primaryEvidence);
        }

        foreach (var operation in requestedOperations)
        {
            if (ProcessDriverOperationRules.IsReadonlyVerificationOperation(operation))
            {
                continue;
            }

            return CreateDenial(
                payload,
                requestedOperations,
                ProcessDriverOperationRules.ResolveReadonlyDenialReason(operation),
                ProcessDriverDiagnosticCategory.MutationAttemptDenied,
                $"Operation {operation} is denied by the process read-only evidence adapter.",
                evidenceReferences,
                primaryEvidence);
        }

        if (evidenceReferences.Count == 0)
        {
            return CreateDenial(
                payload,
                requestedOperations,
                ProcessDriverDenialReason.MissingEvidence,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Process transcript verification requires at least one supplied evidence reference.",
                evidenceReferences,
                primaryEvidence);
        }

        var uriPolicyResult = ProcessDriverEvidencePolicy.ValidateApprovedSuppliedEvidenceUris(
            payload.TranscriptReference,
            evidenceReferences);
        if (!uriPolicyResult.Accepted)
        {
            return CreateDenial(
                payload,
                requestedOperations,
                ProcessDriverDenialReason.MissingEvidence,
                ProcessDriverDiagnosticCategory.TranscriptUntrusted,
                "Evidence source is not an approved supplied process evidence payload reference.",
                evidenceReferences,
                primaryEvidence);
        }

        if (evidenceReferences.Any(reference => !ProcessDriverEvidencePolicy.HasValidSha256ContentHash(reference)))
        {
            return CreateDenial(
                payload,
                requestedOperations,
                ProcessDriverDenialReason.MissingEvidence,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Every supplied process evidence reference must include a valid SHA-256 content hash.",
                evidenceReferences,
                primaryEvidence);
        }

        if (!ProcessDriverEvidencePolicy.TranscriptHashMatches(payload.TranscriptReference, payload.TranscriptText))
        {
            return CreateDenial(
                payload,
                requestedOperations,
                ProcessDriverDenialReason.MissingEvidence,
                ProcessDriverDiagnosticCategory.EvidenceHashMismatch,
                "Supplied transcript content does not match the process evidence hash.",
                evidenceReferences,
                primaryEvidence);
        }

        return null;
    }

    private static ProcessTranscriptVerificationPreflightDenial CreateDenial(
        ProcessTranscriptVerificationReadOnlyEvidencePayload payload,
        IReadOnlyList<ProcessDriverOperation> requestedOperations,
        ProcessDriverDenialReason denialReason,
        ProcessDriverDiagnosticCategory diagnosticCategory,
        string diagnosticMessage,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences,
        ProcessDriverEvidenceReference evidenceReference)
    {
        var redaction = ProcessDriverRedactionPolicy.Redact(string.Empty).Descriptor;
        var diagnostic = new ProcessDriverDiagnostic(
            ProcessDriverDiagnosticSeverity.Error,
            diagnosticCategory,
            diagnosticMessage,
            evidenceReference);
        var diagnosticSummary = ProcessDriverRedactionPolicy.RedactDiagnosticSummary(diagnosticMessage).RedactedText;
        var auditFacts = CreateDeniedAuditFacts(
            payload,
            requestedOperations,
            denialReason,
            redaction,
            evidenceReferences,
            evidenceReference,
            diagnosticSummary);
        var response = new ProcessDriverVerificationResponse(
            Accepted: false,
            DenialReason: denialReason,
            Diagnostics: [diagnostic],
            EvidenceReferences: [evidenceReference],
            Redaction: redaction,
            NoMutationPerformed: true,
            AuditFacts: auditFacts,
            ContractVersion: ProcessDriverContractVersion.Current);

        return new ProcessTranscriptVerificationPreflightDenial(response);
    }

    private static IReadOnlyList<ProcessDriverAuditFact> CreateDeniedAuditFacts(
        ProcessTranscriptVerificationReadOnlyEvidencePayload payload,
        IReadOnlyList<ProcessDriverOperation> requestedOperations,
        ProcessDriverDenialReason denialReason,
        ProcessDriverRedactionDescriptor redaction,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences,
        ProcessDriverEvidenceReference primaryEvidence,
        string diagnosticSummary)
    {
        var outputHash = ProcessDriverEvidencePolicy.ComputeSha256(diagnosticSummary);
        var auditEvidenceReferences = CreateAuditEvidenceReferences(evidenceReferences, primaryEvidence);

        return requestedOperations
            .Select(operation => new ProcessDriverAuditFact(
                CreateStableAuditId(payload, operation, denialReason, auditEvidenceReferences),
                payload.RequestedAt,
                ProcessDriverAuditFactKind.OperationDenied,
                payload.CallerContext.Trim(),
                payload.PermissionMode,
                payload.Scope,
                payload.Scope.Kind,
                operation,
                auditEvidenceReferences,
                denialReason,
                redaction,
                diagnosticSummary,
                outputHash))
            .ToArray();
    }

    private static Guid CreateStableAuditId(
        ProcessTranscriptVerificationReadOnlyEvidencePayload payload,
        ProcessDriverOperation operation,
        ProcessDriverDenialReason denialReason,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences)
    {
        var material = string.Join(
            "|",
            payload.ProcessRunId,
            payload.StepRunId,
            payload.ArtifactId,
            payload.RequestedAt.ToUnixTimeMilliseconds(),
            payload.CallerContext.Trim(),
            payload.PermissionMode,
            payload.Scope.Kind,
            operation,
            denialReason,
            payload.TranscriptReference.Uri.Trim(),
            ProcessDriverEvidencePolicy.NormalizeHash(payload.TranscriptReference.TranscriptHash),
            string.Join(",", evidenceReferences.Select(CreateEvidenceId)));
        var bytes = Convert.FromHexString(ProcessDriverEvidencePolicy.ComputeSha256(material));

        return new Guid(bytes[..16]);
    }

    private static IReadOnlyList<ProcessDriverEvidenceReference> CreateAuditEvidenceReferences(
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        IReadOnlyList<ProcessDriverEvidenceReference> references = evidenceReferences.Count == 0
            ? [primaryEvidence]
            : evidenceReferences;

        return ProcessDriverEvidencePolicy.NormalizeEvidenceReferences(references);
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

internal sealed record ProcessTranscriptVerificationPreflightDenial(ProcessDriverVerificationResponse Response);
