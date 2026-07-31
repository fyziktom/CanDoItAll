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

using static CanDoItAll.Modules.Processes.ProcessAgentRightsDiagnosticPolicy;
using static CanDoItAll.Modules.Processes.ProcessCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessProductCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessProductCompletionStateGate
{
    internal static ProcessCompletionIssue? ValidateRequiredBranchOutcomeSelection(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        ProcessStepExecutionContract stepContract)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            !string.IsNullOrWhiteSpace(output.BranchOutcomeKey))
        {
            return null;
        }

        var runtimeRoutedBranchOutcomeKeys = ResolveRuntimeRoutedBranchOutcomeKeys(
            assignment.LaunchVariables,
            assignment.StepKey);
        var availableBranchOutcomeKeys = stepContract.ConfiguredBranchOutcomeIds
            .Select(outcome => outcome.Value)
            .Where(key => !runtimeRoutedBranchOutcomeKeys.Contains(
                key,
                StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (availableBranchOutcomeKeys.Length == 0)
        {
            return null;
        }

        return new ProcessCompletionIssue(
            ProcessCompletionDiagnosticCodes.RequiredBranchOutcomeMissing,
            $"Step '{assignment.StepKey}' returned Completed without selecting a required branch outcome. Select exactly one agent-visible branch key and preserve it when rewriting recovery evidence. Available keys: {string.Join(", ", availableBranchOutcomeKeys)}.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:required-branch-outcome-missing",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    internal static ProcessCompletionIssue? ValidateRuntimeRoutedBranchWasNotSelectedDirectly(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            string.IsNullOrWhiteSpace(output.BranchOutcomeKey))
        {
            return null;
        }

        var runtimeRoutedBranchOutcomeKeys = ResolveRuntimeRoutedBranchOutcomeKeys(
            assignment.LaunchVariables,
            assignment.StepKey);
        if (!runtimeRoutedBranchOutcomeKeys.Contains(
                output.BranchOutcomeKey,
                StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return new ProcessCompletionIssue(
            ProcessCompletionDiagnosticCodes.RuntimeRoutedBranchSelectedDirectly,
            $"Step '{assignment.StepKey}' directly selected runtime-routed branch '{output.BranchOutcomeKey}'. That branch is reserved for deterministic completion-evidence routing and is not an executable agent decision. Retry the same step and select only an agent-visible branch after performing its required work; the runtime will route missing mutation, validation, or readback evidence when applicable.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-routed-branch-selected-directly:{output.BranchOutcomeKey}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    internal static ProcessCompletionIssue? ValidateCompletedOutcomeDoesNotDeclareBlockers(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        ParentSubprocessBridgedOutcome? verifiedSubprocessOutcome)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed)
        {
            return null;
        }

        if (ProcessRuntimeLaunchVariables.AllowsCompletedOutcomeWithOpenIssues(
                assignment.LaunchVariables,
                output.BranchOutcomeKey) ||
            IsConfiguredCompletionIssueRouteTarget(assignment, output))
        {
            return null;
        }

        var requiresCurrentRunProof = RequiresCurrentRunProof(assignment);
        var blockerLines = EnumerateOutcomeTextOutsideVerifiedSubprocessEnvelopes(
                output,
                toolReceipts,
                verifiedSubprocessOutcome)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => SplitOutcomeLines(value!))
            .Where(line => DeclaresUnresolvedBlocker(line, requiresCurrentRunProof))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
        if (blockerLines.Length == 0)
        {
            return null;
        }

        var blockerSummary = string.Join(" | ", blockerLines);
        return new ProcessCompletionIssue(
            "process.adapter.completed_outcome_declares_unresolved_blocker",
            $"Step '{assignment.StepKey}' returned Completed while its outcome text still declares unresolved blocker or missing-acceptance state: {blockerSummary}. Return Blocked or repair the missing state before claiming completion.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:completed-outcome-declares-blocker",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static IEnumerable<string?> EnumerateOutcomeTextOutsideVerifiedSubprocessEnvelopes(
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        ParentSubprocessBridgedOutcome? verifiedSubprocessOutcome)
    {
        yield return output.Reason;
        yield return output.BranchOutcomeKey;
        yield return output.BranchOutcomeTitle;

        var summary = output.HumanReadableSummaryMarkdown;
        var verifiedEnvelopes = ProcessRuntimeSubprocessEnvelopeValidator.Resolve(
            verifiedSubprocessOutcome,
            toolReceipts);
        if (!string.IsNullOrWhiteSpace(summary) &&
            ProcessRuntimeSubprocessEnvelopeValidator.TryRemoveVerified(
                summary,
                verifiedEnvelopes,
                out var summaryWithoutVerifiedEnvelopes))
        {
            summary = summaryWithoutVerifiedEnvelopes;
        }

        yield return summary;

        foreach (var evidenceRef in output.EvidenceRefs)
        {
            yield return evidenceRef;
        }

        foreach (var nextAction in output.NextActions)
        {
            yield return nextAction;
        }
    }

    private static bool IsConfiguredCompletionIssueRouteTarget(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
        => !string.IsNullOrWhiteSpace(output.BranchOutcomeKey) &&
           ResolveCompletionIssueRoutes(assignment.LaunchVariables, assignment.StepKey)
               .Any(route => string.Equals(
                   route.TargetBranchOutcomeKey,
                   output.BranchOutcomeKey,
                   StringComparison.OrdinalIgnoreCase));

    internal static IEnumerable<string> SplitOutcomeLines(string value)
        => value.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line));

    internal static bool DeclaresUnresolvedBlocker(string line)
        => DeclaresUnresolvedBlocker(line, requiresCurrentRunProof: true);

    internal static bool DeclaresUnresolvedBlocker(string line, bool requiresCurrentRunProof)
    {
        if (ContainsNegatedBlockerPhrase(line))
        {
            return false;
        }

        return ContainsAny(
                line,
                "remaining blocker",
                "unresolved blocker",
                "still blocked",
                "cannot be treated as accepted",
                "cannot be accepted",
                "not launcher-compatible",
                "not launcher compatible",
                "pending writeback receipt") ||
            requiresCurrentRunProof &&
            (MissingRequiredReceiptRegex().IsMatch(line) ||
             DeclaresDeferredValidationProof(line));
    }

    private static bool RequiresCurrentRunProof(ProcessRuntimeStepAssignment assignment)
    {
        var allowedOperations = assignment.AllowedOperations;
        return ProcessExecutionMetadataBuilder.AllowsProductMutation(
                   allowedOperations,
                   assignment.OperationTargetScope) ||
               allowedOperations.Contains(
                   ProcessOperationContractNames.RunValidation,
                   StringComparer.OrdinalIgnoreCase) ||
               allowedOperations.Contains(
                   ProcessOperationContractNames.LaunchRuntime,
                   StringComparer.OrdinalIgnoreCase) ||
               allowedOperations.Contains(
                   ProcessOperationContractNames.CaptureRuntimeProof,
                   StringComparer.OrdinalIgnoreCase) ||
               allowedOperations.Contains(
                   ProcessOperationContractNames.ExecuteExternalAction,
                   StringComparer.OrdinalIgnoreCase);
    }

    internal static bool DeclaresDeferredValidationProof(string line)
    {
        if (!ContainsAny(line, "validation", "build", "test", "restore", "receipt", "proof", "evidence"))
        {
            return false;
        }

        return ContainsAny(
                line,
                "will be added",
                "will be captured",
                "will be recorded",
                "will be executed",
                "will run",
                "to be added",
                "to be captured",
                "to be recorded",
                "not yet captured",
                "not yet recorded",
                "not yet executed",
                "still planned",
                "planned rather than recorded") ||
            ContainsAny(line, "no current-run", "no current run", "missing current-run", "missing current run") &&
            ContainsAny(line, "receipt", "proof", "evidence", "command");
    }

    internal static bool ContainsNegatedBlockerPhrase(string line)
        => ContainsAny(
            line,
            "no remaining blocker",
            "no remaining blockers",
            "no unresolved blocker",
            "no unresolved blockers",
            "without remaining blocker",
            "without unresolved blocker",
            "no missing receipt",
            "no missing receipts",
            "no required receipt missing",
            "no required receipts missing",
            "without missing receipt",
            "blockers: none");

}
