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
        ProcessDriverEvidenceReference evidenceReference)
    {
        var redaction = ProcessDriverRedactionPolicy.Redact(string.Empty).Descriptor;
        var diagnostic = new ProcessDriverDiagnostic(
            ProcessDriverDiagnosticSeverity.Error,
            diagnosticCategory,
            diagnosticMessage,
            evidenceReference);
        var auditFacts = CreateDeniedAuditFacts(
            payload,
            requestedOperations,
            denialReason,
            redaction,
            diagnosticMessage);
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
        string diagnosticSummary)
    {
        var outputHash = ProcessDriverEvidencePolicy.ComputeSha256(diagnosticSummary);

        return requestedOperations
            .Select(operation => new ProcessDriverAuditFact(
                CreateStableAuditId(payload, operation, denialReason),
                payload.RequestedAt,
                ProcessDriverAuditFactKind.OperationDenied,
                payload.CallerContext.Trim(),
                payload.PermissionMode,
                payload.Scope,
                operation,
                denialReason,
                redaction,
                diagnosticSummary,
                outputHash))
            .ToArray();
    }

    private static Guid CreateStableAuditId(
        ProcessTranscriptVerificationReadOnlyEvidencePayload payload,
        ProcessDriverOperation operation,
        ProcessDriverDenialReason denialReason)
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
            ProcessDriverEvidencePolicy.NormalizeHash(payload.TranscriptReference.TranscriptHash));
        var bytes = Convert.FromHexString(ProcessDriverEvidencePolicy.ComputeSha256(material));

        return new Guid(bytes[..16]);
    }
}

internal sealed record ProcessTranscriptVerificationPreflightDenial(ProcessDriverVerificationResponse Response);
