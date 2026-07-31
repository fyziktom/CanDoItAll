using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static CanDoItAll.Modules.Processes.ProcessCompletionIssueResultFactory;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultConverter;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessExecutionMetadataBuilder;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactService;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactFormatter;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactOutcomeParser;
using static CanDoItAll.Modules.Processes.ProcessOutcomeGroundingValidator;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRuntimeOwnedStepCoordinator(
    IEnumerable<IProcessRuntimeOwnedStepExecutor> runtimeOwnedStepExecutors,
    ProcessStepCompletionCoordinator completionCoordinator,
    ProcessToolReceiptPolicyCatalog toolReceiptPolicies)
{
    private readonly IReadOnlyDictionary<string, IProcessRuntimeOwnedStepExecutor> runtimeOwnedStepExecutorsByKey = CreateExecutorMap(runtimeOwnedStepExecutors);

    internal async ValueTask<ProcessExecutionAdapterResult?> TryExecuteRuntimeOwnedStepAsync(
        ProcessRuntimeStepAssignment assignment,
        CancellationToken cancellationToken,
        ProcessStepExecutionContract? stepContract = null)
    {
        if (!ProcessRuntimeLaunchVariables.TryReadProcessStepRuntimeOwnedExecutorKey(
                assignment.LaunchVariables,
                out var executorKey))
        {
            return null;
        }

        if (!runtimeOwnedStepExecutorsByKey.TryGetValue(executorKey, out var executor))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                ComputeHash($"runtime-owned-executor-not-registered:{executorKey}"),
                new ProcessCompletionIssue(
                    "process.adapter.runtime_owned_executor_unavailable",
                    $"Step '{assignment.StepKey}' declares runtime-owned executor '{executorKey}', but no matching executor is registered.",
                    $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-executor-unavailable:{executorKey}",
                    assignment.ProducedArtifactSlotIds,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent));
        }

        var runtimeResult = await executor
            .TryExecuteAsync(assignment, cancellationToken)
            .ConfigureAwait(false);
        if (runtimeResult is null)
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                ComputeHash($"runtime-owned-executor-declined:{executorKey}"),
                new ProcessCompletionIssue(
                    "process.adapter.runtime_owned_executor_declined",
                    $"Step '{assignment.StepKey}' declares runtime-owned executor '{executorKey}', but that executor declined the typed execution contract.",
                    $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-executor-declined:{executorKey}",
                    assignment.ProducedArtifactSlotIds,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent));
        }

        return ResolveRuntimeOwnedStepResult(assignment, runtimeResult, stepContract);
    }

    private static IReadOnlyDictionary<string, IProcessRuntimeOwnedStepExecutor> CreateExecutorMap(
        IEnumerable<IProcessRuntimeOwnedStepExecutor> executors)
    {
        ArgumentNullException.ThrowIfNull(executors);

        var map = new Dictionary<string, IProcessRuntimeOwnedStepExecutor>(StringComparer.OrdinalIgnoreCase);
        foreach (var executor in executors)
        {
            if (string.IsNullOrWhiteSpace(executor.ExecutorKey))
            {
                throw new InvalidOperationException("A runtime-owned step executor must declare a stable executor key.");
            }

            if (!map.TryAdd(executor.ExecutorKey.Trim(), executor))
            {
                throw new InvalidOperationException($"Duplicate runtime-owned step executor key '{executor.ExecutorKey.Trim()}' is registered.");
            }
        }

        return map;
    }

    private ProcessExecutionAdapterResult ResolveRuntimeOwnedStepResult(
        ProcessRuntimeStepAssignment assignment,
        ProcessRuntimeOwnedStepExecutionResult runtimeResult,
        ProcessStepExecutionContract? stepContract)
    {
        if (!runtimeResult.Succeeded || runtimeResult.Output is null)
        {
            var failure = runtimeResult.Failure ?? ProcessRuntimeOwnedStepFailures.Unclassified;
            return NeedsManagerForCompletionIssue(
                assignment,
                ComputeHash(runtimeResult.Evidence),
                new ProcessCompletionIssue(
                    failure.Code.Value,
                    BuildFailureSummary(runtimeResult),
                    runtimeResult.Evidence,
                    assignment.ProducedArtifactSlotIds,
                    failure.RetrySafety,
                    failure.Idempotency));
        }

        var rawOutputHash = ComputeHash(runtimeResult.Evidence);
        if (!TryResolveEffectiveCompletionAssignment(
                assignment,
                runtimeResult,
                out var completionAssignment,
                out var completionContractIssue))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                new ProcessCompletionIssue(
                    "process.adapter.runtime_owned_completion_contract_invalid",
                    $"Runtime-owned executor for step '{assignment.StepKey}' returned an invalid effective completion scope: {completionContractIssue}",
                    $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-owned-completion-contract-invalid:{completionContractIssue}",
                    assignment.ProducedArtifactSlotIds,
                    ProcessDiagnosticRetrySafety.UnsafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent));
        }

        var materialization = completionCoordinator.Materialize(
            completionAssignment,
            runtimeResult.Output,
            runtimeResult.ExecutionRunId,
            runtimeResult.ToolReceipts,
            stepContract);
        if (materialization.Issue is { } materializationIssue)
        {
            return NeedsManagerForCompletionIssue(assignment, rawOutputHash, materializationIssue);
        }

        return completionCoordinator.Complete(
            completionAssignment,
            materialization,
            rawOutputHash,
            runtimeResult.ExecutionRunId,
            materialization.ToolReceipts,
            stepContract: stepContract);
    }

    private bool TryResolveEffectiveCompletionAssignment(
        ProcessRuntimeStepAssignment assignment,
        ProcessRuntimeOwnedStepExecutionResult runtimeResult,
        out ProcessRuntimeStepAssignment effectiveAssignment,
        out string issue)
    {
        effectiveAssignment = assignment;
        issue = string.Empty;
        var completionScope = runtimeResult.EffectiveCompletionScope;
        if (completionScope is null)
        {
            return true;
        }

        if (completionScope != ProcessRuntimeOwnedCompletionScope.ReadOnlyProductVerification)
        {
            issue = $"completion scope '{completionScope}' is not supported";
            return false;
        }

        var declaredOperations = NormalizeOperations(assignment.AllowedOperations);
        if (!declaredOperations.Contains(
                ProcessOperationContractNames.MutateProductTarget,
                StringComparer.OrdinalIgnoreCase))
        {
            issue = "read-only product verification requires MutateProductTarget in the persisted assignment";
            return false;
        }

        if (!string.Equals(
                assignment.OperationTargetScope,
                ProcessOperationContractNames.ExternalProductTargetMutable,
                StringComparison.OrdinalIgnoreCase))
        {
            issue =
                $"read-only product verification cannot narrow target scope '{assignment.OperationTargetScope}'";
            return false;
        }

        if (assignment.ProducedArtifactSlotIds.Count > 0 &&
            !declaredOperations.Contains(
                ProcessOperationContractNames.WriteManagedProcessArtifacts,
                StringComparer.OrdinalIgnoreCase))
        {
            issue =
                "the persisted assignment must include WriteManagedProcessArtifacts while produced artifact slots exist";
            return false;
        }

        if (runtimeResult.ToolReceipts.Any(toolReceiptPolicies.IsProductMutationReceipt))
        {
            issue = "read-only product verification cannot contain a product-mutation receipt";
            return false;
        }

        var effectiveOperations = declaredOperations
            .Where(operation => !string.Equals(
                operation,
                ProcessOperationContractNames.MutateProductTarget,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        effectiveAssignment = assignment with
        {
            AllowedOperations = effectiveOperations,
            OperationTargetScope = ProcessOperationContractNames.ExternalProductTargetReadOnly
        };
        return true;
    }

    private static string BuildFailureSummary(ProcessRuntimeOwnedStepExecutionResult result)
    {
        const int maximumSummaryLength = 2000;
        const int maximumReceiptCount = 12;

        var receiptOutcomes = result.ToolReceipts
            .Take(maximumReceiptCount)
            .Select(receipt => $"{receipt.ToolName}={ClassifyReceiptOutcome(receipt.ExitSummary)}")
            .ToArray();
        var receiptSummary = receiptOutcomes.Length == 0
            ? "no tool receipts"
            : string.Join(", ", receiptOutcomes);
        if (result.ToolReceipts.Count > maximumReceiptCount)
        {
            receiptSummary += $", and {result.ToolReceipts.Count - maximumReceiptCount} more";
        }

        var executionContext =
            $"Runtime execution correlation: {result.ExecutionRunId:D}. Receipt outcomes: {receiptSummary}.";
        var summaryPrefix = $"{executionContext} Driver summary: ";
        if (summaryPrefix.Length >= maximumSummaryLength)
        {
            return summaryPrefix[..maximumSummaryLength];
        }

        var maximumDriverSummaryLength = maximumSummaryLength - summaryPrefix.Length;
        var driverSummary = result.Summary.Length <= maximumDriverSummaryLength
            ? result.Summary
            : result.Summary[..maximumDriverSummaryLength];
        return summaryPrefix + driverSummary;
    }

    private static string ClassifyReceiptOutcome(string exitSummary)
    {
        if (exitSummary.StartsWith("Succeeded", StringComparison.OrdinalIgnoreCase))
        {
            return "Succeeded";
        }

        if (exitSummary.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
        {
            return "Failed";
        }

        return "Observed";
    }
}
