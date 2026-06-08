using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.TranscriptVerification;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessTranscriptVerificationReadOnlyAdapter
{
    private readonly Func<TranscriptVerificationAlphaRequest, ProcessDriverVerificationResponse> verifyTranscript;

    public ProcessTranscriptVerificationReadOnlyAdapter()
        : this(request => new TranscriptVerificationAlphaVerifier().Verify(request))
    {
    }

    internal ProcessTranscriptVerificationReadOnlyAdapter(
        Func<TranscriptVerificationAlphaRequest, ProcessDriverVerificationResponse> verifyTranscript)
    {
        this.verifyTranscript = verifyTranscript ?? throw new ArgumentNullException(nameof(verifyTranscript));
    }

    public ProcessTranscriptVerificationReadOnlyObservation Verify(
        ProcessTranscriptVerificationReadOnlyEvidencePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var evidenceReferences = NormalizeEvidenceReferences(payload.EvidenceReferences);
        var requestedOperations = NormalizeRequestedOperations(payload.RequestedOperations);
        var transcriptEvidence = CreateTranscriptEvidenceReference(payload.TranscriptReference, payload.TranscriptText);
        var primaryEvidence = evidenceReferences.FirstOrDefault() ?? transcriptEvidence;
        var preflightDenial = ValidatePreflight(
            payload,
            evidenceReferences,
            requestedOperations,
            primaryEvidence);

        if (preflightDenial is not null)
        {
            return CreateObservation(payload, preflightDenial.Response);
        }

        var verificationRequest = new ProcessDriverVerificationRequest(
            payload.PermissionMode,
            payload.Scope,
            evidenceReferences,
            requestedOperations,
            payload.CallerContext.Trim(),
            ProcessDriverContractVersion.Current);
        var request = new TranscriptVerificationAlphaRequest(
            verificationRequest,
            payload.TranscriptReference,
            payload.TranscriptText,
            payload.RequestedAt);

        return CreateObservation(payload, verifyTranscript(request));
    }

    private static PreflightDenial? ValidatePreflight(
        ProcessTranscriptVerificationReadOnlyEvidencePayload payload,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences,
        IReadOnlyList<ProcessDriverOperation> requestedOperations,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        if (payload.PermissionMode == ProcessDriverPermissionMode.Unspecified)
        {
            return CreatePreflightDenial(
                payload,
                ProcessDriverDenialReason.MissingPermissionMode,
                ProcessDriverDiagnosticCategory.UnsupportedTranscriptFormat,
                "Verification request is missing a permission mode.",
                primaryEvidence);
        }

        if (!ProcessDriverCapabilityScopeRules.IsDotNetRustTranscriptVerificationScope(payload.Scope, payload.PermissionMode))
        {
            return CreatePreflightDenial(
                payload,
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

            return CreatePreflightDenial(
                payload,
                ProcessDriverOperationRules.ResolveReadonlyDenialReason(operation),
                ProcessDriverDiagnosticCategory.MutationAttemptDenied,
                $"Operation {operation} is denied by the process read-only evidence adapter.",
                primaryEvidence);
        }

        if (evidenceReferences.Count == 0)
        {
            return CreatePreflightDenial(
                payload,
                ProcessDriverDenialReason.MissingEvidence,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Process transcript verification requires at least one supplied evidence reference.",
                primaryEvidence);
        }

        if (!IsApprovedSuppliedEvidenceUri(payload.TranscriptReference.Uri) ||
            evidenceReferences.Any(reference => !IsApprovedSuppliedEvidenceUri(reference.Uri)))
        {
            return CreatePreflightDenial(
                payload,
                ProcessDriverDenialReason.MissingEvidence,
                ProcessDriverDiagnosticCategory.TranscriptUntrusted,
                "Evidence source is not an approved supplied process evidence payload reference.",
                primaryEvidence);
        }

        if (evidenceReferences.Any(reference => !IsSha256(reference.ContentHash)))
        {
            return CreatePreflightDenial(
                payload,
                ProcessDriverDenialReason.MissingEvidence,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Every supplied process evidence reference must include a valid SHA-256 content hash.",
                primaryEvidence);
        }

        var expectedTranscriptHash = NormalizeHash(payload.TranscriptReference.TranscriptHash);
        if (!IsSha256(expectedTranscriptHash) || expectedTranscriptHash != ComputeSha256(payload.TranscriptText))
        {
            return CreatePreflightDenial(
                payload,
                ProcessDriverDenialReason.MissingEvidence,
                ProcessDriverDiagnosticCategory.EvidenceHashMismatch,
                "Supplied transcript content does not match the process evidence hash.",
                primaryEvidence);
        }

        return null;
    }

    private static PreflightDenial CreatePreflightDenial(
        ProcessTranscriptVerificationReadOnlyEvidencePayload payload,
        ProcessDriverDenialReason denialReason,
        ProcessDriverDiagnosticCategory diagnosticCategory,
        string diagnosticMessage,
        ProcessDriverEvidenceReference evidenceReference)
    {
        var redaction = new ProcessDriverRedactionDescriptor(
            ProcessDriverRedactionStatus.None,
            [],
            ComputeSha256(string.Empty));
        var diagnostic = new ProcessDriverDiagnostic(
            ProcessDriverDiagnosticSeverity.Error,
            diagnosticCategory,
            diagnosticMessage,
            evidenceReference);
        var auditFacts = CreateDeniedAuditFacts(
            payload,
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

        return new PreflightDenial(response);
    }

    private static ProcessTranscriptVerificationReadOnlyObservation CreateObservation(
        ProcessTranscriptVerificationReadOnlyEvidencePayload payload,
        ProcessDriverVerificationResponse response)
    {
        return new ProcessTranscriptVerificationReadOnlyObservation(
            payload.ProcessRunId,
            payload.StepRunId,
            payload.ArtifactId,
            ProcessTranscriptVerificationSourceLane.DotNetRustTranscriptVerification,
            response.Accepted,
            response.DenialReason,
            response.Diagnostics,
            response.EvidenceReferences,
            response.Redaction,
            response.NoMutationPerformed,
            response.AuditFacts,
            response.ContractVersion,
            payload.RequestedAt,
            payload.RequestedAt);
    }

    private static IReadOnlyList<ProcessDriverEvidenceReference> NormalizeEvidenceReferences(
        IReadOnlyList<ProcessDriverEvidenceReference>? evidenceReferences)
    {
        var normalized = new List<ProcessDriverEvidenceReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var evidenceReference in evidenceReferences ?? [])
        {
            var normalizedReference = new ProcessDriverEvidenceReference(
                evidenceReference.Kind,
                evidenceReference.Uri.Trim(),
                NormalizeHash(evidenceReference.ContentHash),
                evidenceReference.CoreDescriptorFamily);
            var key = $"{normalizedReference.Kind}|{normalizedReference.Uri}|{normalizedReference.ContentHash}";
            if (seen.Add(key))
            {
                normalized.Add(normalizedReference);
            }
        }

        return normalized;
    }

    private static IReadOnlyList<ProcessDriverOperation> NormalizeRequestedOperations(
        IReadOnlyList<ProcessDriverOperation>? requestedOperations)
    {
        var normalized = (requestedOperations ?? [])
            .Distinct()
            .ToArray();

        if (normalized.Length > 0)
        {
            return normalized;
        }

        return
        [
            ProcessDriverOperation.InspectExistingEvidence,
            ProcessDriverOperation.ReturnDiagnostics,
            ProcessDriverOperation.ReadProcessFacts
        ];
    }

    private static ProcessDriverEvidenceReference CreateTranscriptEvidenceReference(
        ProcessDriverTranscriptReference transcriptReference,
        string transcriptText)
    {
        var transcriptHash = NormalizeHash(transcriptReference.TranscriptHash);

        return new ProcessDriverEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CommandTranscript,
            transcriptReference.Uri.Trim(),
            IsSha256(transcriptHash) ? transcriptHash : ComputeSha256(transcriptText),
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);
    }

    private static IReadOnlyList<ProcessDriverAuditFact> CreateDeniedAuditFacts(
        ProcessTranscriptVerificationReadOnlyEvidencePayload payload,
        ProcessDriverDenialReason denialReason,
        ProcessDriverRedactionDescriptor redaction,
        string diagnosticSummary)
    {
        var operations = NormalizeRequestedOperations(payload.RequestedOperations);
        var outputHash = ComputeSha256(diagnosticSummary);

        return operations
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
            NormalizeHash(payload.TranscriptReference.TranscriptHash));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(material));

        return new Guid(bytes[..16]);
    }

    private static bool IsApprovedSuppliedEvidenceUri(string uri)
    {
        var trimmed = uri.Trim();

        return trimmed.StartsWith("bundle://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("process://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("artifact://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("repo://tests/", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("repo://codex/bundles/", StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeSha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static string NormalizeHash(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static bool IsSha256(string value)
    {
        return value.Length == 64 && value.All(static character =>
            character is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');
    }

    private sealed record PreflightDenial(ProcessDriverVerificationResponse Response);
}

internal sealed record ProcessTranscriptVerificationReadOnlyEvidencePayload(
    Guid ProcessRunId,
    Guid StepRunId,
    Guid? ArtifactId,
    string CallerContext,
    ProcessDriverPermissionMode PermissionMode,
    ProcessDriverCapabilityScope Scope,
    ProcessDriverTranscriptReference TranscriptReference,
    string TranscriptText,
    IReadOnlyList<ProcessDriverEvidenceReference> EvidenceReferences,
    IReadOnlyList<ProcessDriverOperation> RequestedOperations,
    DateTimeOffset RequestedAt);

internal sealed record ProcessTranscriptVerificationReadOnlyObservation(
    Guid ProcessRunId,
    Guid StepRunId,
    Guid? ArtifactId,
    ProcessTranscriptVerificationSourceLane SourceLane,
    bool Accepted,
    ProcessDriverDenialReason DenialReason,
    IReadOnlyList<ProcessDriverDiagnostic> Diagnostics,
    IReadOnlyList<ProcessDriverEvidenceReference> EvidenceReferences,
    ProcessDriverRedactionDescriptor Redaction,
    bool NoMutationPerformed,
    IReadOnlyList<ProcessDriverAuditFact> AuditFacts,
    ProcessDriverContractVersion ContractVersion,
    DateTimeOffset RequestedAt,
    DateTimeOffset ObservedAt);

internal enum ProcessTranscriptVerificationSourceLane
{
    DotNetRustTranscriptVerification = 1
}
