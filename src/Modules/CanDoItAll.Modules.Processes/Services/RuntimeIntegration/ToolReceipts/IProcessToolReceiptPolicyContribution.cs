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

internal sealed record ProcessScriptHelperDescriptor(
    string ScriptVariableName,
    string ScriptRefVariableName,
    string ManifestVariableName);

internal interface IProcessToolReceiptPolicyContribution
{
    bool IsProductMutationTool(string toolName);

    bool IsProductValidationTool(string toolName);

    ProcessToolReceiptRequirementMatch MatchRequirement(
        ToolExecutionReceiptRecord receipt,
        string requirement);

    IEnumerable<string> EnumerateRequirementSearchTerms(string requirement);

    bool TryResolveScriptHelper(
        ProcessRuntimeStepAssignment assignment,
        out ProcessScriptHelperDescriptor descriptor);

    bool AllowsCompletedOutcomeWithDeclaredBlockers(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output);
}
