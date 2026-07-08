using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public interface IProcessRecoveryClassifier
{
    ProcessRecoveryDecisionReceipt ClassifyBlocked(ProcessRecoveryClassificationInput input);
}

public sealed class ProcessRecoveryClassifier(ProcessRecoveryClassifierOptions? options = null) : IProcessRecoveryClassifier
{
    public static ProcessRecoveryClassifier Default { get; } = new();

    private readonly ProcessRecoveryClassifierOptions options = options ?? ProcessRecoveryClassifierOptions.Default;

    public ProcessRecoveryDecisionReceipt ClassifyBlocked(ProcessRecoveryClassificationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var diagnostics = input.Diagnostics
            .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic.Code))
            .ToArray();
        var fingerprint = CreateDiagnosticFingerprint(input.SourceDiagnosticCode, diagnostics);
        var safeRetryAttempt = CountAutomaticSafeRetryReceipts(input) + 1;
        var sameFingerprintAttempt = CountSameFingerprintSafeRetryReceipts(input, fingerprint) + 1;
        if (CanUseCurrentStepSafeRetry(input, diagnostics) &&
            safeRetryAttempt <= options.MaxAutomaticSafeReworksPerStep &&
            sameFingerprintAttempt <= options.MaxSameDiagnosticFingerprintAutomaticReworks)
        {
            return new ProcessRecoveryDecisionReceipt(
                input.FailureCategory,
                ProcessRecoveryDecisionKind.SafeRetry,
                input.SourceDiagnosticCode,
                "process.current-step-safe-retry",
                $"Safe/idempotent completion-gate diagnostics qualify for bounded current-step retry. Diagnostic fingerprint '{fingerprint}', automatic retry {safeRetryAttempt}/{options.MaxAutomaticSafeReworksPerStep}, same-fingerprint retry {sameFingerprintAttempt}/{options.MaxSameDiagnosticFingerprintAutomaticReworks}.")
            {
                RouteKind = ProcessRecoveryRouteKind.CurrentStepRetry,
                ResponsibleStepInstanceId = input.StepInstanceId,
                DiagnosticFingerprint = fingerprint,
                AutomaticRetryAttempt = safeRetryAttempt,
                MaximumAutomaticRetryAttempts = options.MaxAutomaticSafeReworksPerStep,
                SameDiagnosticFingerprintAttempt = sameFingerprintAttempt,
                MaximumSameDiagnosticFingerprintAttempts = options.MaxSameDiagnosticFingerprintAutomaticReworks
            };
        }

        var budgetExhausted = IsCompletionGateFailure(input, diagnostics) &&
                              AreAllDiagnosticsSafeAndIdempotent(diagnostics) &&
                              (safeRetryAttempt > options.MaxAutomaticSafeReworksPerStep ||
                               sameFingerprintAttempt > options.MaxSameDiagnosticFingerprintAutomaticReworks);
        var policy = budgetExhausted
            ? "process.current-step-safe-retry-budget-exhausted"
            : ResolveRecoveryPolicy(input.DefaultRouteKind);
        var reason = budgetExhausted
            ? $"Safe/idempotent completion-gate diagnostics exhausted automatic current-step retry budget. Diagnostic fingerprint '{fingerprint}', automatic retry {safeRetryAttempt}/{options.MaxAutomaticSafeReworksPerStep}, same-fingerprint retry {sameFingerprintAttempt}/{options.MaxSameDiagnosticFingerprintAutomaticReworks}. Manager review is required before another rework."
            : ResolveRecoveryReason(input.DefaultRouteKind);

        return new ProcessRecoveryDecisionReceipt(
            input.FailureCategory,
            ProcessRecoveryDecisionKind.ManagerRequired,
            input.SourceDiagnosticCode,
            policy,
            reason)
        {
            RouteKind = input.DefaultRouteKind,
            ResponsibleStepInstanceId = input.DefaultResponsibleStepInstanceId,
            DiagnosticFingerprint = fingerprint,
            AutomaticRetryAttempt = safeRetryAttempt,
            MaximumAutomaticRetryAttempts = options.MaxAutomaticSafeReworksPerStep,
            SameDiagnosticFingerprintAttempt = sameFingerprintAttempt,
            MaximumSameDiagnosticFingerprintAttempts = options.MaxSameDiagnosticFingerprintAutomaticReworks
        };
    }

    private bool CanUseCurrentStepSafeRetry(
        ProcessRecoveryClassificationInput input,
        IReadOnlyList<StrategyResultDiagnosticReceipt> diagnostics)
    {
        return input.DefaultRouteKind == ProcessRecoveryRouteKind.ManagerAction &&
               IsCompletionGateFailure(input, diagnostics) &&
               AreAllDiagnosticsSafeAndIdempotent(diagnostics) &&
               !diagnostics.Any(IsPolicyOrCapabilityDenial);
    }

    private static bool IsCompletionGateFailure(
        ProcessRecoveryClassificationInput input,
        IReadOnlyList<StrategyResultDiagnosticReceipt> diagnostics)
    {
        return input.FailureCategory == ProcessFailureCategory.ProductCompletionGate ||
               diagnostics.Count > 0 &&
               diagnostics.All(diagnostic => IsCompletionGateDiagnosticCode(diagnostic.Code));
    }

    private static bool AreAllDiagnosticsSafeAndIdempotent(
        IReadOnlyList<StrategyResultDiagnosticReceipt> diagnostics)
    {
        return diagnostics.Count > 0 &&
               diagnostics.All(diagnostic =>
                   diagnostic.RetrySafety == ProcessDiagnosticRetrySafety.SafeToRetry &&
                   diagnostic.Idempotency == ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static bool IsPolicyOrCapabilityDenial(StrategyResultDiagnosticReceipt diagnostic)
    {
        var code = diagnostic.Code;
        return code.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
               code.Contains("policy", StringComparison.OrdinalIgnoreCase) ||
               code.Contains("rights", StringComparison.OrdinalIgnoreCase) ||
               code.Contains("agent_rights", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCompletionGateDiagnosticCode(string code)
    {
        return code.StartsWith("process.adapter.product_", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("process.adapter.produced_artifact_", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("process.adapter.ungrounded_", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "process.adapter.required_tool_receipt_missing", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "process.adapter.completed_outcome_declares_unresolved_blocker", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountAutomaticSafeRetryReceipts(ProcessRecoveryClassificationInput input)
    {
        return input.PriorStepReceipts.Count(receipt =>
            receipt.StepInstanceId == input.StepInstanceId &&
            receipt.RecoveryDecision?.DecisionKind == ProcessRecoveryDecisionKind.SafeRetry &&
            receipt.RecoveryDecision.RouteKind == ProcessRecoveryRouteKind.CurrentStepRetry);
    }

    private static int CountSameFingerprintSafeRetryReceipts(
        ProcessRecoveryClassificationInput input,
        string fingerprint)
    {
        return input.PriorStepReceipts.Count(receipt =>
            receipt.StepInstanceId == input.StepInstanceId &&
            receipt.RecoveryDecision?.DecisionKind == ProcessRecoveryDecisionKind.SafeRetry &&
            receipt.RecoveryDecision.RouteKind == ProcessRecoveryRouteKind.CurrentStepRetry &&
            string.Equals(receipt.RecoveryDecision.DiagnosticFingerprint, fingerprint, StringComparison.Ordinal));
    }

    private static string CreateDiagnosticFingerprint(
        string sourceDiagnosticCode,
        IReadOnlyList<StrategyResultDiagnosticReceipt> diagnostics)
    {
        var parts = diagnostics.Count == 0
            ? [sourceDiagnosticCode]
            : diagnostics
                .Select(diagnostic => $"{diagnostic.Code}:{diagnostic.EvidenceHash}")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", parts)));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ResolveRecoveryPolicy(ProcessRecoveryRouteKind routeKind)
    {
        return routeKind switch
        {
            ProcessRecoveryRouteKind.UpstreamStepRework => "process.upstream-artifact-rework-required",
            ProcessRecoveryRouteKind.ChildRunPropagation => "process.child-run-manager-review-required",
            ProcessRecoveryRouteKind.TemplateRepair => "process.template-or-policy-repair-required",
            ProcessRecoveryRouteKind.TerminalBlock => "process.terminal-failure",
            _ => "process.manager-review-required"
        };
    }

    private static string ResolveRecoveryReason(ProcessRecoveryRouteKind routeKind)
    {
        return routeKind switch
        {
            ProcessRecoveryRouteKind.UpstreamStepRework => "A required input artifact is still missing; the manager must rework the responsible upstream producer before this step is retried.",
            ProcessRecoveryRouteKind.ChildRunPropagation => "The step is blocked by child-run state and requires manager review of the child process boundary.",
            ProcessRecoveryRouteKind.TemplateRepair => "The blocker points to a process policy or template contract and requires manager repair before another execution.",
            ProcessRecoveryRouteKind.TerminalBlock => "The strategy result failed the step and no automatic recovery decision was applied.",
            _ => "The strategy result blocked the step and requires manager or operator decision before rework."
        };
    }
}

public sealed record ProcessRecoveryClassifierOptions(
    int MaxAutomaticSafeReworksPerStep,
    int MaxSameDiagnosticFingerprintAutomaticReworks)
{
    public static ProcessRecoveryClassifierOptions Default { get; } = new(3, 1);
}

public sealed record ProcessRecoveryClassificationInput(
    ProcessStepInstanceId StepInstanceId,
    ProcessFailureCategory FailureCategory,
    string SourceDiagnosticCode,
    ProcessRecoveryRouteKind DefaultRouteKind,
    ProcessStepInstanceId? DefaultResponsibleStepInstanceId,
    IReadOnlyList<StrategyResultDiagnosticReceipt> Diagnostics,
    IReadOnlyList<StrategyResultReceipt> PriorStepReceipts);
