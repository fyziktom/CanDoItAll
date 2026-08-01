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
    private const string ReplayScopeReviewPolicy =
        "process.current-step-replay-scope-review-required";

    public static ProcessRecoveryClassifier Default { get; } = new();

    private readonly ProcessRecoveryClassifierOptions options = options ?? ProcessRecoveryClassifierOptions.Default;

    public ProcessRecoveryDecisionReceipt ClassifyBlocked(ProcessRecoveryClassificationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var diagnostics = input.Diagnostics
            .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic.Code))
            .ToArray();
        var recoveryCandidateAttempt = CountReplayScopeReviewReceipts(input) + 1;
        var diagnosticIdentity = SelectMostPersistentDiagnosticIdentity(input, diagnostics);
        var fingerprint = diagnosticIdentity.Fingerprint;
        var sameFingerprintAttempt = diagnosticIdentity.PriorRetryOccurrences + 1;
        var relatedChildRunId = ResolveRelatedChildRunId(input.SourceDiagnosticCode, diagnostics);
        if (CanRequestReplayScopeReview(input, diagnostics) &&
            recoveryCandidateAttempt <= options.MaxAutomaticSafeReworksPerStep &&
            sameFingerprintAttempt <= options.MaxSameDiagnosticFingerprintAutomaticReworks)
        {
            return new ProcessRecoveryDecisionReceipt(
                input.FailureCategory,
                ProcessRecoveryDecisionKind.ManagerRequired,
                input.SourceDiagnosticCode,
                ReplayScopeReviewPolicy,
                $"Safe/idempotent completion-gate diagnostics are eligible for bounded recovery only after the application recovery policy verifies the whole assignment is replay-safe. Persistent diagnostic identity '{fingerprint}', recovery candidate {recoveryCandidateAttempt}/{options.MaxAutomaticSafeReworksPerStep}, identity occurrence {sameFingerprintAttempt}/{options.MaxSameDiagnosticFingerprintAutomaticReworks}.")
            {
                RouteKind = input.DefaultRouteKind,
                ResponsibleStepInstanceId = input.DefaultResponsibleStepInstanceId,
                DiagnosticFingerprint = fingerprint,
                AutomaticRetryAttempt = recoveryCandidateAttempt,
                MaximumAutomaticRetryAttempts = options.MaxAutomaticSafeReworksPerStep,
                SameDiagnosticFingerprintAttempt = sameFingerprintAttempt,
                MaximumSameDiagnosticFingerprintAttempts = options.MaxSameDiagnosticFingerprintAutomaticReworks,
                RelatedChildRunId = relatedChildRunId
            };
        }

        var budgetExhausted = IsReplayScopeRecoveryCandidate(input, diagnostics) &&
                               AreAllDiagnosticsSafeAndIdempotent(diagnostics) &&
                              (recoveryCandidateAttempt > options.MaxAutomaticSafeReworksPerStep ||
                               sameFingerprintAttempt > options.MaxSameDiagnosticFingerprintAutomaticReworks);
        var policy = budgetExhausted
            ? "process.current-step-safe-retry-budget-exhausted"
            : ResolveRecoveryPolicy(input.DefaultRouteKind);
        var reason = budgetExhausted
            ? $"Safe/idempotent completion-gate diagnostics exhausted the bounded replay-scope recovery budget. Persistent diagnostic identity '{fingerprint}', recovery candidate {recoveryCandidateAttempt}/{options.MaxAutomaticSafeReworksPerStep}, identity occurrence {sameFingerprintAttempt}/{options.MaxSameDiagnosticFingerprintAutomaticReworks}. Manager review is required before another rework."
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
            AutomaticRetryAttempt = recoveryCandidateAttempt,
            MaximumAutomaticRetryAttempts = options.MaxAutomaticSafeReworksPerStep,
            SameDiagnosticFingerprintAttempt = sameFingerprintAttempt,
            MaximumSameDiagnosticFingerprintAttempts = options.MaxSameDiagnosticFingerprintAutomaticReworks,
            RelatedChildRunId = relatedChildRunId
        };
    }

    private static ProcessRunId? ResolveRelatedChildRunId(
        string sourceDiagnosticCode,
        IReadOnlyList<StrategyResultDiagnosticReceipt> diagnostics)
    {
        var relatedChildRunIds = diagnostics
            .Where(diagnostic =>
                string.Equals(
                    diagnostic.Code,
                    sourceDiagnosticCode,
                    StringComparison.OrdinalIgnoreCase) &&
                diagnostic.RelatedChildRunId is not null)
            .Select(diagnostic => diagnostic.RelatedChildRunId!.Value)
            .Distinct()
            .ToArray();
        return relatedChildRunIds.Length == 1
            ? relatedChildRunIds[0]
            : null;
    }

    private static bool CanRequestReplayScopeReview(
        ProcessRecoveryClassificationInput input,
        IReadOnlyList<StrategyResultDiagnosticReceipt> diagnostics)
    {
        return input.DefaultRouteKind == ProcessRecoveryRouteKind.ManagerAction &&
               IsReplayScopeRecoveryCandidate(input, diagnostics) &&
               AreAllDiagnosticsSafeAndIdempotent(diagnostics) &&
               !diagnostics.Any(IsPolicyOrCapabilityDenial);
    }

    private static bool IsReplayScopeRecoveryCandidate(
        ProcessRecoveryClassificationInput input,
        IReadOnlyList<StrategyResultDiagnosticReceipt> diagnostics)
    {
        return IsCompletionGateFailure(input, diagnostics) ||
               input.FailureCategory == ProcessFailureCategory.AdapterRetryable &&
               string.Equals(
                   input.SourceDiagnosticCode,
                   ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionBeforeSideEffects,
                   StringComparison.Ordinal) &&
               diagnostics.Count > 0 &&
               diagnostics.All(diagnostic =>
                   string.Equals(
                       diagnostic.Code,
                       ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionBeforeSideEffects,
                       StringComparison.Ordinal) &&
                   diagnostic.RelatedChildRunId is null);
    }

    private static bool IsCompletionGateFailure(
        ProcessRecoveryClassificationInput input,
        IReadOnlyList<StrategyResultDiagnosticReceipt> diagnostics)
    {
        return input.FailureCategory == ProcessFailureCategory.ProductCompletionGate ||
               diagnostics.Count > 0 &&
               diagnostics.All(diagnostic => ProcessCompletionGateDiagnosticCatalog.IsCompletionGateDiagnosticCode(diagnostic.Code));
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

    private static int CountReplayScopeReviewReceipts(ProcessRecoveryClassificationInput input)
    {
        return input.PriorStepReceipts.Count(receipt =>
            receipt.StepInstanceId == input.StepInstanceId &&
            IsReplayScopeReviewReceipt(receipt));
    }

    private static DiagnosticIdentityRecurrence SelectMostPersistentDiagnosticIdentity(
        ProcessRecoveryClassificationInput input,
        IReadOnlyList<StrategyResultDiagnosticReceipt> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return new DiagnosticIdentityRecurrence(
                CreateDiagnosticIdentityFingerprint(input.SourceDiagnosticCode, string.Empty),
                0);
        }

        var retryReceipts = input.PriorStepReceipts
            .Where(receipt =>
                receipt.StepInstanceId == input.StepInstanceId &&
                IsReplayScopeReviewReceipt(receipt))
            .ToArray();

        return diagnostics
            .Select(CreateDiagnosticIdentityFingerprint)
            .Distinct(StringComparer.Ordinal)
            .Select(fingerprint => new DiagnosticIdentityRecurrence(
                fingerprint,
                retryReceipts.Count(receipt => ReceiptContainsDiagnosticIdentity(receipt, fingerprint))))
            .OrderByDescending(recurrence => recurrence.PriorRetryOccurrences)
            .ThenBy(recurrence => recurrence.Fingerprint, StringComparer.Ordinal)
            .First();
    }

    private static bool IsReplayScopeReviewReceipt(StrategyResultReceipt receipt)
    {
        return receipt.RecoveryDecision is
        {
            DecisionKind: ProcessRecoveryDecisionKind.SafeRetry,
            RouteKind: ProcessRecoveryRouteKind.CurrentStepRetry
        } ||
        receipt.RecoveryDecision is
        {
            DecisionKind: ProcessRecoveryDecisionKind.ManagerRequired
        } decision &&
        string.Equals(
            decision.Policy,
            ReplayScopeReviewPolicy,
            StringComparison.Ordinal);
    }

    private static bool ReceiptContainsDiagnosticIdentity(
        StrategyResultReceipt receipt,
        string fingerprint)
    {
        return receipt.Diagnostics.Any(diagnostic => string.Equals(
            CreateDiagnosticIdentityFingerprint(diagnostic),
            fingerprint,
            StringComparison.Ordinal));
    }

    private static string CreateDiagnosticIdentityFingerprint(StrategyResultDiagnosticReceipt diagnostic)
    {
        var stableEvidenceHash = UsesCodeOnlyDiagnosticIdentity(diagnostic.Code)
            ? string.Empty
            : diagnostic.EvidenceHash;
        return CreateDiagnosticIdentityFingerprint(diagnostic.Code, stableEvidenceHash);
    }

    private static bool UsesCodeOnlyDiagnosticIdentity(string code)
    {
        return string.Equals(
                   code,
                   ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionBeforeSideEffects,
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   code,
                   "process.adapter.completed_outcome_declares_unresolved_blocker",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   code,
                   ProcessCompletionDiagnosticCodes.ArtifactPayloadSchemaInvalid,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateDiagnosticIdentityFingerprint(string code, string evidenceHash)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{code.Trim()}:{evidenceHash.Trim()}"));
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

internal readonly record struct DiagnosticIdentityRecurrence(
    string Fingerprint,
    int PriorRetryOccurrences);

public sealed record ProcessRecoveryClassifierOptions(
    int MaxAutomaticSafeReworksPerStep,
    int MaxSameDiagnosticFingerprintAutomaticReworks)
{
    public static ProcessRecoveryClassifierOptions Default { get; } = new(4, 1);
}

public sealed record ProcessRecoveryClassificationInput(
    ProcessStepInstanceId StepInstanceId,
    ProcessFailureCategory FailureCategory,
    string SourceDiagnosticCode,
    ProcessRecoveryRouteKind DefaultRouteKind,
    ProcessStepInstanceId? DefaultResponsibleStepInstanceId,
    IReadOnlyList<StrategyResultDiagnosticReceipt> Diagnostics,
    IReadOnlyList<StrategyResultReceipt> PriorStepReceipts);
