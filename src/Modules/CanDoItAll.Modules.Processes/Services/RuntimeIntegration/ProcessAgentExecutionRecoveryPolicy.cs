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

using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactService;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactFormatter;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactOutcomeParser;
using static CanDoItAll.Modules.Processes.ProcessOutcomeGroundingValidator;
using static CanDoItAll.Modules.Processes.ProcessRuntimeFailureClassifier;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessAgentExecutionRecoveryPolicy
{
    internal static bool TryBuildRetryableAgentOutputContractIssue(
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
            $"Agent execution for step '{assignment.StepKey}' did not produce a valid process-step finalizer outcome. Retry the step, create the required current-run evidence first, and only return Completed after submit_process_step_outcome evidenceRefs contains one of: {expectedRefSummary}. Review the restricted execution log using the recorded evidence hash if more detail is required.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:agent-output-contract:{ComputeHash(exception.GetType().FullName + ":" + exception.Message)}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
        return true;
    }

    internal static bool TryBuildRetryableAgentTransientExecutionIssue(
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

    internal static bool TryBuildRetryableAgentTransientExecutionIssue(
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

    internal static async ValueTask<ProcessCompletionIssue> AttestRetryableAgentTransientExecutionIssueAsync(
        ProcessRuntimeStepAssignment assignment,
        IAgentFrameworkWorkspaceService workspaceService,
        Guid executionRunId,
        AgentRunMetric? immediateMetric,
        string detail,
        CancellationToken cancellationToken)
    {
        var legacyIssue = CreateRetryableAgentTransientExecutionIssue(assignment, detail);
        try
        {
            var executionDetail = await workspaceService
                .GetExecutionRunDetailAsync(executionRunId, cancellationToken)
                .ConfigureAwait(false);
            if (executionDetail is null)
            {
                return AppendAttestationStatus(
                    legacyIssue,
                    $"Durable execution detail for run '{executionRunId:D}' was missing, so absence of side effects could not be proven.");
            }

            var evidenceSummary = BuildDurableEvidenceSummary(executionRunId, immediateMetric, executionDetail);
            if (!HasExactNoSideEffectAttestation(executionRunId, assignment, immediateMetric, executionDetail))
            {
                return AppendAttestationStatus(
                    legacyIssue,
                    $"Durable execution detail did not prove that run '{executionRunId:D}' failed before recorded side effects. {evidenceSummary}");
            }

            return CreateAttestedAgentTransientExecutionIssue(
                assignment,
                executionRunId,
                detail,
                evidenceSummary,
                ComputeDurableEvidenceDigest(executionRunId, immediateMetric, executionDetail));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return AppendAttestationStatus(
                legacyIssue,
                $"Durable execution detail for run '{executionRunId:D}' was unreadable, so absence of side effects could not be proven.",
                ComputeHash(exception.GetType().FullName + ":" + exception.Message));
        }
    }

    internal static ProcessCompletionIssue CreateRetryableAgentTransientExecutionIssue(
        ProcessRuntimeStepAssignment assignment,
        string detail)
    {
        return new ProcessCompletionIssue(
            ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionRetry,
            $"Agent execution for step '{assignment.StepKey}' failed with a transient provider/runtime error. Retry the same step without relaunching completed child work; preserve any existing managed artifacts and return a normal process-step outcome after the retry. Provider/runtime detail is available only through restricted execution logs; the persisted receipt contains its evidence hash.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:agent-transient-execution:{ComputeHash(detail)}",
            ResolveRequestedArtifactSlotIds(assignment),
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static ProcessCompletionIssue CreateAttestedAgentTransientExecutionIssue(
        ProcessRuntimeStepAssignment assignment,
        Guid executionRunId,
        string detail,
        string evidenceSummary,
        string durableEvidenceDigest)
    {
        var safeEvidenceSummary = LimitDiagnosticText(evidenceSummary, 800);
        var attestation =
            ProcessExecutionSafetyAttestation.FailedBeforeRecordedSideEffects(
                new ProcessExecutionRunId(executionRunId),
                assignment.RunId,
                assignment.StepInstanceId,
                new ProcessExecutionExecutorId(Guid.Parse(assignment.ExecutorId)),
                durableEvidenceDigest);
        return new ProcessCompletionIssue(
            ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionBeforeSideEffects,
            $"Agent execution for step '{assignment.StepKey}' failed with a transient provider/runtime error, and durable execution detail proves that run '{executionRunId:D}' failed before recorded side effects. Retry the same step without relaunching completed child work. Attestation: {safeEvidenceSummary}. Provider/runtime detail is available only through restricted execution logs.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:agent-transient-execution-before-side-effects:{attestation.EvidenceHash}:{ComputeHash(detail)}",
            ResolveRequestedArtifactSlotIds(assignment),
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent)
        {
            ExecutionSafetyAttestation = attestation
        };
    }

    private static ProcessCompletionIssue AppendAttestationStatus(
        ProcessCompletionIssue issue,
        string attestationStatus,
        string? restrictedDetailHash = null)
    {
        return issue with
        {
            Summary = $"{issue.Summary} Attestation status: {LimitDiagnosticText(attestationStatus, 800)}",
            Evidence = string.IsNullOrWhiteSpace(restrictedDetailHash)
                ? issue.Evidence
                : $"{issue.Evidence}:attestation-detail:{restrictedDetailHash}"
        };
    }

    private static IReadOnlyList<ArtifactSlotId> ResolveRequestedArtifactSlotIds(
        ProcessRuntimeStepAssignment assignment)
        => assignment.ProducedArtifactSlotIds.Count > 0
            ? assignment.ProducedArtifactSlotIds
            : assignment.RequiredArtifactSlotIds;

    private static bool HasExactNoSideEffectAttestation(
        Guid executionRunId,
        ProcessRuntimeStepAssignment assignment,
        AgentRunMetric? immediateMetric,
        ExecutionRunDetail detail)
    {
        if (!Guid.TryParse(assignment.ExecutorId, out var assignedAgentId) ||
            detail.Run is null ||
            detail.Run.Id != executionRunId ||
            detail.Run.AgentId != assignedAgentId ||
            !MatchesExecutionIdentity(detail.Run, assignment) ||
            detail.Run.State != ExecutionState.Failed ||
            detail.Run.Outcome != RunOutcome.Failed ||
            !detail.Run.CompletedAtUtc.HasValue ||
            detail.Run.ChatSessionId.HasValue ||
            detail.ChatSession is not null ||
            !string.IsNullOrWhiteSpace(detail.Run.RuntimeSessionKey) ||
            !string.IsNullOrWhiteSpace(detail.Run.SerializedSessionStateJson) ||
            detail.Run.PendingApprovals is null ||
            detail.Run.PendingApprovals.Count != 0 ||
            !string.IsNullOrWhiteSpace(detail.Run.StructuredOutputRawOutput) ||
            !string.IsNullOrWhiteSpace(detail.Run.StructuredOutputValidationStatus) ||
            HasStructuredOutputValidationErrors(detail.Run.StructuredOutputValidationErrorsJson))
        {
            return false;
        }

        if (immediateMetric is not null &&
            (immediateMetric.ExecutionRunId != executionRunId ||
             immediateMetric.AgentId != assignedAgentId ||
             immediateMetric.ChatSessionId.HasValue ||
             immediateMetric.Outcome != RunOutcome.Failed ||
             immediateMetric.ToolCalls != 0))
        {
            return false;
        }

        if (detail.Metrics is null ||
            detail.Metrics.Count == 0 ||
            detail.Metrics.Any(metric =>
                metric.ExecutionRunId != executionRunId ||
                metric.AgentId != assignedAgentId ||
                metric.ChatSessionId.HasValue ||
                metric.Outcome != RunOutcome.Failed ||
                metric.ToolCalls != 0))
        {
            return false;
        }

        if (detail.UsageObservations is null ||
            detail.UsageObservations.Any(observation =>
                observation.ExecutionRunId != executionRunId ||
                observation.AgentId != assignedAgentId ||
                observation.ChatSessionId.HasValue ||
                observation.ToolCallCount != 0 ||
                !string.IsNullOrWhiteSpace(observation.RuntimeSessionKey)))
        {
            return false;
        }

        return detail.ToolReceipts is { Count: 0 } &&
               detail.Artifacts is { Count: 0 } &&
               detail.Checkpoints is { Count: 0 } &&
               detail.Approvals is { Count: 0 } &&
               HasCanonicalFailedExecutionLog(
                   executionRunId,
                   assignedAgentId,
                   detail.Run.CompletedAtUtc.Value,
                   detail.ExecutionLog);
    }

    private static bool HasCanonicalFailedExecutionLog(
        Guid executionRunId,
        Guid assignedAgentId,
        DateTimeOffset completedAtUtc,
        IReadOnlyList<ExecutionLogEntry> executionLog)
    {
        if (executionLog.Count == 0 ||
            executionLog.Select(entry => entry.Id).Distinct().Count() != executionLog.Count)
        {
            return false;
        }

        var canonicalTimeline = executionLog
            .OrderBy(entry => entry.CreatedAtUtc)
            .ThenBy(entry => entry.Id)
            .ToArray();
        var terminalEntry = canonicalTimeline[^1];
        return terminalEntry.State == ExecutionState.Failed &&
               terminalEntry.CreatedAtUtc <= completedAtUtc &&
               canonicalTimeline
                   .Take(canonicalTimeline.Length - 1)
                   .All(entry => entry.State is not ExecutionState.Failed and not ExecutionState.Completed) &&
               canonicalTimeline.All(entry =>
                   entry.ExecutionRunId == executionRunId &&
                   entry.AgentId == assignedAgentId &&
                   !entry.ChatSessionId.HasValue &&
                   entry.CreatedAtUtc <= completedAtUtc &&
                   entry.State is not ExecutionState.WaitingOnTool and not ExecutionState.Completed);
    }

    private static bool MatchesExecutionIdentity(
        ExecutionRunRecord run,
        ProcessRuntimeStepAssignment assignment)
    {
        return string.Equals(run.SourceKind, ProcessMockAgentCatalog.ProcessSourceKind, StringComparison.Ordinal) &&
               string.Equals(run.SourceId, assignment.StepKey, StringComparison.Ordinal) &&
               string.Equals(run.RequestedBy, "process-runtime", StringComparison.Ordinal) &&
               string.Equals(run.RequestedByKind, "system", StringComparison.Ordinal) &&
               MatchesGuid(run.CorrelationId, assignment.RunId.Value) &&
               MatchesGuid(run.CausationId, assignment.StepInstanceId.Value) &&
               MatchesGuid(run.ProcessRunId, assignment.RunId.Value) &&
               MatchesGuid(run.ProcessStepId, assignment.StepInstanceId.Value);
    }

    private static bool MatchesGuid(string value, Guid expected)
        => Guid.TryParse(value, out var actual) &&
           actual == expected;

    private static bool HasStructuredOutputValidationErrors(string? validationErrorsJson)
    {
        if (string.IsNullOrWhiteSpace(validationErrorsJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(validationErrorsJson);
            return document.RootElement.ValueKind != JsonValueKind.Array ||
                   document.RootElement.GetArrayLength() != 0;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static string BuildDurableEvidenceSummary(
        Guid executionRunId,
        AgentRunMetric? immediateMetric,
        ExecutionRunDetail detail)
    {
        var metricToolCalls = detail.Metrics.Sum(metric => (long)metric.ToolCalls);
        var usageToolCalls = detail.UsageObservations.Sum(observation => (long)observation.ToolCallCount);
        var pendingApprovals = detail.Approvals.Count(approval => approval.Status == ExecutionApprovalStatus.Pending);
        var waitingOnToolLogEntries = detail.ExecutionLog.Count(entry => entry.State == ExecutionState.WaitingOnTool);
        return
            $"executionRunId={executionRunId:D}; persistedRunId={detail.Run.Id:D}; state={detail.Run.State}; outcome={detail.Run.Outcome}; immediateMetricToolCalls={immediateMetric?.ToolCalls.ToString(CultureInfo.InvariantCulture) ?? "none"}; metrics={detail.Metrics.Count}; metricToolCalls={metricToolCalls.ToString(CultureInfo.InvariantCulture)}; usageObservations={detail.UsageObservations.Count}; usageToolCalls={usageToolCalls.ToString(CultureInfo.InvariantCulture)}; toolReceipts={detail.ToolReceipts.Count}; artifacts={detail.Artifacts.Count}; checkpoints={detail.Checkpoints.Count}; approvals={detail.Approvals.Count}; pendingApprovals={pendingApprovals}; runPendingApprovals={detail.Run.PendingApprovals.Count}; waitingOnToolLogEntries={waitingOnToolLogEntries}; structuredOutputRawOutput={(string.IsNullOrWhiteSpace(detail.Run.StructuredOutputRawOutput) ? "blank" : "present")}; serializedSessionState={(string.IsNullOrWhiteSpace(detail.Run.SerializedSessionStateJson) ? "blank" : "present")}; runtimeSessionKey={(string.IsNullOrWhiteSpace(detail.Run.RuntimeSessionKey) ? "blank" : "present")}";
    }

    private static string ComputeDurableEvidenceDigest(
        Guid executionRunId,
        AgentRunMetric? immediateMetric,
        ExecutionRunDetail detail)
    {
        var canonical = new StringBuilder();
        AppendCanonicalFact(canonical, "requestedExecutionRunId", executionRunId.ToString("D"));
        AppendCanonicalFact(canonical, "run.id", detail.Run.Id.ToString("D"));
        AppendCanonicalFact(canonical, "run.agentId", detail.Run.AgentId.ToString("D"));
        AppendCanonicalFact(canonical, "run.chatSessionId", detail.Run.ChatSessionId?.ToString("D") ?? string.Empty);
        AppendCanonicalFact(canonical, "run.sourceKind", detail.Run.SourceKind);
        AppendCanonicalFact(canonical, "run.sourceId", detail.Run.SourceId);
        AppendCanonicalFact(canonical, "run.correlationId", detail.Run.CorrelationId);
        AppendCanonicalFact(canonical, "run.causationId", detail.Run.CausationId);
        AppendCanonicalFact(canonical, "run.requestedBy", detail.Run.RequestedBy);
        AppendCanonicalFact(canonical, "run.requestedByKind", detail.Run.RequestedByKind);
        AppendCanonicalFact(canonical, "run.processRunId", detail.Run.ProcessRunId);
        AppendCanonicalFact(canonical, "run.processStepId", detail.Run.ProcessStepId);
        AppendCanonicalFact(canonical, "run.state", detail.Run.State.ToString());
        AppendCanonicalFact(canonical, "run.outcome", detail.Run.Outcome.ToString());
        AppendCanonicalFact(
            canonical,
            "run.completedAtUtcTicks",
            detail.Run.CompletedAtUtc?.UtcTicks.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        AppendCanonicalFact(canonical, "run.runtimeSessionKey", detail.Run.RuntimeSessionKey);
        AppendCanonicalFact(canonical, "run.serializedSessionStateJson", detail.Run.SerializedSessionStateJson);
        AppendCanonicalFact(
            canonical,
            "run.pendingApprovals",
            detail.Run.PendingApprovals.Count.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalFact(canonical, "run.structuredOutputRawOutput", detail.Run.StructuredOutputRawOutput);
        AppendCanonicalFact(
            canonical,
            "run.structuredOutputValidationStatus",
            detail.Run.StructuredOutputValidationStatus);
        AppendCanonicalFact(
            canonical,
            "run.structuredOutputValidationErrorsJson",
            detail.Run.StructuredOutputValidationErrorsJson);
        AppendCanonicalFact(canonical, "chatSession.present", (detail.ChatSession is not null).ToString());

        AppendCanonicalFact(canonical, "immediateMetric.present", (immediateMetric is not null).ToString());
        if (immediateMetric is not null)
        {
            AppendMetricFacts(canonical, "immediateMetric", immediateMetric);
        }

        AppendCanonicalFact(
            canonical,
            "metrics.count",
            detail.Metrics.Count.ToString(CultureInfo.InvariantCulture));
        var metricIndex = 0;
        foreach (var metric in detail.Metrics.OrderBy(metric => metric.Id))
        {
            AppendMetricFacts(canonical, $"metrics[{metricIndex}]", metric);
            metricIndex++;
        }

        AppendCanonicalFact(
            canonical,
            "usageObservations.count",
            detail.UsageObservations.Count.ToString(CultureInfo.InvariantCulture));
        var observationIndex = 0;
        foreach (var observation in detail.UsageObservations.OrderBy(observation => observation.Id))
        {
            var prefix = $"usageObservations[{observationIndex}]";
            AppendCanonicalFact(canonical, $"{prefix}.id", observation.Id.ToString("D"));
            AppendCanonicalFact(
                canonical,
                $"{prefix}.executionRunId",
                observation.ExecutionRunId?.ToString("D") ?? string.Empty);
            AppendCanonicalFact(
                canonical,
                $"{prefix}.agentId",
                observation.AgentId?.ToString("D") ?? string.Empty);
            AppendCanonicalFact(
                canonical,
                $"{prefix}.chatSessionId",
                observation.ChatSessionId?.ToString("D") ?? string.Empty);
            AppendCanonicalFact(canonical, $"{prefix}.usageStatus", observation.UsageStatus.ToString());
            AppendCanonicalFact(
                canonical,
                $"{prefix}.toolCallCount",
                observation.ToolCallCount.ToString(CultureInfo.InvariantCulture));
            AppendCanonicalFact(canonical, $"{prefix}.runtimeSessionKey", observation.RuntimeSessionKey);
            observationIndex++;
        }

        AppendCanonicalFact(
            canonical,
            "toolReceipts.count",
            detail.ToolReceipts.Count.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalFact(
            canonical,
            "artifacts.count",
            detail.Artifacts.Count.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalFact(
            canonical,
            "checkpoints.count",
            detail.Checkpoints.Count.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalFact(
            canonical,
            "approvals.count",
            detail.Approvals.Count.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalFact(
            canonical,
            "executionLog.count",
            detail.ExecutionLog.Count.ToString(CultureInfo.InvariantCulture));
        var logIndex = 0;
        foreach (var entry in detail.ExecutionLog
                     .OrderBy(entry => entry.CreatedAtUtc)
                     .ThenBy(entry => entry.Id))
        {
            var prefix = $"executionLog[{logIndex}]";
            AppendCanonicalFact(canonical, $"{prefix}.id", entry.Id.ToString("D"));
            AppendCanonicalFact(canonical, $"{prefix}.executionRunId", entry.ExecutionRunId.ToString("D"));
            AppendCanonicalFact(canonical, $"{prefix}.agentId", entry.AgentId.ToString("D"));
            AppendCanonicalFact(
                canonical,
                $"{prefix}.chatSessionId",
                entry.ChatSessionId?.ToString("D") ?? string.Empty);
            AppendCanonicalFact(
                canonical,
                $"{prefix}.createdAtUtcTicks",
                entry.CreatedAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture));
            AppendCanonicalFact(canonical, $"{prefix}.state", entry.State.ToString());
            logIndex++;
        }

        return ComputeHash(canonical.ToString());
    }

    private static void AppendMetricFacts(
        StringBuilder canonical,
        string prefix,
        AgentRunMetric metric)
    {
        AppendCanonicalFact(canonical, $"{prefix}.id", metric.Id.ToString("D"));
        AppendCanonicalFact(canonical, $"{prefix}.executionRunId", metric.ExecutionRunId.ToString("D"));
        AppendCanonicalFact(canonical, $"{prefix}.agentId", metric.AgentId.ToString("D"));
        AppendCanonicalFact(
            canonical,
            $"{prefix}.chatSessionId",
            metric.ChatSessionId?.ToString("D") ?? string.Empty);
        AppendCanonicalFact(canonical, $"{prefix}.outcome", metric.Outcome.ToString());
        AppendCanonicalFact(
            canonical,
            $"{prefix}.toolCalls",
            metric.ToolCalls.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendCanonicalFact(
        StringBuilder canonical,
        string key,
        string? value)
    {
        var safeValue = value ?? string.Empty;
        canonical
            .Append(key.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(key)
            .Append('=')
            .Append(safeValue.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(safeValue)
            .Append('\n');
    }

}
