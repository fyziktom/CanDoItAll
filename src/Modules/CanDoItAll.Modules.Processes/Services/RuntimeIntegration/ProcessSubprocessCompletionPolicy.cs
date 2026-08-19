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

using static CanDoItAll.Modules.Processes.ProcessAgentRightsDiagnosticPolicy;
using static CanDoItAll.Modules.Processes.ProcessCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessProductCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessProductRootResolver;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactService;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactFormatter;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactOutcomeParser;
using static CanDoItAll.Modules.Processes.ProcessOutcomeGroundingValidator;
using static CanDoItAll.Modules.Processes.ProcessRuntimeFailureClassifier;
using static CanDoItAll.Modules.Processes.ProcessSubprocessState;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessSubprocessCompletionPolicy
{
    private const int MaximumPreflightDiagnosticItems = 16;
    private const int MaximumPreflightTokenLength = 96;

    internal static bool IsRetryableSubprocessLaunchSkippedBlocker(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
    {
        if (output.Status != ProcessStepOutcomeStatus.Blocked ||
            !RequiresSubprocessLaunch(assignment) ||
            HasToolReceipt(toolReceipts, SubprocessLaunchToolName))
        {
            return false;
        }

        var text = string.Join(
            " ",
            EnumerateOutcomeText(output).Where(value => !string.IsNullOrWhiteSpace(value)));
        if (LooksLikeParentExpectedDirectChildTools(text))
        {
            return true;
        }

        if (LooksLikeSubprocessLaunchToolBoundary(text))
        {
            return false;
        }

        if (LooksLikeUnverifiedSubprocessLaunchCapabilityBlocker(text))
        {
            return true;
        }

        if (LooksLikeRightsOrToolBoundary(text))
        {
            return false;
        }

        return ContainsAny(
            text,
            "subprocess was not launched",
            "subprocess were not launched",
            "child subprocess was not launched",
            "required subprocess was not launched",
            "required child run was not launched",
            "child run was not launched",
            "no current child run",
            "no child run receipt",
            "missing child run receipt");
    }

    internal static bool IsRetryableSubprocessLaunchSkippedCompletion(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
    {
        return output.Status == ProcessStepOutcomeStatus.Completed &&
               RequiresSubprocessLaunch(assignment) &&
               !HasToolReceipt(toolReceipts, SubprocessLaunchToolName) &&
               !HasChildProcessEvidenceRef(assignment, output.EvidenceRefs);
    }

    internal static bool RequiresSubprocessLaunch(ProcessRuntimeStepAssignment assignment)
    {
        return ProcessRuntimeLaunchVariables.TryReadProcessStepSubprocessDefinitionKey(
                   assignment.LaunchVariables,
                   out _) &&
               assignment.AllowedOperations.Contains(
                   ProcessOperationContractNames.ExecuteExternalAction,
                   StringComparer.OrdinalIgnoreCase);
    }

    internal static IReadOnlyList<string> ResolvePreflightRequiredRuntimeToolNames(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepExecutionContract stepContract)
    {
        return ProcessRequiredRuntimeToolNames.NormalizeRuntimeToolNameCandidates(stepContract.RequiredRuntimeToolNames)
            .Concat(ProcessRequiredToolReceiptGate.ResolveRequiredRuntimeToolNames(assignment.CapabilityScope))
            .Concat(ProcessRequiredRuntimeToolNames.FromProductCompletionRequiredToolReceipts(
                ResolveUnconditionalProductCompletionRequiredToolReceipts(
                    assignment.LaunchVariables,
                    assignment.StepKey)))
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(toolName => toolName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static ProcessCompletionIssue CreateRuntimeToolPreflightIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessRuntimeToolPreflightResult result)
    {
        var missingTokens = result.MissingToolNames
            .Select(NormalizePreflightToken)
            .Where(token => token.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(MaximumPreflightDiagnosticItems)
            .ToArray();
        var missingSummary = missingTokens.Length == 0
            ? "not-reported"
            : string.Join(", ", missingTokens);
        var stepKey = NormalizePreflightToken(assignment.StepKey);
        var detailSummary =
            $"Missing={result.MissingToolNames.Count}; PlanIssues={result.PlanIssues.Count}; CapabilityIssues={result.CapabilityDiagnostics.Count}; HostCapabilityIssues={result.HostCapabilityFindings.Count}.";

        return new ProcessCompletionIssue(
            "process.adapter.runtime_tool_preflight_failed",
            $"Step '{stepKey}' cannot be dispatched because runtime tool preflight failed before side effects for: {missingSummary}. {detailSummary}",
            $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-tool-preflight:{missingSummary}:{detailSummary}",
            assignment.ProducedArtifactSlotIds.Count > 0 ? assignment.ProducedArtifactSlotIds : assignment.RequiredArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static string NormalizePreflightToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Take(MaximumPreflightTokenLength)
            .Where(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_' or ':')
            .ToArray());
    }

    internal static bool HasChildProcessEvidenceRef(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<string> evidenceRefs)
    {
        var ownRunId = assignment.RunId.Value.ToString("D");
        foreach (var evidenceRef in evidenceRefs)
        {
            var normalizedRef = evidenceRef.Replace('\\', '/');
            var match = Regex.Match(
                normalizedRef,
                @"(?:^|/)process-runs/(?<runId>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})(?:/|$)",
                RegexOptions.CultureInvariant);
            if (match.Success &&
                !string.Equals(match.Groups["runId"].Value, ownRunId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool LooksLikeSubprocessLaunchToolBoundary(string text)
    {
        if (ContainsAny(text, SubprocessLaunchToolName))
        {
            return ContainsAny(
                text,
                $"Tool '{SubprocessLaunchToolName}'",
                $"tool '{SubprocessLaunchToolName}'",
                $"Tool \"{SubprocessLaunchToolName}\"",
                $"tool \"{SubprocessLaunchToolName}\"",
                $"no {SubprocessLaunchToolName}",
                $"{SubprocessLaunchToolName} not available",
                $"{SubprocessLaunchToolName} unavailable",
                $"not authorized to use {SubprocessLaunchToolName}",
                $"denied tool {SubprocessLaunchToolName}");
        }

        return ContainsAny(
            text,
            "subprocess launch tool is not available",
            "subprocess launch tool unavailable");
    }

    internal static bool LooksLikeParentExpectedDirectChildTools(string text)
    {
        if (!ContainsAny(text, "subprocess", "child process", "child run"))
        {
            return false;
        }

        if (ContainsAny(
            text,
            "step contract explicitly says to launch",
            "only project-structure subprocess launch tools are available",
            "only subprocess launch tools are available"))
        {
            return true;
        }

        return ContainsAny(
                   text,
                   "direct child-work tools",
                   "direct implementation",
                   "direct validation",
                   "parent toolset",
                   "child-work capability") &&
               ContainsAny(
                   text,
                   "not available",
                   "not exposed",
                   "missing tool",
                   "capability",
                   "cannot proceed");
    }

    internal static bool LooksLikeUnverifiedSubprocessLaunchCapabilityBlocker(string text)
    {
        if (ContainsAny(text, "composed capability set", "not part of the composed capability set"))
        {
            return false;
        }

        if (!ContainsAny(text, "subprocess", "child process", "child run", SubprocessLaunchToolName))
        {
            return false;
        }

        if (!ContainsAny(text, "launch capability", "child launch", "launch path", "ExecuteExternalAction", SubprocessLaunchToolName))
        {
            return false;
        }

        return ContainsAny(
            text,
            "unavailable",
            "not available",
            "does not expose",
            "not expose",
            "missing",
            "cannot launch",
            "grant",
            "reassign");
    }

    internal static ProcessCompletionIssue CreateSubprocessLaunchSkippedRetryIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        ProcessRuntimeLaunchVariables.TryReadProcessStepSubprocessDefinitionKey(
            assignment.LaunchVariables,
            out var subprocessDefinitionKey);
        var childKeySummary = string.IsNullOrWhiteSpace(subprocessDefinitionKey)
            ? "the mapped child process definition"
            : $"DefinitionKey '{subprocessDefinitionKey}'";
        var originalReason = string.IsNullOrWhiteSpace(output.Reason)
            ? $"The agent returned {output.Status} before launching the required subprocess."
            : output.Reason.Trim();
        var requestedSlots = assignment.ProducedArtifactSlotIds.Count > 0
            ? assignment.ProducedArtifactSlotIds
            : assignment.RequiredArtifactSlotIds;

        return new ProcessCompletionIssue(
            "process.adapter.subprocess_launch_skipped_retry",
            $"Step '{assignment.StepKey}' is mapped to a subprocess and has ExecuteExternalAction, but the agent returned {output.Status} before invoking {SubprocessLaunchToolName} or citing child-run evidence. Retry the same step: call {SubprocessLaunchToolName} with {childKeySummary}; if launch returns ParentDeferredOutcomeJson, submit that deferred outcome exactly. Complete from child evidence only after a stopped child run is cited through managed artifact refs. Block only after a current launch-tool denial, missing required launch input, or concrete stopped-child blocker. Original reason: {originalReason}",
            $"{assignment.RunId}:{assignment.StepInstanceId}:subprocess-launch-skipped:{subprocessDefinitionKey}:{ComputeHash(originalReason)}",
            requestedSlots,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    internal static ProcessCompletionIssue CreateSubprocessLaunchCoordinatorMissingOutcomeIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessSubprocessLaunchCoordinatorResult launch)
    {
        var childRunSummary = launch.ChildRunId is { } childRunId
            ? childRunId.Value.ToString("D")
            : "no child run";
        return new ProcessCompletionIssue(
            "process.adapter.subprocess_launch_missing_parent_outcome",
            $"Step '{assignment.StepKey}' launched mapped subprocess DefinitionKey '{launch.DefinitionKey}' with stage '{launch.Stage}' and {childRunSummary}, but the launch coordinator did not return a parent deferred outcome.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:subprocess-launch-missing-parent-outcome:{launch.DefinitionKey}:{childRunSummary}:{launch.Stage}",
            assignment.ProducedArtifactSlotIds.Count > 0 ? assignment.ProducedArtifactSlotIds : assignment.RequiredArtifactSlotIds,
            ProcessDiagnosticRetrySafety.Unknown,
            ProcessDiagnosticIdempotencyClassification.Unknown)
        {
            RelatedChildRunId = launch.ChildRunId
        };
    }

    internal static ProcessCompletionIssue CreateSubprocessLaunchDefinitionMissingIssue(
        ProcessRuntimeStepAssignment assignment)
    {
        return new ProcessCompletionIssue(
            "process.adapter.subprocess_launch_definition_missing",
            $"Step '{assignment.StepKey}' is configured as a mapped subprocess launch, but the runtime assignment does not contain a child process definition key.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:subprocess-launch-definition-missing",
            assignment.ProducedArtifactSlotIds.Count > 0 ? assignment.ProducedArtifactSlotIds : assignment.RequiredArtifactSlotIds,
            ProcessDiagnosticRetrySafety.Unknown,
            ProcessDiagnosticIdempotencyClassification.Unknown);
    }

    internal static ProcessCompletionIssue CreateSubprocessLaunchCoordinatorUnavailableIssue(
        ProcessRuntimeStepAssignment assignment,
        string subprocessDefinitionKey)
    {
        return new ProcessCompletionIssue(
            "process.adapter.subprocess_launch_coordinator_unavailable",
            $"Step '{assignment.StepKey}' is mapped to subprocess DefinitionKey '{subprocessDefinitionKey}', but no subprocess launch coordinator is registered for this runtime.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:subprocess-launch-coordinator-unavailable:{subprocessDefinitionKey}",
            assignment.ProducedArtifactSlotIds.Count > 0 ? assignment.ProducedArtifactSlotIds : assignment.RequiredArtifactSlotIds,
            ProcessDiagnosticRetrySafety.Unknown,
            ProcessDiagnosticIdempotencyClassification.Unknown);
    }

    internal static ProcessCompletionIssue CreateSubprocessLaunchNotHandledIssue(
        ProcessRuntimeStepAssignment assignment,
        string subprocessDefinitionKey)
    {
        return new ProcessCompletionIssue(
            "process.adapter.subprocess_launch_not_handled",
            $"Step '{assignment.StepKey}' is mapped to subprocess DefinitionKey '{subprocessDefinitionKey}', but the registered subprocess launch coordinator did not handle this assignment.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:subprocess-launch-not-handled:{subprocessDefinitionKey}",
            assignment.ProducedArtifactSlotIds.Count > 0 ? assignment.ProducedArtifactSlotIds : assignment.RequiredArtifactSlotIds,
            ProcessDiagnosticRetrySafety.Unknown,
            ProcessDiagnosticIdempotencyClassification.Unknown);
    }

    internal static ProcessCompletionIssue CreateSubprocessLaunchFailedIssue(
        ProcessRuntimeStepAssignment assignment,
        string subprocessDefinitionKey,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var requestedSlots = assignment.ProducedArtifactSlotIds.Count > 0
            ? assignment.ProducedArtifactSlotIds
            : assignment.RequiredArtifactSlotIds;
        return new ProcessCompletionIssue(
            "process.adapter.subprocess_launch_failed",
            $"Step '{assignment.StepKey}' could not launch mapped subprocess DefinitionKey '{subprocessDefinitionKey}'. The runtime preserved the parent step instead of retrying an indeterminate launch. Review the launch contract or child-process boundary before rework; restricted failure detail is represented by the diagnostic evidence hash.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:subprocess-launch-failed:{subprocessDefinitionKey}:{ComputeHash(exception.GetType().FullName + ":" + exception.Message)}",
            requestedSlots,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Unknown);
    }

    internal static ProcessCompletionIssue CreateSubprocessContractMissingIssue(
        ProcessRuntimeStepAssignment assignment)
    {
        return new ProcessCompletionIssue(
            "process.adapter.subprocess_contract_missing",
            $"Step '{assignment.StepKey}' is a subprocess parent, but the runtime assignment does not carry a typed subprocess contract and no compatibility contract could be resolved. Rework the run metadata or relaunch from a hardened template before retrying.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:subprocess-contract-missing",
            assignment.ProducedArtifactSlotIds.Count > 0 ? assignment.ProducedArtifactSlotIds : assignment.RequiredArtifactSlotIds,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Unknown);
    }

    internal static ProcessCompletionIssue CreateSubprocessChildNoGoIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessRunId childRunId,
        IReadOnlyList<string> evidenceRefs)
    {
        var evidenceSummary = evidenceRefs.Count == 0
            ? "no no-go refs"
            : string.Join("; ", evidenceRefs);
        return new ProcessCompletionIssue(
            "process.adapter.subprocess_child_nogo_output",
            $"Step '{assignment.StepKey}' has completed child run '{childRunId.Value:D}', but the child produced a typed no-go output. Treat this as blocker evidence, not accepted parent proof. No-go evidence: {evidenceSummary}.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:subprocess-child-nogo:{childRunId}:{evidenceSummary}",
            assignment.ProducedArtifactSlotIds.Count > 0 ? assignment.ProducedArtifactSlotIds : assignment.RequiredArtifactSlotIds,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent)
        {
            RelatedChildRunId = childRunId
        };
    }

    internal static ProcessCompletionIssue CreateSubprocessChildAcceptedOutputMissingIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessRunId childRunId,
        ProcessSubprocessContract contract)
    {
        var acceptedOutputs = contract.AcceptedChildOutputs.Count == 0
            ? "none"
            : string.Join(
                ", ",
                contract.AcceptedChildOutputs.Select(output =>
                    string.IsNullOrWhiteSpace(output.ArtifactExpectationKey)
                        ? output.StepKey
                        : $"{output.StepKey}/{output.ArtifactExpectationKey}"));
        return new ProcessCompletionIssue(
            "process.adapter.subprocess_child_accepted_output_missing",
            $"Step '{assignment.StepKey}' has completed child run '{childRunId.Value:D}', but none of the typed accepted child outputs were materialized. Expected one of: {acceptedOutputs}. Do not complete the parent from a generic child folder.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:subprocess-child-accepted-output-missing:{childRunId}:{acceptedOutputs}",
            assignment.ProducedArtifactSlotIds.Count > 0 ? assignment.ProducedArtifactSlotIds : assignment.RequiredArtifactSlotIds,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent)
        {
            RelatedChildRunId = childRunId
        };
    }

    internal static ProcessCompletionIssue CreateSubprocessChildForwardedContextIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessRunId childRunId,
        ParentSubprocessForwardedContextIssue forwardedContextIssue)
    {
        ArgumentNullException.ThrowIfNull(forwardedContextIssue);

        return new ProcessCompletionIssue(
            forwardedContextIssue.Code,
            $"Step '{assignment.StepKey}' has completed child run '{childRunId.Value:D}', but the runtime could not forward a typed child context artifact required by the parent contract. {forwardedContextIssue.SafeSummary}",
            $"{assignment.RunId}:{assignment.StepInstanceId}:subprocess-child-forwarded-context:{childRunId}:{forwardedContextIssue.Evidence}",
            assignment.ProducedArtifactSlotIds.Count > 0 ? assignment.ProducedArtifactSlotIds : assignment.RequiredArtifactSlotIds,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent)
        {
            RelatedChildRunId = childRunId
        };
    }

    internal static ProcessCompletionIssue CreateSubprocessChildStoppedIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessRunId childRunId,
        ParentSubprocessStoppedChild stoppedChild,
        bool failed)
    {
        var diagnosticSummary = stoppedChild.Diagnostics.Count == 0
            ? "no child diagnostics were recorded"
            : string.Join(
                " | ",
                stoppedChild.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.SafeSummary}"));
        var recoverySummary = stoppedChild.RecoveryDecision is null
            ? "no child recovery decision was recorded"
            : $"child recovery decision {stoppedChild.RecoveryDecision.DecisionKind}/{stoppedChild.RecoveryDecision.RouteKind}; policy {stoppedChild.RecoveryDecision.Policy}; retry {stoppedChild.RecoveryDecision.AutomaticRetryAttempt}/{stoppedChild.RecoveryDecision.MaximumAutomaticRetryAttempts}; persistent diagnostic identity {stoppedChild.RecoveryDecision.SameDiagnosticFingerprintAttempt}/{stoppedChild.RecoveryDecision.MaximumSameDiagnosticFingerprintAttempts}; reason {stoppedChild.RecoveryDecision.SafeReason}";
        var childStepId = stoppedChild.ChildStepInstanceId?.Value.ToString("D") ?? "unknown";
        var code = failed
            ? ProcessExecutionAdapterDiagnosticCodes.SubprocessChildFailed
            : ProcessExecutionAdapterDiagnosticCodes.SubprocessChildBlocked;
        var statusLabel = failed ? "failed" : "blocked";
        var summary = $"Step '{assignment.StepKey}' has {statusLabel} child process run '{childRunId.Value:D}' at child step '{stoppedChild.ChildStepKey}' ({childStepId}); child runtime status {stoppedChild.ChildStatus}; child step status {stoppedChild.ChildStepStatus?.ToString() ?? "unknown"}. Child diagnostic(s): {diagnosticSummary}. {recoverySummary}.";
        var evidence = $"{assignment.RunId}:{assignment.StepInstanceId}:subprocess-child-{statusLabel}:{childRunId}:{stoppedChild.ChildStatus}:{stoppedChild.ChildStepKey}:{childStepId}:{string.Join("|", stoppedChild.Diagnostics.Select(diagnostic => $"{diagnostic.Code}:{diagnostic.EvidenceHash}"))}:{recoverySummary}";
        return new ProcessCompletionIssue(
            code,
            summary,
            evidence,
            assignment.ProducedArtifactSlotIds.Count > 0 ? assignment.ProducedArtifactSlotIds : assignment.RequiredArtifactSlotIds,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent)
        {
            RelatedChildRunId = childRunId
        };
    }

    internal static bool HasToolReceipt(
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        string toolName)
    {
        return toolReceipts?.Any(receipt =>
            string.Equals(receipt.ToolName, toolName, StringComparison.OrdinalIgnoreCase)) == true;
    }

}
