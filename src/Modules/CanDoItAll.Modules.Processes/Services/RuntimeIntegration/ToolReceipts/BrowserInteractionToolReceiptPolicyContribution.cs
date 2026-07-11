using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed class BrowserInteractionToolReceiptPolicyContribution : IProcessToolReceiptPolicyContribution
{
    internal const string InteractionProofRequirement = "browser interaction proof";

    private static readonly HashSet<string> InteractionToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ToolContractCatalog.BrowserClick,
        ToolContractCatalog.BrowserFillForm,
        ToolContractCatalog.BrowserSelectOption,
        ToolContractCatalog.BrowserPressKey,
        ToolContractCatalog.BrowserType,
        ToolContractCatalog.BrowserDrag,
        ToolContractCatalog.BrowserWaitFor
    };

    public bool IsProductMutationTool(string toolName)
        => false;

    public bool IsProductValidationTool(string toolName)
        => false;

    public ProcessToolReceiptRequirementMatch MatchRequirement(
        ToolExecutionReceiptRecord receipt,
        string requirement)
    {
        if (!string.Equals(requirement, InteractionProofRequirement, StringComparison.OrdinalIgnoreCase))
        {
            return ProcessToolReceiptRequirementMatch.NotHandled;
        }

        return InteractionToolNames.Contains(receipt.ToolName)
            ? ProcessToolReceiptRequirementMatch.Matched
            : ProcessToolReceiptRequirementMatch.NotMatched;
    }

    public IEnumerable<string> EnumerateRequirementSearchTerms(string requirement)
        => string.Equals(requirement, InteractionProofRequirement, StringComparison.OrdinalIgnoreCase)
            ? InteractionToolNames
            : [];

    public bool TryResolveScriptHelper(
        ProcessRuntimeStepAssignment assignment,
        out ProcessScriptHelperDescriptor descriptor)
    {
        descriptor = null!;
        return false;
    }

    public bool AllowsCompletedOutcomeWithDeclaredBlockers(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
        => false;
}
