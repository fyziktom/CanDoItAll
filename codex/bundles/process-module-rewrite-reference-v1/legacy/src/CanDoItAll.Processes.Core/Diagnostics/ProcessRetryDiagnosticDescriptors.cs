namespace CanDoItAll.Processes.Core.Diagnostics;

public enum ProcessRetryDiagnosticFailureKind
{
    None = 0,
    MissingRequiredTool = 1,
    CriticalToolFailure = 2,
    BuildFailure = 3,
    TestFailure = 4,
    ProviderFailure = 5,
    ExecutionInterruption = 6,
    FinalizerValidationFailure = 7,
    NoProgress = 8,
    Unknown = 9
}

public sealed record ProcessRetryDiagnosticDescriptor(
    bool ShouldRetry,
    int AttemptNumber,
    int MaxExecutionAttempts,
    IReadOnlyList<string> RetryReasons,
    string RetryReasonSummary,
    IReadOnlyList<string> MissingRequiredTools,
    IReadOnlyList<string> FailedToolNames,
    int UnresolvedCriticalToolFailureCount,
    bool HasMissingRequiredTools,
    bool HasUnresolvedCriticalToolFailures,
    bool HasBuildFailure,
    bool HasTestFailure,
    bool HasRecoverableProviderFailure,
    bool HasRecoverableExecutionInterruption,
    bool HasRecoverableFinalizerFailure,
    ProcessRetryDiagnosticFailureKind PrimaryFailureKind);

public sealed record ProcessNoProgressRetryDiagnosticDescriptor(
    bool HasSignal,
    string Fingerprint,
    Guid? ExecutionRunId,
    string ToolSignature,
    string ArtifactValidationFingerprint,
    string MutationDelta,
    string ProofDelta);

public sealed record ProcessProviderRepairDiagnosticDescriptor(
    bool HasRecoverableProviderFailure,
    bool HasRepairOutcome,
    string FailureSummary,
    string FailedProviderName,
    string FallbackProviderName,
    string FallbackModel,
    int AffectedAgentCount);

public static class ProcessRetryDiagnosticDescriptorRules
{
    public static ProcessRetryDiagnosticDescriptor DescribeRetry(
        bool shouldRetry,
        int attemptNumber,
        int maxExecutionAttempts,
        IEnumerable<string> retryReasons,
        IEnumerable<string> missingRequiredTools,
        IEnumerable<string> failedToolNames,
        int unresolvedCriticalToolFailureCount,
        bool hasBuildFailure,
        bool hasTestFailure,
        bool hasRecoverableProviderFailure,
        bool hasRecoverableExecutionInterruption,
        bool hasRecoverableFinalizerFailure,
        bool hasNoProgressSignal)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attemptNumber);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExecutionAttempts);
        ArgumentOutOfRangeException.ThrowIfNegative(unresolvedCriticalToolFailureCount);
        ArgumentNullException.ThrowIfNull(retryReasons);
        ArgumentNullException.ThrowIfNull(missingRequiredTools);
        ArgumentNullException.ThrowIfNull(failedToolNames);

        var normalizedRetryReasons = NormalizeReasons(retryReasons);
        var normalizedMissingRequiredTools = NormalizeToolNames(missingRequiredTools);
        var normalizedFailedToolNames = NormalizeToolNames(failedToolNames);
        var hasMissingRequiredTools = normalizedMissingRequiredTools.Count > 0;
        var hasUnresolvedCriticalToolFailures = unresolvedCriticalToolFailureCount > 0;

        return new ProcessRetryDiagnosticDescriptor(
            shouldRetry,
            attemptNumber,
            maxExecutionAttempts,
            normalizedRetryReasons,
            normalizedRetryReasons.Count == 0
                ? "unspecified recoverable failure"
                : string.Join(" | ", normalizedRetryReasons),
            normalizedMissingRequiredTools,
            normalizedFailedToolNames,
            unresolvedCriticalToolFailureCount,
            hasMissingRequiredTools,
            hasUnresolvedCriticalToolFailures,
            hasBuildFailure,
            hasTestFailure,
            hasRecoverableProviderFailure,
            hasRecoverableExecutionInterruption,
            hasRecoverableFinalizerFailure,
            ResolvePrimaryFailureKind(
                hasRecoverableProviderFailure,
                hasRecoverableExecutionInterruption,
                hasRecoverableFinalizerFailure,
                hasTestFailure,
                hasBuildFailure,
                hasMissingRequiredTools,
                hasUnresolvedCriticalToolFailures,
                hasNoProgressSignal,
                normalizedRetryReasons.Count > 0));
    }

    public static ProcessNoProgressRetryDiagnosticDescriptor DescribeNoProgressSignal(
        string fingerprint,
        Guid executionRunId,
        string toolSignature,
        string artifactValidationFingerprint,
        string mutationDelta,
        string proofDelta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolSignature);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactValidationFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(mutationDelta);
        ArgumentException.ThrowIfNullOrWhiteSpace(proofDelta);

        return new ProcessNoProgressRetryDiagnosticDescriptor(
            HasSignal: true,
            fingerprint.Trim(),
            executionRunId,
            toolSignature.Trim(),
            artifactValidationFingerprint.Trim(),
            mutationDelta.Trim(),
            proofDelta.Trim());
    }

    public static ProcessNoProgressRetryDiagnosticDescriptor DescribeNoProgressSignalAbsent()
    {
        return new ProcessNoProgressRetryDiagnosticDescriptor(
            HasSignal: false,
            Fingerprint: string.Empty,
            ExecutionRunId: null,
            ToolSignature: string.Empty,
            ArtifactValidationFingerprint: string.Empty,
            MutationDelta: string.Empty,
            ProofDelta: string.Empty);
    }

    public static ProcessProviderRepairDiagnosticDescriptor DescribeProviderRepair(
        bool hasRecoverableProviderFailure,
        string failureSummary,
        string failedProviderName,
        string fallbackProviderName,
        string fallbackModel,
        int affectedAgentCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(affectedAgentCount);

        return new ProcessProviderRepairDiagnosticDescriptor(
            hasRecoverableProviderFailure,
            affectedAgentCount > 0 && !string.IsNullOrWhiteSpace(fallbackProviderName),
            NormalizeText(failureSummary),
            NormalizeText(failedProviderName),
            NormalizeText(fallbackProviderName),
            NormalizeText(fallbackModel),
            affectedAgentCount);
    }

    private static ProcessRetryDiagnosticFailureKind ResolvePrimaryFailureKind(
        bool hasRecoverableProviderFailure,
        bool hasRecoverableExecutionInterruption,
        bool hasRecoverableFinalizerFailure,
        bool hasTestFailure,
        bool hasBuildFailure,
        bool hasMissingRequiredTools,
        bool hasUnresolvedCriticalToolFailures,
        bool hasNoProgressSignal,
        bool hasRetryReasons)
    {
        if (hasRecoverableProviderFailure)
        {
            return ProcessRetryDiagnosticFailureKind.ProviderFailure;
        }

        if (hasRecoverableExecutionInterruption)
        {
            return ProcessRetryDiagnosticFailureKind.ExecutionInterruption;
        }

        if (hasRecoverableFinalizerFailure)
        {
            return ProcessRetryDiagnosticFailureKind.FinalizerValidationFailure;
        }

        if (hasTestFailure)
        {
            return ProcessRetryDiagnosticFailureKind.TestFailure;
        }

        if (hasBuildFailure)
        {
            return ProcessRetryDiagnosticFailureKind.BuildFailure;
        }

        if (hasMissingRequiredTools)
        {
            return ProcessRetryDiagnosticFailureKind.MissingRequiredTool;
        }

        if (hasUnresolvedCriticalToolFailures)
        {
            return ProcessRetryDiagnosticFailureKind.CriticalToolFailure;
        }

        if (hasNoProgressSignal)
        {
            return ProcessRetryDiagnosticFailureKind.NoProgress;
        }

        return hasRetryReasons
            ? ProcessRetryDiagnosticFailureKind.Unknown
            : ProcessRetryDiagnosticFailureKind.None;
    }

    private static IReadOnlyList<string> NormalizeReasons(IEnumerable<string> values)
    {
        return values
            .Select(NormalizeText)
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeToolNames(IEnumerable<string> values)
    {
        return values
            .Select(NormalizeText)
            .Where(static value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}
