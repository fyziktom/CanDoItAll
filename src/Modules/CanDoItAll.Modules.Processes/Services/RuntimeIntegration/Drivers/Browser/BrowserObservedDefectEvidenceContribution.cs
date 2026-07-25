using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;
using static CanDoItAll.Modules.Processes.ProcessOutcomeGroundingValidator;

namespace CanDoItAll.Modules.Processes;

internal sealed class BrowserObservedDefectEvidenceContribution : IProcessCompletionDefectEvidenceContribution
{
    private static readonly HashSet<string> BrowserStateEvidenceToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ToolContractCatalog.BrowserEvaluate,
        ToolContractCatalog.BrowserSnapshot,
        ToolContractCatalog.BrowserTakeScreenshot
    };

    private static readonly Regex VisibleBrowserDefectRegex = new(
        @"(?:\b(?:captured|displayed|exposed|found|observed|present|renders?|rendered|reported|showed|shown|visible|visibly)\b.{0,160}(?:#blazor-error-ui|\bblazor\s+error\s+ui\b|\ban\s+unhandled\s+error\s+has\s+occurred\b|\bunhandled\s+(?:application\s+|browser\s+|runtime\s+)?error\b|\b(?:application|browser|page|runtime|ui)\s+(?:crash|error|exception|failure)\b))|(?:(?:#blazor-error-ui|\bblazor\s+error\s+ui\b|\ban\s+unhandled\s+error\s+has\s+occurred\b|\bunhandled\s+(?:application\s+|browser\s+|runtime\s+)?error\b|\b(?:application|browser|page|runtime|ui)\s+(?:crash|error|exception|failure)\b).{0,160}\b(?:captured|displayed|exposed|found|observed|present|renders?|rendered|reported|showed|shown|visible|visibly)\b)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex NegatedBrowserDefectRegex = new(
        @"(?:\b(?:captured|displayed|found|observed|reported|showed|shown)\b.{0,40}\b(?:no|not|never|without)\b.{0,40}(?:#blazor-error-ui|\bblazor\s+error\s+ui\b|\ban\s+unhandled\s+error\s+has\s+occurred\b|\bunhandled\s+(?:application\s+|browser\s+|runtime\s+)?error\b|\b(?:application|browser|page|runtime|ui)\s+(?:crash|error|exception|failure)\b))|(?:\b(?:no|without)\s+(?:displayed\s+|present\s+|rendered\s+|shown\s+|visible\s+)?(?:#blazor-error-ui|\bblazor\s+error\s+ui\b|\ban\s+unhandled\s+error\s+has\s+occurred\b|\bunhandled\s+(?:application\s+|browser\s+|runtime\s+)?error\b|\b(?:application|browser|page|runtime|ui)\s+(?:crash|error|exception|failure)\b))|(?:\b(?:not|never)\s+(?:captured|displayed|found|observed|present|rendered|reported|showed|shown|visible)\b.{0,40}(?:#blazor-error-ui|\bblazor\s+error\s+ui\b|\ban\s+unhandled\s+error\s+has\s+occurred\b|\bunhandled\s+(?:application\s+|browser\s+|runtime\s+)?error\b|\b(?:application|browser|page|runtime|ui)\s+(?:crash|error|exception|failure)\b))|(?:(?:#blazor-error-ui|\bblazor\s+error\s+ui\b|\ban\s+unhandled\s+error\s+has\s+occurred\b|\bunhandled\s+(?:application\s+|browser\s+|runtime\s+)?error\b|\b(?:application|browser|page|runtime|ui)\s+(?:crash|error|exception|failure)\b).{0,40}\b(?:absent|hidden|no\s+longer\s+(?:displayed|present|rendered|shown|visible)|not\s+(?:displayed|present|rendered|shown|visible)|display\s*(?:is|was|=|:)\s*none)\b)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex BrowserClaimBoundaryRegex = new(
        @"(?:\r?\n)+|[.!?;]+\s*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string ContributionKey => "browser.observed-defect-evidence";

    public int Order => 110;

    public bool TryDescribeDefectEvidence(
        ProcessCompletionDefectEvidenceContext context,
        out string defectSummary)
    {
        ArgumentNullException.ThrowIfNull(context);

        var outcomeText = string.Join(
            Environment.NewLine,
            new[]
            {
                context.Output.Reason,
                context.Output.BranchOutcomeTitle,
                context.Output.HumanReadableSummaryMarkdown
            }
            .Concat(context.Output.NextActions)
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (!HasVisibleBrowserDefectClaim(outcomeText) ||
            context.CurrentExecutionRunId is not { } currentExecutionRunId)
        {
            defectSummary = string.Empty;
            return false;
        }

        var currentRunRoot = NormalizeManagedArtifactRef(BuildManagedArtifactRoot(context.Assignment));
        var currentRunEvidenceRefs = context.Output.EvidenceRefs
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeManagedArtifactRef)
            .Where(value => value.StartsWith(currentRunRoot + "/", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (currentRunEvidenceRefs.Count == 0)
        {
            defectSummary = string.Empty;
            return false;
        }

        var hasMatchingReceipt = (context.ToolReceipts ?? []).Any(receipt =>
            receipt.ExecutionRunId == currentExecutionRunId &&
            BrowserStateEvidenceToolNames.Contains(receipt.ToolName) &&
            IsSuccessfulReceipt(receipt.ExitSummary) &&
            currentRunEvidenceRefs.Any(evidenceRef =>
                ContainsExactManagedArtifactReference(
                    receipt.RequestSummary,
                    evidenceRef)));
        if (!hasMatchingReceipt)
        {
            defectSummary = string.Empty;
            return false;
        }

        defectSummary = "Current-execution browser state capture produced a cited artifact under the current process-run root for the visible runtime defect.";
        return true;
    }

    private static bool HasVisibleBrowserDefectClaim(string outcomeText)
        => BrowserClaimBoundaryRegex
            .Split(outcomeText)
            .Any(claim =>
                VisibleBrowserDefectRegex.IsMatch(claim) &&
                !NegatedBrowserDefectRegex.IsMatch(claim));

    private static bool ContainsExactManagedArtifactReference(
        string requestSummary,
        string evidenceRef)
    {
        if (string.IsNullOrWhiteSpace(requestSummary) ||
            string.IsNullOrWhiteSpace(evidenceRef))
        {
            return false;
        }

        var normalizedRequest = requestSummary.Replace('\\', '/');
        return Regex.IsMatch(
            normalizedRequest,
            $@"(?<![A-Za-z0-9._/-]){Regex.Escape(evidenceRef)}(?=$|[\s,;""'\)\]\}}])",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }
}
