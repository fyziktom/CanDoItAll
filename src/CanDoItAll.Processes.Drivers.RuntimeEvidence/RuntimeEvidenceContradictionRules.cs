using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core.Artifacts;
using CanDoItAll.Processes.Core.Diagnostics;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.RuntimeEvidence;

internal static class RuntimeEvidenceContradictionRules
{
    public static IReadOnlyList<ProcessDriverDiagnostic> Evaluate(
        RuntimeEvidenceConsistencyVerificationRequest request,
        ProcessDriverEvidenceReference primaryEvidence)
    {
        var diagnostics = new List<ProcessDriverDiagnostic>();

        AddExecutionFinalizerContradictions(request, primaryEvidence, diagnostics);
        AddRetryContradictions(request, primaryEvidence, diagnostics);
        AddProviderContradictions(request, primaryEvidence, diagnostics);
        AddNoProgressContradictions(request, primaryEvidence, diagnostics);
        AddProjectionContradictions(request, primaryEvidence, diagnostics);

        return diagnostics;
    }

    private static void AddExecutionFinalizerContradictions(
        RuntimeEvidenceConsistencyVerificationRequest request,
        ProcessDriverEvidenceReference primaryEvidence,
        List<ProcessDriverDiagnostic> diagnostics)
    {
        if (request.ExecutionEvidence is { Run: { IsTerminal: true, IsActive: true } })
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.RuntimeEvidenceInconsistent,
                "Execution evidence reports a terminal run that is still active.",
                primaryEvidence));
        }

        if (request.ExecutionEvidence is { Run: { IsTerminal: true, CompletedAtUtc: null } })
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.RuntimeEvidenceInconsistent,
                "Execution evidence reports a terminal run without a completion timestamp.",
                primaryEvidence));
        }

        if (request.ExecutionEvidence is { Attempt: { HasUnresolvedCriticalToolFailures: false, UnresolvedCriticalToolFailureCount: > 0 } })
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.RuntimeEvidenceInconsistent,
                "Execution attempt evidence has critical tool failure count without the unresolved failure flag.",
                primaryEvidence));
        }

        if (request.ExecutionEvidence is not null &&
            request.FinalizerEvidence is not null &&
            request.ExecutionEvidence.Run.Outcome == ProcessAutomationRunOutcome.Succeeded &&
            request.FinalizerEvidence.Result.HasResult &&
            request.FinalizerEvidence.Result.CompletionStatus is not ProcessStepRunStatus.Completed)
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.FinalizerContradiction,
                "Execution evidence reports success while finalizer evidence reports a non-completed result.",
                primaryEvidence));
        }

        if (request.FinalizerEvidence is { Result: { HasResult: true, StepRunConcurrencyToken: null } })
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.FinalizerContradiction,
                "Finalizer evidence reports a result without a step-run concurrency token.",
                primaryEvidence));
        }

        if (request.FinalizerEvidence is { Intent.CompletionStatus: var intentStatus, Result.CompletionStatus: { } resultStatus } &&
            intentStatus != resultStatus)
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.FinalizerContradiction,
                "Finalizer intent completion status differs from finalizer result completion status.",
                primaryEvidence));
        }

        if (request.FinalizerEvidence is { Result: { ShouldApplyTransition: true, HasResult: false } })
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.FinalizerContradiction,
                "Finalizer evidence says a transition should be applied but no finalizer result exists.",
                primaryEvidence));
        }
    }

    private static void AddRetryContradictions(
        RuntimeEvidenceConsistencyVerificationRequest request,
        ProcessDriverEvidenceReference primaryEvidence,
        List<ProcessDriverDiagnostic> diagnostics)
    {
        if (request.RetryDiagnostic is { ShouldRetry: false, HasUnresolvedCriticalToolFailures: true })
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.RetryContradiction,
                "Retry diagnostics report no retry while unresolved critical tool failures remain.",
                primaryEvidence));
        }

        if (request.RetryDiagnostic is { ShouldRetry: true } retry &&
            retry.AttemptNumber >= retry.MaxExecutionAttempts)
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.RetryContradiction,
                "Retry diagnostics request another attempt after the maximum execution attempts have been reached.",
                primaryEvidence));
        }

        if (request.RetryDiagnostic is { ShouldRetry: true, PrimaryFailureKind: ProcessRetryDiagnosticFailureKind.None })
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.RetryContradiction,
                "Retry diagnostics request retry without a primary failure kind.",
                primaryEvidence));
        }
    }

    private static void AddProviderContradictions(
        RuntimeEvidenceConsistencyVerificationRequest request,
        ProcessDriverEvidenceReference primaryEvidence,
        List<ProcessDriverDiagnostic> diagnostics)
    {
        if (request.ProviderRepairDiagnostic is { HasRepairOutcome: true, HasRecoverableProviderFailure: false })
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.ProviderRepairInconsistent,
                "Provider repair diagnostics report a repair outcome without a recoverable provider failure.",
                primaryEvidence));
        }

        if (request.ProviderRepairDiagnostic is { HasRecoverableProviderFailure: true } provider &&
            (provider.AffectedAgentCount <= 0 ||
             string.IsNullOrWhiteSpace(provider.FailedProviderName) ||
             string.IsNullOrWhiteSpace(provider.FailureSummary)))
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.ProviderRepairInconsistent,
                "Provider repair diagnostics report a recoverable provider failure without affected agent count, provider name, or failure summary.",
                primaryEvidence));
        }

        if (request.ProviderRepairDiagnostic is { HasRepairOutcome: true } repair &&
            (string.IsNullOrWhiteSpace(repair.FallbackProviderName) ||
             string.IsNullOrWhiteSpace(repair.FallbackModel)))
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.ProviderRepairInconsistent,
                "Provider repair diagnostics report a repair outcome without fallback provider and model metadata.",
                primaryEvidence));
        }
    }

    private static void AddNoProgressContradictions(
        RuntimeEvidenceConsistencyVerificationRequest request,
        ProcessDriverEvidenceReference primaryEvidence,
        List<ProcessDriverDiagnostic> diagnostics)
    {
        if (request.NoProgressDiagnostic is { HasSignal: true } noProgress &&
            string.IsNullOrWhiteSpace(noProgress.Fingerprint))
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.NoProgressFingerprintMissing,
                "No-progress retry diagnostics must include a stable fingerprint when no progress is signaled.",
                primaryEvidence));
        }

        if (request.NoProgressDiagnostic is { HasSignal: true } signal &&
            (!signal.ExecutionRunId.HasValue ||
             string.IsNullOrWhiteSpace(signal.ToolSignature) ||
             string.IsNullOrWhiteSpace(signal.ArtifactValidationFingerprint) ||
             string.IsNullOrWhiteSpace(signal.MutationDelta) ||
             string.IsNullOrWhiteSpace(signal.ProofDelta)))
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.NoProgressFingerprintMissing,
                "No-progress retry diagnostics must include execution id, tool signature, artifact fingerprint, mutation delta, and proof delta.",
                primaryEvidence));
        }
    }

    private static void AddProjectionContradictions(
        RuntimeEvidenceConsistencyVerificationRequest request,
        ProcessDriverEvidenceReference primaryEvidence,
        List<ProcessDriverDiagnostic> diagnostics)
    {
        if (request.ProjectionSourceOrder.Count > 0 &&
            !ProcessArtifactProjectionEvidenceDescriptorRules.IsDefaultProjectionOrder(
                request.ProjectionSourceOrder.Select(source => source.SourceKind).ToArray()))
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.ProjectionOrderDrift,
                "Artifact projection source order differs from the Core default projection order.",
                primaryEvidence));
        }

        var duplicateSourceKinds = request.ProjectionSourceOrder
            .GroupBy(source => source.SourceKind)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateSourceKinds.Length > 0)
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Error,
                ProcessDriverDiagnosticCategory.ProjectionOrderDrift,
                "Artifact projection source order contains duplicate source kinds.",
                primaryEvidence));
        }
    }
}
