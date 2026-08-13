using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

internal static class LegacyProcessStrategyResultReceiptNormalizer
{
    private const int MaximumRecoveryCounter = 1024;
    private const string Sha256Prefix = "sha256:";
    private const string TruncationSuffix = " [truncated]";

    public static StrategyResultReceipt Normalize(StrategyResultReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        return receipt with
        {
            ResultHash = NormalizeDigest(receipt.ResultHash),
            UserSafeSummary = NormalizePublicText(
                receipt.UserSafeSummary,
                ProcessStrategyResultLimits.MaximumUserSafeSummaryLength),
            Diagnostics = receipt.Diagnostics
                .Take(ProcessStrategyResultLimits.MaximumDiagnostics)
                .Select(NormalizeDiagnostic)
                .ToArray(),
            ProducedArtifacts = receipt.ProducedArtifacts
                .Take(ProcessStrategyResultLimits.MaximumArtifacts)
                .Select(artifact => artifact with
                {
                    ContentHash = NormalizeDigest(artifact.ContentHash)
                })
                .ToArray(),
            RecoveryDecision = NormalizeRecoveryDecision(
                receipt.RecoveryDecision,
                receipt.StepInstanceId)
        };
    }

    private static StrategyResultDiagnosticReceipt NormalizeDiagnostic(
        StrategyResultDiagnosticReceipt diagnostic)
        => diagnostic with
        {
            EvidenceHash = NormalizeDigest(diagnostic.EvidenceHash),
            SafeSummary = NormalizePublicText(
                diagnostic.SafeSummary,
                ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength),
            RestrictedEvidenceReference = NormalizeRestrictedEvidenceReference(
                diagnostic.RestrictedEvidenceReference),
            RelatedChildRunId = diagnostic.RelatedChildRunId is { Value: var relatedChildRunId } &&
                                relatedChildRunId == Guid.Empty
                ? null
                : diagnostic.RelatedChildRunId
        };

    private static ProcessRecoveryDecisionReceipt? NormalizeRecoveryDecision(
        ProcessRecoveryDecisionReceipt? decision,
        ProcessStepInstanceId stepInstanceId)
    {
        if (decision is null)
        {
            return null;
        }

        var fingerprint = NormalizeOptionalDigest(decision.DiagnosticFingerprint);
        var automaticRetry = NormalizeCounterPair(
            decision.AutomaticRetryAttempt,
            decision.MaximumAutomaticRetryAttempts,
            fingerprint.Length > 0);
        var sameFingerprint = NormalizeCounterPair(
            decision.SameDiagnosticFingerprintAttempt,
            decision.MaximumSameDiagnosticFingerprintAttempts,
            fingerprint.Length > 0);

        return decision with
        {
            SourceDiagnosticCode = ProcessPublicReceiptTextPolicy.NormalizePublicToken(
                decision.SourceDiagnosticCode,
                "process.runtime.legacy_recovery"),
            Policy = ProcessPublicReceiptTextPolicy.NormalizePublicToken(
                decision.Policy,
                "process.legacy-recovery"),
            SafeReason = NormalizeRequiredPublicText(
                decision.SafeReason,
                "Legacy recovery decision requires review."),
            RouteKind = NormalizeRoute(decision),
            ResponsibleStepInstanceId = decision.ResponsibleStepInstanceId is { Value: var responsibleStepId } &&
                                        responsibleStepId != Guid.Empty
                ? decision.ResponsibleStepInstanceId
                : stepInstanceId,
            DiagnosticFingerprint = fingerprint,
            AutomaticRetryAttempt = automaticRetry.Attempt,
            MaximumAutomaticRetryAttempts = automaticRetry.Maximum,
            SameDiagnosticFingerprintAttempt = sameFingerprint.Attempt,
            MaximumSameDiagnosticFingerprintAttempts = sameFingerprint.Maximum,
            RelatedChildRunId = decision.RelatedChildRunId is { Value: var relatedChildRunId } &&
                                relatedChildRunId == Guid.Empty
                ? null
                : decision.RelatedChildRunId
        };
    }

    private static ProcessRecoveryRouteKind NormalizeRoute(ProcessRecoveryDecisionReceipt decision)
        => decision.DecisionKind switch
        {
            ProcessRecoveryDecisionKind.SafeRetry => ProcessRecoveryRouteKind.CurrentStepRetry,
            ProcessRecoveryDecisionKind.TerminalBlocked => ProcessRecoveryRouteKind.TerminalBlock,
            ProcessRecoveryDecisionKind.ManagerRequired
                when decision.RouteKind is ProcessRecoveryRouteKind.None or ProcessRecoveryRouteKind.TerminalBlock ||
                     decision.RouteKind == ProcessRecoveryRouteKind.ChildRunPropagation &&
                     decision.RelatedChildRunId is null
                => ProcessRecoveryRouteKind.ManagerAction,
            _ => decision.RouteKind
        };

    private static (int Attempt, int Maximum) NormalizeCounterPair(
        int attempt,
        int maximum,
        bool hasFingerprint)
    {
        if (!hasFingerprint)
        {
            return (0, 0);
        }

        var boundedMaximum = Math.Clamp(maximum, 0, MaximumRecoveryCounter);
        return (Math.Clamp(attempt, 0, boundedMaximum), boundedMaximum);
    }

    private static string NormalizeRequiredPublicText(string? value, string fallback)
    {
        var normalized = NormalizePublicText(
            value,
            ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength);
        return normalized.Length == 0 ? fallback : normalized;
    }

    private static string NormalizePublicText(string? value, int maximumLength)
    {
        var sanitized = ProcessPublicReceiptTextPolicy.Sanitize(value).Trim();
        if (sanitized.Length <= maximumLength)
        {
            return sanitized;
        }

        return string.Concat(
            sanitized.AsSpan(0, maximumLength - TruncationSuffix.Length).TrimEnd(),
            TruncationSuffix);
    }

    private static string? NormalizeRestrictedEvidenceReference(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return ProcessStrategyReceiptValuePolicy.IsRestrictedEvidenceReference(normalized)
            ? normalized
            : null;
    }

    private static string NormalizeOptionalDigest(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : NormalizeDigest(value);

    private static string NormalizeDigest(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        var payload = normalized.StartsWith(Sha256Prefix, StringComparison.OrdinalIgnoreCase)
            ? normalized[Sha256Prefix.Length..]
            : normalized;
        if (payload.Length != ProcessStrategyResultLimits.MaximumHashLength - Sha256Prefix.Length ||
            payload.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException(
                "Legacy process strategy result receipt contains an unsupported digest shape.");
        }

        return Sha256Prefix + payload.ToLowerInvariant();
    }
}
