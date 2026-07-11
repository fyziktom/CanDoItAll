using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed class GenericWorkspaceToolReceiptPolicyContribution : IProcessToolReceiptPolicyContribution
{
    private static readonly HashSet<string> ProductMutationToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "workspace_write_file",
        "workspace_append_file",
        "workspace_copy_path",
        "workspace_move_path",
        "workspace_delete_path",
        "workspace_pwsh_run_script"
    };

    public bool IsProductMutationTool(string toolName)
        => ProductMutationToolNames.Contains(toolName);

    public bool IsProductValidationTool(string toolName)
        => false;

    public ProcessToolReceiptRequirementMatch MatchRequirement(
        ToolExecutionReceiptRecord receipt,
        string requirement)
        => ProcessToolReceiptRequirementMatch.NotHandled;

    public IEnumerable<string> EnumerateRequirementSearchTerms(string requirement)
        => [];

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
