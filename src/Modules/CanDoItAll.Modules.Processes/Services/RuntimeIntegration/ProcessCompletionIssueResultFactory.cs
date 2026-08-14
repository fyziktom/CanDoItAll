using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

using static CanDoItAll.Modules.Processes.ProcessBranchOutcomeResolver;
using static CanDoItAll.Modules.Processes.ProcessCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessProductCompletionRuleParser;
using static CanDoItAll.Modules.Processes.ProcessProductRootResolver;
using static CanDoItAll.Modules.Processes.ProcessCompletionText;
using static CanDoItAll.Modules.Processes.ProcessExecutionResultFactory;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactService;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactFormatter;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactOutcomeParser;
using static CanDoItAll.Modules.Processes.ProcessRequiredReceiptMatcher;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessCompletionIssueResultFactory
{
    private readonly IWorkspaceFileService workspaceFiles;
    private readonly ProcessCompletionDefectEvidenceCatalog completionDefectEvidenceCatalog;
    private readonly ProcessProductCompletionPathGate productCompletionPathGate;

    public ProcessCompletionIssueResultFactory(
        IWorkspaceFileService workspaceFiles,
        ProcessCompletionDefectEvidenceCatalog completionDefectEvidenceCatalog,
        ProcessProductCompletionPathGate productCompletionPathGate)
    {
        this.workspaceFiles = workspaceFiles ?? throw new ArgumentNullException(nameof(workspaceFiles));
        this.completionDefectEvidenceCatalog = completionDefectEvidenceCatalog ??
            throw new ArgumentNullException(nameof(completionDefectEvidenceCatalog));
        this.productCompletionPathGate = productCompletionPathGate ??
            throw new ArgumentNullException(nameof(productCompletionPathGate));
    }

    internal ProcessProductCompletionPathGate ProductCompletionPathGate => productCompletionPathGate;

    internal static ProcessExecutionAdapterResult NeedsManagerForCompletionIssue(
        ProcessRuntimeStepAssignment assignment,
        string rawOutputHash,
        ProcessCompletionIssue issue)
    {
        return NeedsManagerForCompletionIssues(
            assignment,
            rawOutputHash,
            new ProcessCompletionGateEvaluation([issue], [issue]));
    }

    internal static ProcessExecutionAdapterResult NeedsManagerForCompletionIssues(
        ProcessRuntimeStepAssignment assignment,
        string rawOutputHash,
        ProcessCompletionGateEvaluation evaluation)
    {
        if (evaluation.IsSatisfied)
        {
            throw new InvalidOperationException("Completion issue result requires at least one completion gate issue.");
        }

        var issues = evaluation.OrderedIssues
            .Take(ProcessStrategyResultLimits.MaximumDiagnostics)
            .Select(issue => issue with
            {
                Summary = ProcessReceiptNarrativeSanitizer.SanitizeText(
                    assignment,
                    issue.Summary,
                    ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength)
            })
            .ToArray();
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
                    issue.Idempotency)
                {
                    RelatedChildRunId = issue.RelatedChildRunId,
                    ExecutionSafetyAttestation = issue.ExecutionSafetyAttestation
                })
                .ToArray(),
            issues
                .Select(issue => new ManagerSignal(
                    new ManagerSignalCode(issue.Code),
                    ComputeHash($"{rawOutputHash}:manager:{issue.Code}:{issue.Evidence}"),
                    issue.Summary))
                .ToArray(),
            ProcessReceiptNarrativeSanitizer.SanitizeText(
                assignment,
                BuildCompletionGateSummary(issues),
                ProcessStrategyResultLimits.MaximumUserSafeSummaryLength),
            ComputeHash($"{rawOutputHash}:completion-gates:{string.Join("|", issues.Select(issue => $"{issue.Code}:{issue.Evidence}"))}"));
    }

    internal bool TryCreateRoutedCompletionIssueResult(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        string rawOutputHash,
        ProcessCompletionGateEvaluation evaluation,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        Guid? currentExecutionRunId,
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
            !HasCompletionDefectEvidence(
                assignment,
                output,
                toolReceipts,
                primaryIssue,
                currentExecutionRunId,
                out defectSummary))
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
        var safeRouteSummary = ProcessReceiptNarrativeSanitizer.SanitizeText(
            assignment,
            routeSummary,
            ProcessStrategyResultLimits.MaximumManagerSignalSummaryLength);
        var safePrimaryIssueSummary = ProcessReceiptNarrativeSanitizer.SanitizeText(
            assignment,
            primaryIssue.Summary,
            ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength);
        var safeDefectSummary = string.IsNullOrWhiteSpace(defectSummary)
            ? string.Empty
            : ProcessReceiptNarrativeSanitizer.SanitizeText(
                assignment,
                defectSummary,
                ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength);
        var diagnosticSummary = ProcessReceiptNarrativeSanitizer.SanitizeText(
            assignment,
            string.IsNullOrWhiteSpace(safeDefectSummary)
                ? safePrimaryIssueSummary
                : $"{safePrimaryIssueSummary} Deterministic defect evidence: {safeDefectSummary}.",
            ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength);
        var publicDiagnosticSummary = ProcessReceiptNarrativeSanitizer.SanitizeText(
            assignment,
            $"Completion issue '{primaryIssue.Code}' was routed from branch '{output.BranchOutcomeKey}' to branch '{route.TargetBranchOutcomeKey}'. {diagnosticSummary}",
            ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength);
        var publicResultSummary = ProcessReceiptNarrativeSanitizer.SanitizeText(
            assignment,
            $"Completion issue '{primaryIssue.Code}' was routed to branch '{route.TargetBranchOutcomeKey}'. {safePrimaryIssueSummary}",
            ProcessStrategyResultLimits.MaximumUserSafeSummaryLength);
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
                    publicDiagnosticSummary,
                    RestrictedEvidenceReference: null,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ],
            [
                new ManagerSignal(
                    ProcessBranchSignalCodes.Outcome(route.TargetBranchOutcomeKey),
                    ComputeHash($"{rawOutputHash}:branch:{route.TargetBranchOutcomeKey}:{primaryIssue.Evidence}"),
                    safeRouteSummary)
            ],
            publicResultSummary,
            ComputeHash($"{rawOutputHash}:completion-issue-routed:{primaryIssue.Code}:{primaryIssue.Evidence}:{route.TargetBranchOutcomeKey}"));
        return true;
    }

    internal ProcessCompletionIssue? AppendRuntimeGateFindingsForRoutedCompletionIssue(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        Guid executionRunId,
        ProcessCompletionGateEvaluation evaluation,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts)
    {
        if (!TryResolveCompletionIssueRoute(assignment, output, evaluation, out var primaryIssue, out var route))
        {
            return null;
        }

        if (route.RequiresDefectEvidence &&
            !HasCompletionDefectEvidence(
                assignment,
                output,
                toolReceipts,
                primaryIssue,
                executionRunId,
                out _))
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

    internal static bool TryResolveCompletionIssueRoute(
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
                (!candidate.OnlyAfterAutomaticRetry ||
                 ProcessExecutionMetadataBuilder.IsAutomaticRuntimeDiagnosticRecovery(assignment.Prompt)) &&
                IsApplicableToBranchOutcome(candidate.SourceBranchOutcomeKeys, output.BranchOutcomeKey))!;
        return route is not null;
    }

    internal static string BuildRuntimeGateFindingsContent(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        Guid executionRunId,
        ProcessCompletionIssue issue,
        ProcessCompletionIssueRoute route)
    {
        var safeRouteTitle = ProcessReceiptNarrativeSanitizer.SanitizeText(
            assignment,
            route.TargetBranchOutcomeTitle,
            ProcessStrategyResultLimits.MaximumManagerSignalSummaryLength);
        var safeIssueSummary = ProcessReceiptNarrativeSanitizer.SanitizeText(
            assignment,
            issue.Summary,
            ProcessStrategyResultLimits.MaximumDiagnosticSummaryLength);
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
        if (!string.IsNullOrWhiteSpace(safeRouteTitle))
        {
            builder.AppendLine($"- Routed branch title: {safeRouteTitle}");
        }

        builder.AppendLine($"- Gate issue code: {issue.Code}");
        builder.AppendLine($"- Gate issue summary: {safeIssueSummary}");
        builder.AppendLine($"- Defect evidence required: {route.RequiresDefectEvidence}");
        builder.AppendLine($"- Recorded at UTC: {DateTimeOffset.UtcNow:u}");
        return builder.ToString();
    }

    internal ProcessCompletionIssue? ValidateBranchOutcomeDefectEvidence(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        Guid? currentExecutionRunId)
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
            HasCompletionDefectEvidence(
                assignment,
                output,
                toolReceipts,
                issue: null,
                currentExecutionRunId,
                out _))
        {
            return null;
        }

        return new ProcessCompletionIssue(
            "process.adapter.branch_outcome_defect_evidence_missing",
            $"Step '{assignment.StepKey}' selected branch '{output.BranchOutcomeKey}', but that branch requires deterministic defect evidence and none was found in failed validation receipts, current-run contributed diagnostics, or product content checks.",
            $"{assignment.RunId}:{assignment.StepInstanceId}:branch-outcome-defect-evidence-missing:{output.BranchOutcomeKey}",
            assignment.ProducedArtifactSlotIds,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    internal bool HasCompletionDefectEvidence(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output,
        IReadOnlyList<ToolExecutionReceiptRecord>? toolReceipts,
        ProcessCompletionIssue? issue,
        Guid? currentExecutionRunId,
        out string defectSummary)
    {
        if (issue is not null &&
            (string.Equals(issue.Code, "process.adapter.product_required_file_content_missing", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(
                 issue.Code,
                 ProcessCompletionDiagnosticCodes.ToolReceiptEvidenceContentRejected,
                 StringComparison.OrdinalIgnoreCase) ||
             string.Equals(
                 issue.Code,
                 ProcessCompletionDiagnosticCodes.ProductSourceInspectionEvidenceMissing,
                 StringComparison.OrdinalIgnoreCase) ||
             string.Equals(
                 issue.Code,
                 ProcessCompletionDiagnosticCodes.UiInteractionEvidenceMissing,
                 StringComparison.OrdinalIgnoreCase) ||
             string.Equals(
                 issue.Code,
                 ProcessCompletionDiagnosticCodes.UiPostInteractionStateEvidenceMissing,
                 StringComparison.OrdinalIgnoreCase)))
        {
            defectSummary = issue.Summary;
            return true;
        }

        if (ProcessAcceptanceCriteriaGate.TryGetFailedCriterionEvidence(
                assignment,
                output,
                out defectSummary))
        {
            return true;
        }

        if (productCompletionPathGate.HasProductFileContentDefectEvidence(
                assignment,
                output,
                out defectSummary))
        {
            return true;
        }

        if (completionDefectEvidenceCatalog.TryDescribeDefectEvidence(
                new ProcessCompletionDefectEvidenceContext(
                    assignment,
                    output,
                    toolReceipts,
                    issue,
                    currentExecutionRunId),
                out defectSummary))
        {
            return true;
        }

        defectSummary = string.Empty;
        return false;
    }

    internal static IReadOnlyList<ArtifactSlotId> ResolveRequestedArtifactSlots(
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

    internal static string BuildCompletionGateSummary(IReadOnlyList<ProcessCompletionIssue> issues)
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
