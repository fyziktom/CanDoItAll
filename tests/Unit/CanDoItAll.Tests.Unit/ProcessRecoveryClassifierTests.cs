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
}
