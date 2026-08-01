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
    public void RecoveryClassifier_requires_assignment_replay_scope_review_for_safe_idempotent_completion_gate()
    {
        var classifier = new ProcessRecoveryClassifier(new ProcessRecoveryClassifierOptions(3, 1));
        var result = classifier.ClassifyBlocked(CreateInput(Diagnostic(
            "process.adapter.product_required_tool_receipt_missing",
            "sha256:missing-tool")));

        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, result.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.ManagerAction, result.RouteKind);
        Assert.Equal("process.current-step-replay-scope-review-required", result.Policy);
        Assert.Equal(StepId, result.ResponsibleStepInstanceId);
        Assert.Equal(1, result.AutomaticRetryAttempt);
        Assert.Equal(1, result.SameDiagnosticFingerprintAttempt);
        Assert.StartsWith("sha256:", result.DiagnosticFingerprint, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoveryClassifier_requires_assignment_replay_scope_review_for_branch_defect_evidence_gap()
    {
        var classifier = new ProcessRecoveryClassifier(new ProcessRecoveryClassifierOptions(3, 1));
        var result = classifier.ClassifyBlocked(CreateInput(
            Diagnostic(
                "process.adapter.branch_outcome_defect_evidence_missing",
                "sha256:missing-defect-evidence"),
            failureCategory: ProcessFailureCategory.Unknown));

        Assert.Equal(ProcessFailureCategory.Unknown, result.FailureCategory);
        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, result.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.ManagerAction, result.RouteKind);
        Assert.Equal("process.current-step-replay-scope-review-required", result.Policy);
    }

    [Fact]
    public void RecoveryClassifier_requires_assignment_replay_scope_review_for_runtime_lifecycle_gap()
    {
        var classifier = new ProcessRecoveryClassifier(new ProcessRecoveryClassifierOptions(3, 1));
        var result = classifier.ClassifyBlocked(CreateInput(
            Diagnostic(
                "process.adapter.runtime_lifecycle_correlation_missing",
                "sha256:runtime-lifecycle"),
            failureCategory: ProcessFailureCategory.Unknown));

        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, result.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.ManagerAction, result.RouteKind);
        Assert.Equal("process.current-step-replay-scope-review-required", result.Policy);
    }

    [Fact]
    public void RecoveryClassifier_allows_one_schema_artifact_correction_then_requires_manager_review()
    {
        var classifier = new ProcessRecoveryClassifier(new ProcessRecoveryClassifierOptions(3, 1));
        var firstDiagnostic = Diagnostic(
            ProcessCompletionDiagnosticCodes.ArtifactPayloadSchemaInvalid,
            "sha256:missing-initialization-plan");

        var first = classifier.ClassifyBlocked(CreateInput(
            firstDiagnostic,
            failureCategory: ProcessFailureCategory.Unknown));
        var priorReceipt = CreateReceipt(firstDiagnostic, first, "sha256:first-schema-correction");
        var secondDiagnostic = Diagnostic(
            ProcessCompletionDiagnosticCodes.ArtifactPayloadSchemaInvalid,
            "sha256:missing-validation-rules");
        var second = classifier.ClassifyBlocked(CreateInput(
            secondDiagnostic,
            [priorReceipt],
            ProcessFailureCategory.Unknown));

        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, first.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.ManagerAction, first.RouteKind);
        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, second.DecisionKind);
        Assert.Equal("process.current-step-safe-retry-budget-exhausted", second.Policy);
        Assert.Equal(2, second.SameDiagnosticFingerprintAttempt);
        Assert.Equal(first.DiagnosticFingerprint, second.DiagnosticFingerprint);
    }

    [Fact]
    public void RecoveryClassifier_keeps_distinct_non_schema_diagnostic_evidence_as_separate_identities()
    {
        var classifier = new ProcessRecoveryClassifier(new ProcessRecoveryClassifierOptions(3, 1));
        var firstDiagnostic = Diagnostic(
            "process.adapter.product_required_tool_receipt_missing",
            "sha256:missing-build-receipt");
        var first = classifier.ClassifyBlocked(CreateInput(firstDiagnostic));
        var priorReceipt = CreateReceipt(firstDiagnostic, first, "sha256:first-retry");
        var secondDiagnostic = Diagnostic(
            "process.adapter.product_required_tool_receipt_missing",
            "sha256:missing-validation-receipt");

        var second = classifier.ClassifyBlocked(CreateInput(secondDiagnostic, [priorReceipt]));

        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, second.DecisionKind);
        Assert.Equal("process.current-step-replay-scope-review-required", second.Policy);
        Assert.Equal(2, second.AutomaticRetryAttempt);
        Assert.Equal(1, second.SameDiagnosticFingerprintAttempt);
        Assert.NotEqual(first.DiagnosticFingerprint, second.DiagnosticFingerprint);
    }

    [Fact]
    public void RecoveryClassifier_routes_direct_runtime_branch_selection_to_one_replay_scope_review()
    {
        var classifier = new ProcessRecoveryClassifier();
        var diagnostic = Diagnostic(
            ProcessCompletionDiagnosticCodes.RuntimeRoutedBranchSelectedDirectly,
            "sha256:runtime-owned-branch");

        var first = classifier.ClassifyBlocked(CreateInput(
            diagnostic,
            failureCategory: ProcessFailureCategory.Unknown));

        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, first.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.ManagerAction, first.RouteKind);
        Assert.Equal("process.current-step-replay-scope-review-required", first.Policy);

        var priorReceipt = CreateReceipt(diagnostic, first, "sha256:first-runtime-owned-branch-retry");
        var repeated = classifier.ClassifyBlocked(CreateInput(
            diagnostic,
            [priorReceipt],
            ProcessFailureCategory.Unknown));

        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, repeated.DecisionKind);
        Assert.Equal("process.current-step-safe-retry-budget-exhausted", repeated.Policy);
    }

    [Fact]
    public void RecoveryClassifier_routes_managed_artifact_completion_retry_to_replay_scope_review()
    {
        var classifier = new ProcessRecoveryClassifier();
        var result = classifier.ClassifyBlocked(CreateInput(
            Diagnostic(
                "process.adapter.managed_artifact_missing_primary_output_retry",
                "sha256:managed-artifact-timeout-recovery"),
            failureCategory: ProcessFailureCategory.AdapterRetryable));

        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, result.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.ManagerAction, result.RouteKind);
        Assert.Equal("process.current-step-replay-scope-review-required", result.Policy);
    }

    [Fact]
    public void RecoveryClassifier_default_requires_manager_when_same_diagnostic_survives_one_retry()
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
            ProcessRuntimeStepStatus.Blocked,
            "sha256:first-retry",
            diagnostics: [diagnostic],
            recoveryDecision: first);

        var second = classifier.ClassifyBlocked(CreateInput(diagnostic, [priorReceipt]));
        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, second.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.ManagerAction, second.RouteKind);
        Assert.Equal(2, second.SameDiagnosticFingerprintAttempt);
        Assert.Equal(1, second.MaximumSameDiagnosticFingerprintAttempts);
        Assert.Equal(first.DiagnosticFingerprint, second.DiagnosticFingerprint);
    }

    [Fact]
    public void RecoveryClassifier_treats_changed_blocker_prose_as_same_diagnostic_identity()
    {
        const string diagnosticCode = "process.adapter.completed_outcome_declares_unresolved_blocker";
        var classifier = new ProcessRecoveryClassifier();
        var firstDiagnostic = Diagnostic(diagnosticCode, "sha256:first-blocker-prose");
        var first = classifier.ClassifyBlocked(CreateInput(firstDiagnostic));
        var priorReceipt = CreateReceipt(firstDiagnostic, first, "sha256:first-blocker-result");
        var secondDiagnostic = Diagnostic(diagnosticCode, "sha256:rewritten-blocker-prose");

        var second = classifier.ClassifyBlocked(CreateInput(secondDiagnostic, [priorReceipt]));

        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, second.DecisionKind);
        Assert.Equal("process.current-step-safe-retry-budget-exhausted", second.Policy);
        Assert.Equal(2, second.SameDiagnosticFingerprintAttempt);
        Assert.Equal(first.DiagnosticFingerprint, second.DiagnosticFingerprint);
    }

    [Fact]
    public void RecoveryClassifier_escalates_when_one_diagnostic_persists_while_incidental_diagnostics_change()
    {
        var classifier = new ProcessRecoveryClassifier();
        var persistent = Diagnostic(
            "process.adapter.tool_receipt_evidence_content_rejected",
            "sha256:persistent-fatal-state");
        var removed = Diagnostic(
            "process.adapter.product_required_file_content_missing",
            "sha256:starter-content");
        var first = classifier.ClassifyBlocked(CreateInput([persistent, removed]));
        var firstReceipt = CreateReceipt([persistent, removed], first, "sha256:first-retry");
        var added = Diagnostic(
            "process.adapter.runtime_lifecycle_correlation_missing",
            "sha256:new-lifecycle-gap");

        var second = classifier.ClassifyBlocked(CreateInput([persistent, added], [firstReceipt]));

        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, second.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.ManagerAction, second.RouteKind);
        Assert.Equal("process.current-step-safe-retry-budget-exhausted", second.Policy);
        Assert.Equal(2, second.SameDiagnosticFingerprintAttempt);
        Assert.Equal(1, second.MaximumSameDiagnosticFingerprintAttempts);
        Assert.StartsWith("sha256:", second.DiagnosticFingerprint, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoveryClassifier_default_allows_bounded_follow_ups_when_diagnostics_are_replaced()
    {
        var classifier = new ProcessRecoveryClassifier();
        var priorReceipts = new List<StrategyResultReceipt>();
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var diagnostic = Diagnostic(
                "process.adapter.product_required_file_content_missing",
                $"sha256:progress-state-{attempt}");
            var decision = classifier.ClassifyBlocked(CreateInput(diagnostic, priorReceipts));
            Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, decision.DecisionKind);
            Assert.Equal("process.current-step-replay-scope-review-required", decision.Policy);
            Assert.Equal(attempt, decision.AutomaticRetryAttempt);
            Assert.Equal(1, decision.SameDiagnosticFingerprintAttempt);
            priorReceipts.Add(CreateReceipt(diagnostic, decision, $"sha256:retry-{attempt}"));
        }

        var exhaustedDiagnostic = Diagnostic(
            "process.adapter.product_required_file_content_missing",
            "sha256:progress-state-5");
        var exhausted = classifier.ClassifyBlocked(CreateInput(exhaustedDiagnostic, priorReceipts));

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
            ProcessRuntimeStepStatus.Blocked,
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

    [Fact]
    public void RecoveryClassifier_preserves_typed_child_identity_for_child_propagation()
    {
        var childRunId = ProcessRunId.New();
        var diagnostic = new StrategyResultDiagnosticReceipt(
            "process.adapter.subprocess_child_blocked",
            StrategyDiagnosticSensitivity.Normal,
            "sha256:blocked-child",
            "The linked child run is blocked.",
            RestrictedEvidenceReference: null,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent)
        {
            RelatedChildRunId = childRunId
        };
        var result = new ProcessRecoveryClassifier().ClassifyBlocked(
            new ProcessRecoveryClassificationInput(
                StepId,
                ProcessFailureCategory.ChildRunBlocked,
                diagnostic.Code,
                ProcessRecoveryRouteKind.ChildRunPropagation,
                StepId,
                [diagnostic],
                []));

        Assert.Equal(ProcessRecoveryDecisionKind.ManagerRequired, result.DecisionKind);
        Assert.Equal(ProcessRecoveryRouteKind.ChildRunPropagation, result.RouteKind);
        Assert.Equal(childRunId, result.RelatedChildRunId);
    }

    [Fact]
    public void RecoveryClassifier_counts_attested_transient_retries_by_code_not_execution_detail()
    {
        var classifier = new ProcessRecoveryClassifier();
        var firstDiagnostic = Diagnostic(
            ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionBeforeSideEffects,
            "sha256:first-execution-run");
        var first = classifier.ClassifyBlocked(CreateInput(
            firstDiagnostic,
            failureCategory: ProcessFailureCategory.AdapterRetryable));
        var priorReceipt = CreateReceipt(firstDiagnostic, first, "sha256:first-transient-result");
        var repeatedDiagnostic = Diagnostic(
            ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionBeforeSideEffects,
            "sha256:second-execution-run");

        var repeated = classifier.ClassifyBlocked(CreateInput(
            repeatedDiagnostic,
            [priorReceipt],
            ProcessFailureCategory.AdapterRetryable));

        Assert.Equal("process.current-step-replay-scope-review-required", first.Policy);
        Assert.Equal(first.DiagnosticFingerprint, repeated.DiagnosticFingerprint);
        Assert.Equal(2, repeated.SameDiagnosticFingerprintAttempt);
        Assert.Equal(1, repeated.MaximumSameDiagnosticFingerprintAttempts);
        Assert.Equal("process.current-step-safe-retry-budget-exhausted", repeated.Policy);
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

    private static ProcessRecoveryClassificationInput CreateInput(
        IReadOnlyList<StrategyResultDiagnosticReceipt> diagnostics,
        IReadOnlyList<StrategyResultReceipt>? priorReceipts = null)
    {
        return new ProcessRecoveryClassificationInput(
            StepId,
            ProcessFailureCategory.ProductCompletionGate,
            diagnostics[0].Code,
            ProcessRecoveryRouteKind.ManagerAction,
            StepId,
            diagnostics,
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
            ProcessRuntimeStepStatus.Blocked,
            resultHash,
            diagnostics: [diagnostic],
            recoveryDecision: decision);
    }

    private static StrategyResultReceipt CreateReceipt(
        IReadOnlyList<StrategyResultDiagnosticReceipt> diagnostics,
        ProcessRecoveryDecisionReceipt decision,
        string resultHash)
    {
        return new StrategyResultReceipt(
            StepId,
            StrategyId,
            StrategyResultIdempotencyKey.New(),
            StrategyOutcome.NeedsManager,
            ProcessRuntimeStepStatus.Blocked,
            resultHash,
            diagnostics: diagnostics,
            recoveryDecision: decision);
    }
}
