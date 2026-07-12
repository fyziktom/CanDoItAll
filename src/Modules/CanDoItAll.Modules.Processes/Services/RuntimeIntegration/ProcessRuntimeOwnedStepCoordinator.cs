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
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactService;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactFormatter;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactOutcomeParser;
using static CanDoItAll.Modules.Processes.ProcessOutcomeGroundingValidator;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRuntimeOwnedStepCoordinator(
    IEnumerable<IProcessRuntimeOwnedStepExecutor> runtimeOwnedStepExecutors,
    ProcessStepCompletionCoordinator completionCoordinator)
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
            return NeedsManagerForCompletionIssue(
                assignment,
                ComputeHash(runtimeResult.Evidence),
                new ProcessCompletionIssue(
                    "process.adapter.runtime_owned_step_failed",
                    runtimeResult.Summary,
                    runtimeResult.Evidence,
                    assignment.ProducedArtifactSlotIds,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent));
        }

        var rawOutputHash = ComputeHash(runtimeResult.Evidence);
        var materialization = completionCoordinator.Materialize(
            assignment,
            runtimeResult.Output,
            runtimeResult.ExecutionRunId,
            runtimeResult.ToolReceipts);
        if (materialization.Issue is { } materializationIssue)
        {
            return NeedsManagerForCompletionIssue(assignment, rawOutputHash, materializationIssue);
        }

        return completionCoordinator.Complete(
            assignment,
            materialization,
            rawOutputHash,
            runtimeResult.ExecutionRunId,
            materialization.ToolReceipts,
            stepContract: stepContract);
    }
}
