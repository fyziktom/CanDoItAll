using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessToolReceiptPolicyCatalog
{
    private readonly IReadOnlyList<IProcessToolReceiptPolicyContribution> contributions;

    public ProcessToolReceiptPolicyCatalog(IEnumerable<IProcessToolReceiptPolicyContribution> contributions)
    {
        ArgumentNullException.ThrowIfNull(contributions);
        this.contributions = contributions.ToArray();
    }

    public bool IsProductMutationReceipt(ToolExecutionReceiptRecord receipt)
        => contributions.Any(contribution => contribution.IsProductMutationReceipt(receipt));

    public bool IsProductValidationTool(string toolName)
        => contributions.Any(contribution => contribution.IsProductValidationTool(toolName));

    public ProcessToolReceiptRequirementMatch MatchRequirement(
        ToolExecutionReceiptRecord receipt,
        string requirement)
    {
        var handledMatches = contributions
            .Select(contribution => contribution.MatchRequirement(receipt, requirement))
            .Where(match => match.IsHandled)
            .ToArray();
        return handledMatches.Length switch
        {
            0 => ProcessToolReceiptRequirementMatch.NotHandled,
            1 => handledMatches[0],
            _ => throw new InvalidOperationException(
                $"Multiple process tool receipt policies handled requirement '{requirement}'. Policy ownership must be unambiguous.")
        };
    }

    public IEnumerable<string> EnumerateRequirementSearchTerms(string requirement)
        => contributions
            .SelectMany(contribution => contribution.EnumerateRequirementSearchTerms(requirement))
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase);

}
