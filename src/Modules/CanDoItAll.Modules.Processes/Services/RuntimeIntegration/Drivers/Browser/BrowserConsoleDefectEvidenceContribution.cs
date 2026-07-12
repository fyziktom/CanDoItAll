using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

using static CanDoItAll.Modules.Processes.ProcessOutcomeGroundingValidator;
using static CanDoItAll.Modules.Processes.ProcessManagedArtifactEvidence;

namespace CanDoItAll.Modules.Processes;

internal sealed class BrowserConsoleDefectEvidenceContribution : IProcessCompletionDefectEvidenceContribution
{
    private const string BrowserConsoleMessagesToolName = "browser_console_messages";

    private static readonly Regex BrowserConsoleDefectRegex = new(
        @"\b(?:[1-9]\d*|an?|one|some|multiple|with|reported)\s+console\s+errors?\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public string ContributionKey => "browser.console-defect-evidence";

    public int Order => 100;

    public bool TryDescribeDefectEvidence(
        ProcessCompletionDefectEvidenceContext context,
        out string defectSummary)
    {
        ArgumentNullException.ThrowIfNull(context);

        var outcomeText = string.Join(
            " ",
            new[]
            {
                context.Output.Reason,
                context.Output.BranchOutcomeTitle,
                context.Output.HumanReadableSummaryMarkdown
            }
            .Concat(context.Output.NextActions)
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (!BrowserConsoleDefectRegex.IsMatch(outcomeText))
        {
            defectSummary = string.Empty;
            return false;
        }

        if (context.CurrentExecutionRunId is not { } currentExecutionRunId)
        {
            defectSummary = string.Empty;
            return false;
        }

        var currentRunRoot = $"process-runs/{context.Assignment.RunId.Value:D}";
        var receipt = (context.ToolReceipts ?? [])
            .Where(candidate =>
                string.Equals(candidate.ToolName, BrowserConsoleMessagesToolName, StringComparison.OrdinalIgnoreCase) &&
                candidate.ExecutionRunId == currentExecutionRunId &&
                IsSuccessfulReceipt(candidate.ExitSummary) &&
                NormalizeOutcomeReferenceText(candidate.RequestSummary)
                    .Contains(currentRunRoot, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.CompletedAtUtc)
            .FirstOrDefault();
        if (receipt is null)
        {
            defectSummary = string.Empty;
            return false;
        }

        defectSummary = "Current-run browser console collection reported one or more console errors on the validated product route.";
        return true;
    }

}
