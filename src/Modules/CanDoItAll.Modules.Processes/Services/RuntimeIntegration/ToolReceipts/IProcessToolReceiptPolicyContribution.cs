using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal readonly record struct ProcessToolReceiptRequirementMatch(bool IsHandled, bool IsMatch)
{
    public static ProcessToolReceiptRequirementMatch NotHandled { get; } = new(false, false);

    public static ProcessToolReceiptRequirementMatch Matched { get; } = new(true, true);

    public static ProcessToolReceiptRequirementMatch NotMatched { get; } = new(true, false);
}

internal interface IProcessToolReceiptPolicyContribution
{
    bool IsProductMutationReceipt(ToolExecutionReceiptRecord receipt);

    bool IsProductValidationTool(string toolName);

    ProcessToolReceiptRequirementMatch MatchRequirement(
        ToolExecutionReceiptRecord receipt,
        string requirement);

    IEnumerable<string> EnumerateRequirementSearchTerms(string requirement);
}
