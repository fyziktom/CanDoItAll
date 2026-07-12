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
using static CanDoItAll.Modules.Processes.ProcessBranchOutcomeResolver;
using static CanDoItAll.Modules.Processes.ProcessCompletionIssueResultFactory;
using static CanDoItAll.Modules.Processes.ProcessCompletionRetryPolicy;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactService;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactFormatter;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactOutcomeParser;
using static CanDoItAll.Modules.Processes.ProcessOutcomeCitationSanitizer;
using static CanDoItAll.Modules.Processes.ProcessProductCompletionRetryPolicy;
using static CanDoItAll.Modules.Processes.ProcessRequiredReceiptRetryPolicy;
using static CanDoItAll.Modules.Processes.ProcessSubprocessCompletionPolicy;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessExecutionResultConverter(
    ProcessCompletionGateEvaluator completionGateEvaluator,
    ProcessToolReceiptPolicyCatalog toolReceiptPolicies,
    ProcessCompletionIssueResultFactory completionIssueResultFactory)
{
    internal ProcessExecutionAdapterResult ToAdapterResult(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        string rawOutputHash,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts = null,
        Guid? currentExecutionRunId = null,
        IReadOnlyDictionary<ArtifactSlotId, string>? producedArtifactContentHashes = null,
        ProcessStepExecutionContract? stepContract = null)
    {
        var rawOutput = output;
        output = RemoveNonCitableSourceMetadataFromOutcome(output);

        var outcome = output.Status switch
        {
            ProcessStepOutcomeStatus.Completed => StrategyOutcome.Succeeded,
            ProcessStepOutcomeStatus.Blocked or ProcessStepOutcomeStatus.WaitingApproval => StrategyOutcome.NeedsManager,
            ProcessStepOutcomeStatus.Refused => StrategyOutcome.Canceled,
            ProcessStepOutcomeStatus.Failed => StrategyOutcome.Failed,
            _ => StrategyOutcome.Failed
        };
        if (outcome == StrategyOutcome.NeedsManager &&
            IsRetryableNonTerminalPrimaryArtifactBlocker(assignment, output, toolReceipts))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                CreateNonTerminalPrimaryArtifactRetryIssue(assignment, output));
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            IsRetryableManagedArtifactSelfEvidenceBlocker(assignment, output, toolReceipts))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                CreateManagedArtifactSelfEvidenceRetryIssue(assignment, output));
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            IsRetryableManagedArtifactMissingPrimaryOutputBlocker(assignment, output))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                CreateManagedArtifactMissingPrimaryOutputRetryIssue(assignment, output));
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            IsRetryableSubprocessLaunchSkippedBlocker(assignment, output, toolReceipts))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                CreateSubprocessLaunchSkippedRetryIssue(assignment, output));
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            TryCreateProductRequiredToolReceiptBlockedRetryIssue(
                assignment,
                output,
                toolReceipts,
                toolReceiptPolicies,
                out var requiredToolReceiptBlockedIssue))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                requiredToolReceiptBlockedIssue);
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            TryCreateProcessRequiredToolReceiptBlockedRetryIssue(
                assignment,
                output,
                toolReceipts,
                currentExecutionRunId,
                toolReceiptPolicies,
                out var processRequiredToolReceiptBlockedIssue))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                processRequiredToolReceiptBlockedIssue);
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            TryCreateProductRequiredStateBlockedRetryIssue(assignment, output, toolReceipts, out var requiredStateBlockedIssue))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                requiredStateBlockedIssue);
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            IsRetryableProductMutationEvidenceBlocker(
                assignment,
                output,
                toolReceipts,
                toolReceiptPolicies))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                CreateProductMutationEvidenceRetryIssue(assignment, output, toolReceipts!));
        }

        if ((outcome == StrategyOutcome.NeedsManager || outcome == StrategyOutcome.Succeeded) &&
            TryInferEvidenceBackedBranchOutcome(assignment, output, out var inferredBranchOutcomeKey))
        {
            output = CopyWithBranchOutcomeKey(output, inferredBranchOutcomeKey);
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            ShouldRouteBlockedBranchOutcome(assignment, output))
        {
            output = CopyAsCompletedBranchOutcome(output);
            outcome = StrategyOutcome.Succeeded;
        }

        if (outcome == StrategyOutcome.Succeeded &&
            IsRetryableSubprocessLaunchSkippedCompletion(assignment, output, toolReceipts))
        {
            return NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                CreateSubprocessLaunchSkippedRetryIssue(assignment, output));
        }

        if (outcome == StrategyOutcome.Succeeded)
        {
            var completionGateEvaluation = completionGateEvaluator.Evaluate(new ProcessCompletionGateContext(
                assignment,
                output,
                toolReceipts,
                currentExecutionRunId)
            {
                StepContract = stepContract ?? ProcessStepExecutionContract.Empty
            });
            if (!completionGateEvaluation.IsSatisfied)
            {
                if (completionIssueResultFactory.TryCreateRoutedCompletionIssueResult(
                    assignment,
                    output,
                    rawOutputHash,
                    completionGateEvaluation,
                    toolReceipts,
                    currentExecutionRunId,
                    producedArtifactContentHashes,
                    out var routedResult))
                {
                    return routedResult;
                }

                return NeedsManagerForCompletionIssues(assignment, rawOutputHash, completionGateEvaluation);
            }
        }

        IReadOnlyList<ProducedArtifactRef> artifacts = outcome == StrategyOutcome.Succeeded
            ? assignment.ProducedArtifactSlotIds
                .Select(slotId => new ProducedArtifactRef(
                    ArtifactInstanceId.New(),
                    slotId,
                    ResolveProducedArtifactContentHash(
                        slotId,
                        producedArtifactContentHashes,
                        rawOutputHash,
                        assignment.StepInstanceId)))
                .ToArray()
            : [];
        IReadOnlyList<RequestedArtifactRef> requestedArtifacts = outcome == StrategyOutcome.NeedsManager
            ? assignment.RequiredArtifactSlotIds
                .Select(slotId => new RequestedArtifactRef(
                    slotId,
                    ComputeHash($"{rawOutputHash}:requested:{slotId}")))
                .ToArray()
            : [];
        var diagnostics = new List<ProcessExecutionAdapterDiagnostic>();
        var managerSignals = new List<ManagerSignal>();
        var userSafeSummary = output.Reason;
        if (!string.IsNullOrWhiteSpace(output.BranchOutcomeKey))
        {
            managerSignals.Add(new ManagerSignal(
                ProcessBranchSignalCodes.Outcome(output.BranchOutcomeKey),
                ComputeHash(output.BranchOutcomeKey),
                string.IsNullOrWhiteSpace(output.BranchOutcomeTitle)
                    ? $"Branch outcome selected: {output.BranchOutcomeKey}"
                    : output.BranchOutcomeTitle));
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            TryBuildAgentRightsManagerRequest(assignment, output, out var managerRequest))
        {
            var rightsHash = ComputeHash($"{rawOutputHash}:agent-rights:{managerRequest}");
            diagnostics.Add(new ProcessExecutionAdapterDiagnostic(
                new StrategyDiagnosticCode(AgentRightsManagerRequestCode),
                StrategyDiagnosticSensitivity.Normal,
                rightsHash,
                managerRequest,
                RestrictedEvidenceReference: null,
                ProcessDiagnosticRetrySafety.UnsafeToRetry,
                ProcessDiagnosticIdempotencyClassification.Idempotent));
            managerSignals.Add(new ManagerSignal(
                new ManagerSignalCode(AgentRightsManagerRequestCode),
                rightsHash,
                managerRequest));
            userSafeSummary = string.IsNullOrWhiteSpace(userSafeSummary)
                ? managerRequest
                : $"{userSafeSummary}{Environment.NewLine}{Environment.NewLine}{managerRequest}";
        }

        if (outcome == StrategyOutcome.NeedsManager &&
            diagnostics.Count == 0)
        {
            var blockedSummary = BuildGenericBlockedDiagnosticSummary(assignment, output, rawOutput);
            var blockedHash = ComputeHash($"{rawOutputHash}:agent-blocked:{blockedSummary}");
            diagnostics.Add(new ProcessExecutionAdapterDiagnostic(
                new StrategyDiagnosticCode("process.adapter.agent_blocked"),
                StrategyDiagnosticSensitivity.Normal,
                blockedHash,
                blockedSummary,
                RestrictedEvidenceReference: null,
                ProcessDiagnosticRetrySafety.Unknown,
                ProcessDiagnosticIdempotencyClassification.Unknown));
            managerSignals.Add(new ManagerSignal(
                new ManagerSignalCode("process.adapter.agent_blocked"),
                blockedHash,
                blockedSummary));
            userSafeSummary = string.IsNullOrWhiteSpace(userSafeSummary)
                ? blockedSummary
                : userSafeSummary;
        }

        return new ProcessExecutionAdapterResult(
            outcome,
            artifacts,
            requestedArtifacts,
            diagnostics,
            managerSignals,
            userSafeSummary,
            rawOutputHash);
    }

    internal static string BuildGenericBlockedDiagnosticSummary(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        ProcessStepOutcomeResult rawOutput)
    {
        var reason = CompactDiagnosticText(FirstNonEmpty(
            RemoveNonCitableSourceMetadataFragments(rawOutput.Reason),
            output.Reason,
            output.HumanReadableSummaryMarkdown,
            "The agent returned a blocked process-step outcome without a classified adapter diagnostic."));
        var nextActions = rawOutput.NextActions
            .Select(RemoveNonCitableSourceMetadataFragments)
            .Concat(output.NextActions)
            .Where(action => !string.IsNullOrWhiteSpace(action))
            .Take(2)
            .Select(action => CompactDiagnosticText(action, 400))
            .ToArray();
        var nextActionSummary = nextActions.Length == 0
            ? string.Empty
            : $" Next action(s): {string.Join(" ", nextActions)}";
        return CompactDiagnosticText(
            $"Step '{assignment.StepKey}' returned {output.Status}: {reason}{nextActionSummary}",
            1600);
    }

    internal static string CompactDiagnosticText(string value, int maxLength = 800)
    {
        var compact = Regex.Replace(value.Trim(), @"\s+", " ");
        return compact.Length <= maxLength
            ? compact
            : compact[..maxLength].TrimEnd() + "...";
    }
}
