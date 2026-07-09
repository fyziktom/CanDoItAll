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

namespace CanDoItAll.Modules.Processes;

internal sealed partial class AgentFrameworkProcessExecutionAdapter
{
    private async ValueTask<ProcessExecutionAdapterResult?> TryExecuteRuntimeOwnedStepAsync(
        ProcessRuntimeStepAssignment assignment,
        CancellationToken cancellationToken)
    {
        foreach (var executor in runtimeOwnedStepExecutors)
        {
            var runtimeResult = await executor
                .TryExecuteAsync(assignment, cancellationToken)
                .ConfigureAwait(false);
            if (runtimeResult is null)
            {
                continue;
            }

            return ResolveRuntimeOwnedStepResult(assignment, runtimeResult);
        }

        return null;
    }

    private ProcessExecutionAdapterResult ResolveRuntimeOwnedStepResult(
        ProcessRuntimeStepAssignment assignment,
        ProcessRuntimeOwnedStepExecutionResult runtimeResult)
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
        var materialization = MaterializeManagedOutcomeArtifactIfNeeded(
            assignment,
            runtimeResult.Output,
            runtimeResult.ExecutionRunId,
            runtimeResult.ToolReceipts);
        if (materialization.Issue is { } materializationIssue)
        {
            return NeedsManagerForCompletionIssue(assignment, rawOutputHash, materializationIssue);
        }

        if (ValidateManagedArtifactBodyReferences(
                assignment,
                materialization.Output,
                materialization.ToolReceipts) is { } ungroundedArtifactReferenceIssue)
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                ungroundedArtifactReferenceIssue);
        }

        var completionGateEvaluation = CompletionGateEvaluator.Evaluate(new ProcessCompletionGateContext(
            assignment,
            materialization.Output,
            materialization.ToolReceipts,
            runtimeResult.ExecutionRunId));
        if (!completionGateEvaluation.IsSatisfied)
        {
            return NeedsManagerForCompletionIssues(
                assignment,
                rawOutputHash,
                completionGateEvaluation);
        }

        if (AcceptManagedOutcomeArtifactIfNeeded(
                assignment,
                materialization,
                runtimeResult.ExecutionRunId,
                materialization.ToolReceipts,
                out var acceptedCompletionToolReceipts) is { } acceptanceIssue)
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                acceptanceIssue);
        }

        var producedArtifactContentHashes = BuildProducedArtifactContentHashes(
            assignment,
            materialization.Output,
            out var producedArtifactReadbackIssue);
        if (producedArtifactReadbackIssue is not null)
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                producedArtifactReadbackIssue);
        }

        return ToAdapterResult(
            assignment,
            materialization.Output,
            rawOutputHash,
            acceptedCompletionToolReceipts,
            runtimeResult.ExecutionRunId,
            producedArtifactContentHashes);
    }
}
