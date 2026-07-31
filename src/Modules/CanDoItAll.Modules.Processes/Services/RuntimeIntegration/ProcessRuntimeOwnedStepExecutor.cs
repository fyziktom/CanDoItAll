using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessRuntimeOwnedStepExecutor
{
    string ExecutorKey { get; }

    ValueTask<ProcessRuntimeOwnedStepExecutionResult?> TryExecuteAsync(
        ProcessRuntimeStepAssignment assignment,
        CancellationToken cancellationToken = default);
}

internal sealed record ProcessRuntimeOwnedStepExecutionResult(
    bool Succeeded,
    ProcessStepOutcomeResult? Output,
    IReadOnlyList<ToolExecutionReceiptRecord> ToolReceipts,
    Guid ExecutionRunId,
    string Summary,
    string Evidence,
    ProcessRuntimeOwnedStepFailure? Failure = null,
    ProcessRuntimeOwnedCompletionScope? EffectiveCompletionScope = null);

internal enum ProcessRuntimeOwnedCompletionScope
{
    ReadOnlyProductVerification
}

internal sealed record ProcessRuntimeOwnedStepFailure(
    StrategyDiagnosticCode Code,
    ProcessDiagnosticRetrySafety RetrySafety,
    ProcessDiagnosticIdempotencyClassification Idempotency);

internal static class ProcessRuntimeOwnedStepFailures
{
    internal static ProcessRuntimeOwnedStepFailure ContractInvalid { get; } = new(
        new StrategyDiagnosticCode("process.adapter.runtime_owned_contract_invalid"),
        ProcessDiagnosticRetrySafety.UnsafeToRetry,
        ProcessDiagnosticIdempotencyClassification.Unknown);

    internal static ProcessRuntimeOwnedStepFailure ExecutionFailed { get; } = new(
        new StrategyDiagnosticCode("process.adapter.runtime_owned_execution_failed"),
        ProcessDiagnosticRetrySafety.UnsafeToRetry,
        ProcessDiagnosticIdempotencyClassification.Unknown);

    internal static ProcessRuntimeOwnedStepFailure ExecutionDenied { get; } = new(
        new StrategyDiagnosticCode("process.adapter.runtime_owned_execution_denied"),
        ProcessDiagnosticRetrySafety.UnsafeToRetry,
        ProcessDiagnosticIdempotencyClassification.Unknown);

    internal static ProcessRuntimeOwnedStepFailure ExecutionTimedOut { get; } = new(
        new StrategyDiagnosticCode("process.adapter.runtime_owned_execution_timed_out"),
        ProcessDiagnosticRetrySafety.UnsafeToRetry,
        ProcessDiagnosticIdempotencyClassification.Unknown);

    internal static ProcessRuntimeOwnedStepFailure ReadbackPathMissing { get; } = new(
        new StrategyDiagnosticCode("process.adapter.runtime_owned_readback_path_missing"),
        ProcessDiagnosticRetrySafety.UnsafeToRetry,
        ProcessDiagnosticIdempotencyClassification.Unknown);

    internal static ProcessRuntimeOwnedStepFailure ReadbackContentMissing { get; } = new(
        new StrategyDiagnosticCode("process.adapter.runtime_owned_readback_content_missing"),
        ProcessDiagnosticRetrySafety.UnsafeToRetry,
        ProcessDiagnosticIdempotencyClassification.Unknown);

    internal static ProcessRuntimeOwnedStepFailure ReadbackUnavailable { get; } = new(
        new StrategyDiagnosticCode("process.adapter.runtime_owned_readback_unavailable"),
        ProcessDiagnosticRetrySafety.UnsafeToRetry,
        ProcessDiagnosticIdempotencyClassification.Unknown);

    internal static ProcessRuntimeOwnedStepFailure VerificationFailed { get; } = new(
        new StrategyDiagnosticCode("process.adapter.runtime_owned_verification_failed"),
        ProcessDiagnosticRetrySafety.UnsafeToRetry,
        ProcessDiagnosticIdempotencyClassification.Unknown);

    internal static ProcessRuntimeOwnedStepFailure Unclassified { get; } = new(
        new StrategyDiagnosticCode("process.adapter.runtime_owned_step_failed"),
        ProcessDiagnosticRetrySafety.UnsafeToRetry,
        ProcessDiagnosticIdempotencyClassification.Unknown);

    internal static ProcessRuntimeOwnedStepFailure ApplyDeclaredIdempotency(
        ProcessRuntimeOwnedStepFailure failure,
        ProcessToolOperationIdempotencyPolicy idempotency)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return idempotency == ProcessToolOperationIdempotencyPolicy.CurrentRunRepeatable
            ? failure with
            {
                RetrySafety = ProcessDiagnosticRetrySafety.SafeToRetry,
                Idempotency = ProcessDiagnosticIdempotencyClassification.Idempotent
            }
            : failure;
    }

    internal static ProcessRuntimeOwnedStepFailure ResolveExecutionFailure(
        string? outcome,
        ProcessToolOperationIdempotencyPolicy idempotency)
    {
        if (string.Equals(outcome, "Denied", StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionDenied;
        }

        if (string.Equals(outcome, "TimedOut", StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionTimedOut;
        }

        return string.Equals(outcome, "Failed", StringComparison.OrdinalIgnoreCase)
            ? ApplyDeclaredIdempotency(ExecutionFailed, idempotency)
            : ExecutionFailed;
    }
}
