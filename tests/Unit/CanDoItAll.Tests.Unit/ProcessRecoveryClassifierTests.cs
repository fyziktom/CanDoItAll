using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRecoveryClassifierTests
{
    private static readonly ProcessStepInstanceId StepId = new(new Guid("bfec45fa-6ac0-4c29-af6c-01c9499d0629"));
    private static readonly StrategyId StrategyId = new("strategy.execute");

    [Fact]
    public void RecoveryClassifier_routes_safe_idempotent_completion_gate_to_current_step_retry()
    {
        var classifier = new ProcessRecoveryClassifier(new ProcessRecoveryClassifierOptions(3, 1));
        var result = classifier.ClassifyBlocked(CreateInput(Diagnostic(
            "process.adapter.product_required_tool_receipt_missing",
            "sha256:missing-tool")));

        Assert.Equal(ProcessRecoveryDecisionKind.SafeRetry, result.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.CurrentStepRetry, result.RouteKind);
        Assert.Equal("process.current-step-safe-retry", result.Policy);
        Assert.Equal(StepId, result.ResponsibleStepInstanceId);
        Assert.Equal(1, result.AutomaticRetryAttempt);
        Assert.Equal(1, result.SameDiagnosticFingerprintAttempt);
        Assert.StartsWith("sha256:", result.DiagnosticFingerprint, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoveryClassifier_routes_branch_defect_evidence_gap_to_current_step_retry()
    {
        var classifier = new ProcessRecoveryClassifier(new ProcessRecoveryClassifierOptions(3, 1));
        var result = classifier.ClassifyBlocked(CreateInput(
            Diagnostic(
                "process.adapter.branch_outcome_defect_evidence_missing",
                "sha256:missing-defect-evidence"),
            failureCategory: ProcessFailureCategory.Unknown));

        Assert.Equal(ProcessFailureCategory.Unknown, result.FailureCategory);
        Assert.Equal(ProcessRecoveryDecisionKind.SafeRetry, result.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.CurrentStepRetry, result.RouteKind);
        Assert.Equal("process.current-step-safe-retry", result.Policy);
    }

    [Fact]
    public void RecoveryClassifier_routes_runtime_lifecycle_gap_to_current_step_retry()
    {
        var classifier = new ProcessRecoveryClassifier(new ProcessRecoveryClassifierOptions(3, 1));
        var result = classifier.ClassifyBlocked(CreateInput(
            Diagnostic(
                "process.adapter.runtime_lifecycle_correlation_missing",
                "sha256:runtime-lifecycle"),
            failureCategory: ProcessFailureCategory.Unknown));

        Assert.Equal(ProcessRecoveryDecisionKind.SafeRetry, result.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.CurrentStepRetry, result.RouteKind);
        Assert.Equal("process.current-step-safe-retry", result.Policy);
    }

    [Fact]
    public void RecoveryClassifier_routes_managed_artifact_completion_retry_to_current_step_retry()
    {
        var classifier = new ProcessRecoveryClassifier();
        var result = classifier.ClassifyBlocked(CreateInput(
            Diagnostic(
                "process.adapter.managed_artifact_missing_primary_output_retry",
                "sha256:managed-artifact-timeout-recovery"),
            failureCategory: ProcessFailureCategory.AdapterRetryable));

        Assert.Equal(ProcessRecoveryDecisionKind.SafeRetry, result.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.CurrentStepRetry, result.RouteKind);
        Assert.Equal("process.current-step-safe-retry", result.Policy);
    }

    [Fact]
    public void RecoveryClassifier_default_allows_three_same_fingerprint_safe_reworks()
    {
        var classifier = new ProcessRecoveryClassifier();
        var diagnostic = Diagnostic(
            "process.adapter.product_required_file_content_missing",
            "sha256:remaining-product-content");
        var first = classifier.ClassifyBlocked(CreateInput(diagnostic));
        var priorReceipt = new StrategyResultReceipt(
            StepId,
            StrategyId,
            StrategyResultIdempotencyKey.New(),
            StrategyOutcome.NeedsManager,
            ProcessRuntimeStepStatus.Ready,
            "sha256:first-retry",
            diagnostics: [diagnostic],
            recoveryDecision: first);

        var second = classifier.ClassifyBlocked(CreateInput(diagnostic, [priorReceipt]));
        var secondReceipt = new StrategyResultReceipt(
            StepId,
            StrategyId,
            StrategyResultIdempotencyKey.New(),
            StrategyOutcome.NeedsManager,
            ProcessRuntimeStepStatus.Ready,
            "sha256:second-retry",
            diagnostics: [diagnostic],
            recoveryDecision: second);

        var third = classifier.ClassifyBlocked(CreateInput(diagnostic, [priorReceipt, secondReceipt]));

        Assert.Equal(ProcessRecoveryDecisionKind.SafeRetry, second.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.CurrentStepRetry, second.RouteKind);
        Assert.Equal(2, second.SameDiagnosticFingerprintAttempt);
        Assert.Equal(3, second.MaximumSameDiagnosticFingerprintAttempts);
        Assert.Equal(ProcessRecoveryDecisionKind.SafeRetry, third.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.CurrentStepRetry, third.RouteKind);
        Assert.Equal(3, third.SameDiagnosticFingerprintAttempt);
        Assert.Equal(3, third.MaximumSameDiagnosticFingerprintAttempts);
    }

    [Fact]
    public void RecoveryClassifier_default_allows_one_bounded_follow_up_after_diagnostic_progress()
    {
        var classifier = new ProcessRecoveryClassifier();
        var originalDiagnostic = Diagnostic(
            "process.adapter.product_required_file_content_missing",
            "sha256:three-files-remain");
        var priorReceipts = new List<StrategyResultReceipt>();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var decision = classifier.ClassifyBlocked(CreateInput(originalDiagnostic, priorReceipts));
            Assert.Equal(ProcessRecoveryDecisionKind.SafeRetry, decision.DecisionKind);
            priorReceipts.Add(CreateReceipt(originalDiagnostic, decision, $"sha256:retry-{attempt}"));
        }

        var progressedDiagnostic = Diagnostic(
            "process.adapter.product_required_file_content_missing",
            "sha256:two-files-remain");
        var progressFollowUp = classifier.ClassifyBlocked(CreateInput(progressedDiagnostic, priorReceipts));

        Assert.Equal(ProcessRecoveryDecisionKind.SafeRetry, progressFollowUp.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.CurrentStepRetry, progressFollowUp.RouteKind);
        Assert.Equal(4, progressFollowUp.AutomaticRetryAttempt);
        Assert.Equal(4, progressFollowUp.MaximumAutomaticRetryAttempts);
        Assert.Equal(1, progressFollowUp.SameDiagnosticFingerprintAttempt);
        Assert.Equal(3, progressFollowUp.MaximumSameDiagnosticFingerprintAttempts);

        priorReceipts.Add(CreateReceipt(progressedDiagnostic, progressFollowUp, "sha256:progress-follow-up"));
        var exhausted = classifier.ClassifyBlocked(CreateInput(progressedDiagnostic, priorReceipts));

        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, exhausted.DecisionKind);
        Assert.Equal("process.current-step-safe-retry-budget-exhausted", exhausted.Policy);
        Assert.Equal(5, exhausted.AutomaticRetryAttempt);
    }

    [Fact]
    public void RecoveryClassifier_escalates_after_same_fingerprint_budget_exhausted()
    {
        var classifier = new ProcessRecoveryClassifier(new ProcessRecoveryClassifierOptions(3, 1));
        var diagnostic = Diagnostic(
            "process.adapter.product_required_tool_receipt_missing",
            "sha256:missing-tool");
        var first = classifier.ClassifyBlocked(CreateInput(diagnostic));
        var priorReceipt = new StrategyResultReceipt(
            StepId,
            StrategyId,
            StrategyResultIdempotencyKey.New(),
            StrategyOutcome.NeedsManager,
            ProcessRuntimeStepStatus.Ready,
            "sha256:first-retry",
            diagnostics: [diagnostic],
            recoveryDecision: first);

        var second = classifier.ClassifyBlocked(CreateInput(diagnostic, [priorReceipt]));

        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, second.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.ManagerAction, second.RouteKind);
        Assert.Equal("process.current-step-safe-retry-budget-exhausted", second.Policy);
        Assert.Equal(2, second.SameDiagnosticFingerprintAttempt);
        Assert.Equal(first.DiagnosticFingerprint, second.DiagnosticFingerprint);
    }

    [Fact]
    public void RecoveryClassifier_does_not_safe_retry_unsafe_completion_gate()
    {
        var classifier = new ProcessRecoveryClassifier(new ProcessRecoveryClassifierOptions(3, 1));
        var result = classifier.ClassifyBlocked(CreateInput(new StrategyResultDiagnosticReceipt(
            "process.adapter.product_required_file_content_check_invalid",
            StrategyDiagnosticSensitivity.Normal,
            "sha256:invalid-check",
            "Invalid configured product readback check.",
            RestrictedEvidenceReference: null,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Unknown)));

        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, result.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.ManagerAction, result.RouteKind);
        Assert.Equal("process.manager-review-required", result.Policy);
    }

    [Fact]
    public void RecoveryClassifier_does_not_safe_retry_policy_or_tool_denial()
    {
        var classifier = new ProcessRecoveryClassifier(new ProcessRecoveryClassifierOptions(3, 1));
        var result = classifier.ClassifyBlocked(CreateInput(Diagnostic(
            "process.adapter.product_policy_denied",
            "sha256:denied")));

        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, result.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.ManagerAction, result.RouteKind);
        Assert.Equal("process.manager-review-required", result.Policy);
    }

    private static ProcessRecoveryClassificationInput CreateInput(
        StrategyResultDiagnosticReceipt diagnostic,
        IReadOnlyList<StrategyResultReceipt>? priorReceipts = null,
        ProcessFailureCategory failureCategory = ProcessFailureCategory.ProductCompletionGate)
    {
        return new ProcessRecoveryClassificationInput(
            StepId,
            failureCategory,
            diagnostic.Code,
            ProcessRecoveryRouteKind.ManagerAction,
            StepId,
            [diagnostic],
            priorReceipts ?? []);
    }

    private static StrategyResultDiagnosticReceipt Diagnostic(
        string code,
        string evidenceHash)
    {
        return new StrategyResultDiagnosticReceipt(
            code,
            StrategyDiagnosticSensitivity.Normal,
            evidenceHash,
            "Safe completion gate failure.",
            RestrictedEvidenceReference: null,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static StrategyResultReceipt CreateReceipt(
        StrategyResultDiagnosticReceipt diagnostic,
        ProcessRecoveryDecisionReceipt decision,
        string resultHash)
    {
        return new StrategyResultReceipt(
            StepId,
            StrategyId,
            StrategyResultIdempotencyKey.New(),
            StrategyOutcome.NeedsManager,
            ProcessRuntimeStepStatus.Ready,
            resultHash,
            diagnostics: [diagnostic],
            recoveryDecision: decision);
    }
}
