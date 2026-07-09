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
    private static ProcessExecutionAdapterResult NeedsManagerForCompletionIssue(
        ProcessRuntimeStepAssignment assignment,
        string rawOutputHash,
        ProcessCompletionIssue issue)
    {
        return NeedsManagerForCompletionIssues(
            assignment,
            rawOutputHash,
            new ProcessCompletionGateEvaluation([issue], [issue]));
    }

    private static ProcessExecutionAdapterResult NeedsManagerForCompletionIssues(
        ProcessRuntimeStepAssignment assignment,
        string rawOutputHash,
        ProcessCompletionGateEvaluation evaluation)
    {
        if (evaluation.IsSatisfied)
        {
            throw new InvalidOperationException("Completion issue result requires at least one completion gate issue.");
        }

        var issues = evaluation.OrderedIssues;
        var primaryIssue = issues[0];
        var requestedArtifactSlots = ResolveRequestedArtifactSlots(assignment, issues);
        return new ProcessExecutionAdapterResult(
            StrategyOutcome.NeedsManager,
            [],
            requestedArtifactSlots
                .Select(slotId => new RequestedArtifactRef(
                    slotId,
                    ComputeHash($"{rawOutputHash}:requested:{slotId}:{primaryIssue.Code}")))
                .ToArray(),
            issues
                .Select(issue => new ProcessExecutionAdapterDiagnostic(
                    new StrategyDiagnosticCode(issue.Code),
                    StrategyDiagnosticSensitivity.Normal,
                    ComputeHash(issue.Evidence),
                    issue.Summary,
                    RestrictedEvidenceReference: null,
                    issue.RetrySafety,
                    issue.Idempotency))
                .ToArray(),
            issues
                .Select(issue => new ManagerSignal(
                    new ManagerSignalCode(issue.Code),
                    ComputeHash($"{rawOutputHash}:manager:{issue.Code}:{issue.Evidence}"),
                    issue.Summary))
                .ToArray(),
            BuildCompletionGateSummary(issues),
            ComputeHash($"{rawOutputHash}:completion-gates:{string.Join("|", issues.Select(issue => $"{issue.Code}:{issue.Evidence}"))}"));
    }

    private static bool TryCreateRoutedCompletionIssueResult(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        string rawOutputHash,
        ProcessCompletionGateEvaluation evaluation,
        IReadOnlyDictionary<ArtifactSlotId, string>? producedArtifactContentHashes,
        out ProcessExecutionAdapterResult result)
    {
        result = null!;
        if (!TryResolveCompletionIssueRoute(assignment, output, evaluation, out var primaryIssue, out var route))
        {
            return false;
        }

        var defectSummary = string.Empty;
        if (route.RequiresDefectEvidence &&
            !HasCompletionDefectEvidence(assignment, output, null, primaryIssue, out defectSummary))
        {
            result = NeedsManagerForCompletionIssue(
                assignment,
                rawOutputHash,
                new ProcessCompletionIssue(
                    "process.adapter.branch_route_defect_evidence_missing",
                    $"Step '{assignment.StepKey}' has a configured route from completion issue '{primaryIssue.Code}' to branch '{route.TargetBranchOutcomeKey}', but no deterministic defect evidence was found for that route.",
                    $"{assignment.RunId}:{assignment.StepInstanceId}:branch-route-defect-evidence-missing:{primaryIssue.Code}:{route.TargetBranchOutcomeKey}",
                    assignment.ProducedArtifactSlotIds,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent));
            return true;
        }

        var artifacts = assignment.ProducedArtifactSlotIds
            .Select(slotId => new ProducedArtifactRef(
                ArtifactInstanceId.New(),
                slotId,
                ResolveProducedArtifactContentHash(
                    slotId,
                    producedArtifactContentHashes,
                    rawOutputHash,
                    assignment.StepInstanceId)))
            .ToArray();
        var routeSummary = string.IsNullOrWhiteSpace(route.TargetBranchOutcomeTitle)
            ? $"Branch outcome selected: {route.TargetBranchOutcomeKey}"
            : route.TargetBranchOutcomeTitle;
        var diagnosticSummary = string.IsNullOrWhiteSpace(defectSummary)
            ? primaryIssue.Summary
            : $"{primaryIssue.Summary} Deterministic defect evidence: {defectSummary}.";
        var diagnosticHash = ComputeHash($"{rawOutputHash}:routed-completion-issue:{primaryIssue.Code}:{primaryIssue.Evidence}:{route.TargetBranchOutcomeKey}");
        result = new ProcessExecutionAdapterResult(
            StrategyOutcome.Succeeded,
            artifacts,
            [],
            [
                new ProcessExecutionAdapterDiagnostic(
                    new StrategyDiagnosticCode("process.adapter.completion_issue_routed"),
                    StrategyDiagnosticSensitivity.Normal,
                    diagnosticHash,
                    $"Completion issue '{primaryIssue.Code}' was routed from branch '{output.BranchOutcomeKey}' to branch '{route.TargetBranchOutcomeKey}'. {diagnosticSummary}",
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [
                new ManagerSignal(
                    ProcessBranchSignalCodes.Outcome(route.TargetBranchOutcomeKey),
                    ComputeHash($"{rawOutputHash}:branch:{route.TargetBranchOutcomeKey}:{primaryIssue.Evidence}"),
                    routeSummary)
            ],
            $"Completion issue '{primaryIssue.Code}' was routed to branch '{route.TargetBranchOutcomeKey}'. {primaryIssue.Summary}",
            ComputeHash($"{rawOutputHash}:completion-issue-routed:{primaryIssue.Code}:{primaryIssue.Evidence}:{route.TargetBranchOutcomeKey}"));
        return true;
    }

    private ProcessCompletionIssue? AppendRuntimeGateFindingsForRoutedCompletionIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        Guid executionRunId,
        ProcessCompletionGateEvaluation evaluation)
    {
        if (!TryResolveCompletionIssueRoute(assignment, output, evaluation, out var primaryIssue, out var route))
        {
            return null;
        }

        if (route.RequiresDefectEvidence &&
            !HasCompletionDefectEvidence(assignment, output, null, primaryIssue, out _))
        {
            return null;
        }

        var primaryRef = BuildManagedStepArtifactPath(assignment);
        var appendResult = workspaceFiles.AppendTextFile(
            primaryRef,
            BuildRuntimeGateFindingsContent(assignment, output, executionRunId, primaryIssue, route));
        if (appendResult.Succeeded)
        {
            return null;
        }

        return new ProcessCompletionIssue(
            "process.adapter.runtime_gate_findings_append_failed",
            $"Step '{assignment.StepKey}' had a configured branch route for completion issue '{primaryIssue.Code}', but runtime gate findings could not be appended to primary managed artifact '{primaryRef}': {appendResult.Message}",
            $"{assignment.RunId}:{assignment.StepInstanceId}:runtime-gate-findings-append-failed:{primaryIssue.Code}:{route.TargetBranchOutcomeKey}:{appendResult.Message}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static bool TryResolveCompletionIssueRoute(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        ProcessCompletionGateEvaluation evaluation,
        out ProcessCompletionIssue primaryIssue,
        out ProcessCompletionIssueRoute route)
    {
        var issue = evaluation.OrderedIssues[0];
        primaryIssue = issue;
        route = ResolveCompletionIssueRoutes(assignment.LaunchVariables, assignment.StepKey)
            .FirstOrDefault(candidate =>
                string.Equals(candidate.IssueCode, issue.Code, StringComparison.OrdinalIgnoreCase) &&
                IsApplicableToBranchOutcome(candidate.SourceBranchOutcomeKeys, output.BranchOutcomeKey))!;
        return route is not null;
    }

    private static string BuildRuntimeGateFindingsContent(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        Guid executionRunId,
        ProcessCompletionIssue issue,
        ProcessCompletionIssueRoute route)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("## Runtime gate findings");
        builder.AppendLine();
        builder.AppendLine("The process runtime routed this completed outcome because a deterministic completion gate failed for the selected branch.");
        builder.AppendLine();
        builder.AppendLine($"- Run id: {assignment.RunId.Value:D}");
        builder.AppendLine($"- Step id: {assignment.StepInstanceId.Value:D}");
        builder.AppendLine($"- Step key: {assignment.StepKey}");
        builder.AppendLine($"- Execution run id: {executionRunId:D}");
        builder.AppendLine($"- Source branch outcome: {output.BranchOutcomeKey}");
        builder.AppendLine($"- Routed branch outcome: {route.TargetBranchOutcomeKey}");
        if (!string.IsNullOrWhiteSpace(route.TargetBranchOutcomeTitle))
        {
            builder.AppendLine($"- Routed branch title: {route.TargetBranchOutcomeTitle}");
        }

        builder.AppendLine($"- Gate issue code: {issue.Code}");
        builder.AppendLine($"- Gate issue summary: {issue.Summary}");
        builder.AppendLine($"- Defect evidence required: {route.RequiresDefectEvidence}");
        builder.AppendLine($"- Recorded at UTC: {DateTimeOffset.UtcNow:u}");
        return builder.ToString();
    }

    private static ProcessCompletionIssue? ValidateBranchOutcomeDefectEvidence(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
    {
        if (output.Status != ProcessStepOutcomeStatus.Completed ||
            string.IsNullOrWhiteSpace(output.BranchOutcomeKey))
        {
            return null;
        }

        var route = ResolveCompletionIssueRoutes(assignment.LaunchVariables, assignment.StepKey)
            .FirstOrDefault(candidate =>
                candidate.RequiresDefectEvidence &&
                string.Equals(candidate.TargetBranchOutcomeKey, output.BranchOutcomeKey, StringComparison.OrdinalIgnoreCase));
        if (route is null ||
            HasCompletionDefectEvidence(assignment, output, toolReceipts, issue: null, out _))
        {
            return null;
        }

        return new ProcessCompletionIssue(
            "process.adapter.branch_outcome_defect_evidence_missing",
            $"Step '{assignment.StepKey}' selected branch '{output.BranchOutcomeKey}', but that branch requires deterministic defect evidence and none was found in failed validation receipts or product content checks.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:branch-outcome-defect-evidence-missing:{output.BranchOutcomeKey}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static bool HasCompletionDefectEvidence(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        ProcessCompletionIssue? issue,
        out string defectSummary)
    {
        if (issue is not null &&
            string.Equals(issue.Code, "process.adapter.product_required_file_content_missing", StringComparison.OrdinalIgnoreCase))
        {
            defectSummary = issue.Summary;
            return true;
        }

        if (HasProductFileContentDefectEvidence(assignment, output, out defectSummary))
        {
            return true;
        }

        var failedValidationReceipts = (toolReceipts ?? [])
            .Where(receipt => !IsSuccessfulReceipt(receipt.ExitSummary) && IsValidationDefectEvidenceReceipt(receipt))
            .Select(receipt => $"{receipt.ToolName} ({SummarizeReceiptExit(receipt.ExitSummary)})")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
        if (failedValidationReceipts.Length == 0)
        {
            defectSummary = string.Empty;
            return false;
        }

        defectSummary = string.Join("; ", failedValidationReceipts);
        return true;
    }

    private static bool IsValidationDefectEvidenceReceipt(ToolExecutionReceiptRecord receipt)
    {
        return receipt.ToolName.Contains("build", StringComparison.OrdinalIgnoreCase) ||
               receipt.ToolName.Contains("test", StringComparison.OrdinalIgnoreCase) ||
               receipt.ToolName.Contains("restore", StringComparison.OrdinalIgnoreCase) ||
               receipt.ToolName.Contains("browser", StringComparison.OrdinalIgnoreCase) ||
               receipt.ToolName.Contains("validation", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ArtifactSlotId> ResolveRequestedArtifactSlots(
        ProcessRuntimeStepAssignment assignment,
        IReadOnlyList<ProcessCompletionIssue> issues)
    {
        var requestedArtifactSlots = issues
            .SelectMany(issue => issue.RequestedArtifactSlotIds)
            .Distinct()
            .ToArray();
        if (requestedArtifactSlots.Length > 0)
        {
            return requestedArtifactSlots;
        }

        return assignment.ProducedArtifactSlotIds.Count > 0
            ? assignment.ProducedArtifactSlotIds
            : assignment.RequiredArtifactSlotIds;
    }

    private static string BuildCompletionGateSummary(IReadOnlyList<ProcessCompletionIssue> issues)
    {
        if (issues.Count == 1)
        {
            return issues[0].Summary;
        }

        var primaryIssue = issues[0];
        var secondarySummaries = issues
            .Skip(1)
            .Select(issue => $"- {issue.Code}: {issue.Summary}");
        return $"Completion gates are unsatisfied. Primary issue: {primaryIssue.Summary}{Environment.NewLine}{Environment.NewLine}Additional completion gate issue(s):{Environment.NewLine}{string.Join(Environment.NewLine, secondarySummaries)}";
    }
}
