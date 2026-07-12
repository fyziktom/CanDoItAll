using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessCompletionDefectEvidenceContext(
    ProcessRuntimeStepAssignment Assignment,
    ProcessStepOutcomeResult Output,
    IReadOnlyList<ToolExecutionReceiptRecord>? ToolReceipts,
    ProcessCompletionIssue? Issue,
    Guid? CurrentExecutionRunId);

internal interface IProcessCompletionDefectEvidenceContribution
{
    string ContributionKey { get; }

    int Order { get; }

    bool TryDescribeDefectEvidence(
        ProcessCompletionDefectEvidenceContext context,
        out string defectSummary);
}

internal sealed class ProcessCompletionDefectEvidenceCatalog(
    IEnumerable<IProcessCompletionDefectEvidenceContribution> contributions)
{
    public static ProcessCompletionDefectEvidenceCatalog Empty { get; } = new([]);

    private readonly IReadOnlyList<IProcessCompletionDefectEvidenceContribution> contributions =
        CreateContributions(contributions);

    internal bool TryDescribeDefectEvidence(
        ProcessCompletionDefectEvidenceContext context,
        out string defectSummary)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var contribution in contributions)
        {
            if (!contribution.TryDescribeDefectEvidence(context, out defectSummary))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(defectSummary))
            {
                throw new InvalidOperationException(
                    $"Process completion defect evidence contribution '{contribution.ContributionKey}' returned success without a defect summary.");
            }

            return true;
        }

        defectSummary = string.Empty;
        return false;
    }

    private static IReadOnlyList<IProcessCompletionDefectEvidenceContribution> CreateContributions(
        IEnumerable<IProcessCompletionDefectEvidenceContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);

        var ordered = contributions
            .OrderBy(contribution => contribution.Order)
            .ThenBy(contribution => contribution.ContributionKey, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(contribution => string.IsNullOrWhiteSpace(contribution.ContributionKey)))
        {
            throw new InvalidOperationException(
                "A process completion defect evidence contribution must declare a stable contribution key.");
        }

        var duplicate = ordered
            .GroupBy(contribution => contribution.ContributionKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate process completion defect evidence contribution key '{duplicate.Key}' is registered.");
        }

        return ordered;
    }
}
