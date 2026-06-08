using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Core.Diagnostics;
using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.RuntimeEvidence;

public sealed class RuntimeEvidenceConsistencyAlphaVerifier
{
    public ProcessDriverVerificationResponse Verify(RuntimeEvidenceConsistencyVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = request.VerificationRequest ?? throw new ArgumentException(
            "Verification request is required.",
            nameof(request));

        var evidenceReferences = ProcessDriverEvidencePolicy.NormalizeEvidenceReferences(
            request.VerificationRequest.EvidenceReferences);
        var primaryEvidence = evidenceReferences.FirstOrDefault() ?? CreateSyntheticRuntimeEvidenceReference(request);
        var diagnostics = new List<ProcessDriverDiagnostic>();
        var denialReason = ValidateRequest(request, evidenceReferences, diagnostics, primaryEvidence);
        if (denialReason == ProcessDriverDenialReason.None)
        {
            AddConsistencyDiagnostics(request, primaryEvidence, diagnostics);
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Info,
                ProcessDriverDiagnosticCategory.NoIssueDetected,
                "Runtime evidence consistency verification found no contradictory Core descriptor facts.",
                primaryEvidence));
        }

        var accepted = denialReason == ProcessDriverDenialReason.None;
        var redaction = ProcessDriverRedactionPolicy.Redact(string.Join(
            " ",
            diagnostics.Select(diagnostic => diagnostic.Message)));
        var auditFacts = CreateAuditFacts(
            request,
            diagnostics,
            redaction.Descriptor,
            accepted,
            denialReason);

        return new ProcessDriverVerificationResponse(
            accepted,
            denialReason,
            diagnostics,
            evidenceReferences.Count == 0 ? [primaryEvidence] : evidenceReferences,
            redaction.Descriptor,
            NoMutationPerformed: true,
            auditFacts,
            ProcessDriverContractVersion.Current);
    }

    private static ProcessDriverDenialReason ValidateRequest(
        RuntimeEvidenceConsistencyVerificationRequest request,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences,
        List<ProcessDriverDiagnostic> diagnostics,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        var verificationRequest = request.VerificationRequest;
        if (verificationRequest.ContractVersion.Major != ProcessDriverContractVersion.Current.Major)
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.UnsupportedContractVersion,
                "Runtime evidence verification requires a compatible process driver contract major version.",
                primaryEvidence));

            return ProcessDriverDenialReason.UnsupportedMode;
        }

        if (verificationRequest.PermissionMode == ProcessDriverPermissionMode.Unspecified)
        {
            diagnostics.Add(CreateDiagnostic(
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
            diagnostics.Add(CreateDiagnostic(
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
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.MutationAttemptDenied,
                $"Operation {operation} is denied for verification-only runtime evidence inspection.",
                primaryEvidence));

            return denialReason;
        }

        if ((verificationRequest.EvidenceReferences?.Count ?? 0) == 0 || evidenceReferences.Count == 0)
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Runtime evidence verification requires at least one supplied Core descriptor evidence reference.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        var uriPolicyResult = ProcessDriverEvidencePolicy.ValidateApprovedSuppliedEvidenceUris(evidenceReferences);
        if (!uriPolicyResult.Accepted)
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.TranscriptUntrusted,
                "Runtime evidence source is not an approved supplied process evidence payload reference.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        if (evidenceReferences.Any(evidence => !ProcessDriverEvidencePolicy.HasValidSha256ContentHash(evidence)))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.InsufficientProof,
                "Every runtime evidence reference must include a valid SHA-256 content hash.",
                primaryEvidence));

            return ProcessDriverDenialReason.MissingEvidence;
        }

        return ProcessDriverDenialReason.None;
    }

    private static void AddConsistencyDiagnostics(
        RuntimeEvidenceConsistencyVerificationRequest request,
        ProcessDriverEvidenceReference primaryEvidence,
        List<ProcessDriverDiagnostic> diagnostics)
    {
        if (request.ExecutionEvidence is not null &&
            request.FinalizerEvidence is not null &&
            request.ExecutionEvidence.Run.Outcome == ProcessAutomationRunOutcome.Succeeded &&
            request.FinalizerEvidence.Result.HasResult &&
            request.FinalizerEvidence.Result.CompletionStatus is not ProcessStepRunStatus.Completed)
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.FinalizerContradiction,
                "Execution evidence reports success while finalizer evidence reports a non-completed result.",
                primaryEvidence));
        }

        if (request.RetryDiagnostic is { ShouldRetry: false, HasUnresolvedCriticalToolFailures: true })
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.RetryContradiction,
                "Retry diagnostics report no retry while unresolved critical tool failures remain.",
                primaryEvidence));
        }

        if (request.ProviderRepairDiagnostic is { HasRepairOutcome: true, HasRecoverableProviderFailure: false })
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.ProviderRepairInconsistent,
                "Provider repair diagnostics report a repair outcome without a recoverable provider failure.",
                primaryEvidence));
        }

        if (request.FinalizerEvidence is { Result: { ShouldApplyTransition: true, HasResult: false } })
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.FinalizerContradiction,
                "Finalizer evidence says a transition should be applied but no finalizer result exists.",
                primaryEvidence));
        }

        if (request.NoProgressDiagnostic is { HasSignal: true } noProgress &&
            string.IsNullOrWhiteSpace(noProgress.Fingerprint))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.NoProgressFingerprintMissing,
                "No-progress retry diagnostics must include a stable fingerprint when no progress is signaled.",
                primaryEvidence));
        }

        if (request.ProjectionSourceOrder.Count > 0 &&
            !ProcessArtifactProjectionEvidenceDescriptorRules.IsDefaultProjectionOrder(
                request.ProjectionSourceOrder.Select(source => source.SourceKind).ToArray()))
        {
            diagnostics.Add(CreateDiagnostic(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.ProjectionOrderDrift,
                "Artifact projection source order differs from the Core default projection order.",
                primaryEvidence));
        }
    }

    private static ProcessDriverDiagnostic CreateDiagnostic(
        ProcessDriverDiagnosticSeverity severity,
        ProcessDriverDiagnosticCategory category,
        string message,
        ProcessDriverEvidenceReference evidence)
    {
        return new ProcessDriverDiagnostic(
            severity,
            category,
            ProcessDriverRedactionPolicy.Redact(
                message,
                ProcessDriverRedactionPolicy.DefaultMaxAuditSummaryLength).RedactedText,
            evidence);
    }

    private static IReadOnlyList<ProcessDriverAuditFact> CreateAuditFacts(
        RuntimeEvidenceConsistencyVerificationRequest request,
        IReadOnlyList<ProcessDriverDiagnostic> diagnostics,
        ProcessDriverRedactionDescriptor redaction,
        bool accepted,
        ProcessDriverDenialReason denialReason)
    {
        var requestedOperations = request.VerificationRequest.RequestedOperations.Count == 0
            ? [ProcessDriverOperation.ReadProcessFacts]
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
        RuntimeEvidenceConsistencyVerificationRequest request,
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
            string.Join(
                ",",
                request.VerificationRequest.EvidenceReferences.Select(reference => reference.Uri)));
        var bytes = Convert.FromHexString(ProcessDriverEvidencePolicy.ComputeSha256(material));

        return new Guid(bytes[..16]);
    }

    private static ProcessDriverEvidenceReference CreateSyntheticRuntimeEvidenceReference(
        RuntimeEvidenceConsistencyVerificationRequest request)
    {
        var material = string.Join(
            "|",
            request.ExecutionEvidence?.Run.ExecutionRunId,
            request.FinalizerEvidence?.Intent.ProcessRunId,
            request.RetryDiagnostic?.AttemptNumber,
            request.ProjectionSourceOrder.Count);

        return new ProcessDriverEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CoreDescriptor,
            "bundle://runtime-evidence/synthetic-reference",
            ProcessDriverEvidencePolicy.ComputeSha256(material),
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);
    }
}
