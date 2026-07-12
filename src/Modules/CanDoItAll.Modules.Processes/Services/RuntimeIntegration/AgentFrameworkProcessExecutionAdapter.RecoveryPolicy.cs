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
    private static bool TryBuildRetryableAgentOutputContractIssue(
        ProcessRuntimeStepAssignment assignment,
        Exception exception,
        out ProcessCompletionIssue issue)
    {
        issue = null!;
        if (!LooksLikeAgentOutputContractFailure(exception))
        {
            return false;
        }

        var expectedRefs = assignment.ProducedArtifactSlotIds.Count > 0
            ? assignment.ProducedArtifactSlotIds
                .SelectMany(slotId => EnumerateManagedArtifactEvidenceRefs(assignment, slotId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [BuildManagedStepArtifactPath(assignment)];
        var expectedRefSummary = expectedRefs.Length == 0
            ? "the concrete current-run evidence ref required by the process step brief"
            : string.Join("; ", expectedRefs);
        issue = new ProcessCompletionIssue(
            "process.adapter.agent_output_contract_retryable",
            $"Agent execution for step '{assignment.StepKey}' did not produce a valid process-step finalizer outcome. Retry the step, create the required current-run evidence first, and only return Completed after submit_process_step_outcome evidenceRefs contains one of: {expectedRefSummary}. Runtime detail: {exception.Message}",
            $"{assignment.RunId}:{assignment.StepInstanceId}:agent-output-contract:{exception.GetType().FullName}:{exception.Message}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
        return true;
    }

    private static bool TryBuildRetryableAgentTransientExecutionIssue(
        ProcessRuntimeStepAssignment assignment,
        ExecutionRunResult result,
        out ProcessCompletionIssue issue)
    {
        issue = null!;
        if (result.Metric.Outcome != RunOutcome.Failed ||
            !LooksLikeTransientAgentExecutionFailure(result.ResponseText))
        {
            return false;
        }

        issue = CreateRetryableAgentTransientExecutionIssue(
            assignment,
            $"executionRunId={result.ExecutionRunId:D}; outcome={result.Metric.Outcome}; provider={result.Metric.ProviderName}; model={result.Metric.Model}; detail={result.ResponseText}");
        return true;
    }

    private static bool TryBuildRetryableAgentTransientExecutionIssue(
        ProcessRuntimeStepAssignment assignment,
        Exception exception,
        out ProcessCompletionIssue issue)
    {
        issue = null!;
        var text = exception.ToString();
        if (!LooksLikeTransientAgentExecutionFailure(text))
        {
            return false;
        }

        issue = CreateRetryableAgentTransientExecutionIssue(
            assignment,
            $"{exception.GetType().Name}: {exception.Message}");
        return true;
    }

    private static ProcessCompletionIssue CreateRetryableAgentTransientExecutionIssue(
        ProcessRuntimeStepAssignment assignment,
        string detail)
    {
        IReadOnlyList<ArtifactSlotId> requestedArtifactSlotIds = assignment.ProducedArtifactSlotIds.Count > 0
            ? assignment.ProducedArtifactSlotIds
            : assignment.RequiredArtifactSlotIds;
        var safeDetail = LimitDiagnosticText(detail);
        return new ProcessCompletionIssue(
            "process.adapter.agent_transient_execution_retry",
            $"Agent execution for step '{assignment.StepKey}' failed with a transient provider/runtime error. Retry the same step without relaunching completed child work; preserve any existing managed artifacts and return a normal process-step outcome after the retry. Runtime detail: {safeDetail}",
            $"{assignment.RunId}:{assignment.StepInstanceId}:agent-transient-execution:{ComputeHash(safeDetail)}",
            requestedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

}
