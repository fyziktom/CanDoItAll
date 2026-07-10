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

    public bool IsProductMutationTool(string toolName)
        => contributions.Any(contribution => contribution.IsProductMutationTool(toolName));

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

    public bool TryResolveScriptHelper(
        ProcessRuntimeStepAssignment assignment,
        out ProcessScriptHelperDescriptor descriptor)
    {
        var matches = contributions
            .Select(contribution => contribution.TryResolveScriptHelper(assignment, out var candidate)
                ? candidate
                : null)
            .Where(candidate => candidate is not null)
            .ToArray();
        switch (matches.Length)
        {
            case 0:
                descriptor = null!;
                return false;

            case 1:
                descriptor = matches[0]!;
                return true;

            default:
                throw new InvalidOperationException(
                    $"Multiple process tool receipt policies supplied script-helper guidance for step '{assignment.StepKey}'. Policy ownership must be unambiguous.");
        }
    }

    public bool AllowsCompletedOutcomeWithDeclaredBlockers(ProcessRuntimeStepAssignment assignment)
        => contributions.Any(contribution =>
            contribution.AllowsCompletedOutcomeWithDeclaredBlockers(assignment));
}
