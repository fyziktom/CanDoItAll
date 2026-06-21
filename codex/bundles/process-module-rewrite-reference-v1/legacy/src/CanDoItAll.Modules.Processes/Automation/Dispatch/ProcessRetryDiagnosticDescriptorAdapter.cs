using CoreNoProgressRetryDiagnosticDescriptor = global::CanDoItAll.Processes.Core.Diagnostics.ProcessNoProgressRetryDiagnosticDescriptor;
using CoreProviderRepairDiagnosticDescriptor = global::CanDoItAll.Processes.Core.Diagnostics.ProcessProviderRepairDiagnosticDescriptor;
using CoreRetryDiagnosticDescriptor = global::CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor;
using CoreRetryDiagnosticDescriptorRules = global::CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptorRules;

namespace CanDoItAll.Modules.Processes;

using NoProgressRetrySignal = ProcessRunAutomationDispatchService.NoProgressRetrySignal;
using ProviderRepairOutcome = ProcessRunAutomationDispatchService.ProviderRepairOutcome;

internal static class ProcessRetryDiagnosticDescriptorAdapter
{
    public static CoreRetryDiagnosticDescriptor DescribeRetry(
        ProcessRecoveryRetryFacts retryFacts,
        IReadOnlyList<string> retryReasons,
        bool shouldRetry,
        int attemptNumber,
        int maxExecutionAttempts,
        bool hasRecoverableProviderFailure,
        bool hasRecoverableExecutionInterruption,
        bool hasRecoverableFinalizerFailure,
        NoProgressRetrySignal? noProgressSignal)
    {
        ArgumentNullException.ThrowIfNull(retryFacts);
        ArgumentNullException.ThrowIfNull(retryReasons);

        return CoreRetryDiagnosticDescriptorRules.DescribeRetry(
            shouldRetry,
            attemptNumber,
            maxExecutionAttempts,
            retryReasons,
            retryFacts.MissingRequiredTools,
            retryFacts.FailedToolNames,
            retryFacts.UnresolvedCriticalToolFailures.Count,
            retryFacts.HasBuildFailure,
            retryFacts.HasTestFailure,
            hasRecoverableProviderFailure,
            hasRecoverableExecutionInterruption,
            hasRecoverableFinalizerFailure,
            noProgressSignal is not null);
    }

    public static CoreNoProgressRetryDiagnosticDescriptor DescribeNoProgressSignal(
        NoProgressRetrySignal? signal)
    {
        return signal is null
            ? CoreRetryDiagnosticDescriptorRules.DescribeNoProgressSignalAbsent()
            : CoreRetryDiagnosticDescriptorRules.DescribeNoProgressSignal(
                signal.Fingerprint,
                signal.ExecutionRunId,
                signal.ToolSignature,
                signal.ArtifactValidationFingerprint,
                signal.MutationDelta,
                signal.ProofDelta);
    }

    public static CoreProviderRepairDiagnosticDescriptor DescribeProviderRepair(
        ProviderRepairOutcome? providerRepair)
    {
        return providerRepair is null
            ? CoreRetryDiagnosticDescriptorRules.DescribeProviderRepair(
                hasRecoverableProviderFailure: false,
                failureSummary: string.Empty,
                failedProviderName: string.Empty,
                fallbackProviderName: string.Empty,
                fallbackModel: string.Empty,
                affectedAgentCount: 0)
            : CoreRetryDiagnosticDescriptorRules.DescribeProviderRepair(
                hasRecoverableProviderFailure: true,
                providerRepair.FailureSummary,
                providerRepair.FailedProviderName,
                providerRepair.FallbackProviderName,
                providerRepair.FallbackModel,
                providerRepair.AffectedAgentCount);
    }
}
