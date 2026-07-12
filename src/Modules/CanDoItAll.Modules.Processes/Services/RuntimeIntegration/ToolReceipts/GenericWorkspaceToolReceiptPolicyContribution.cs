using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed class GenericWorkspaceToolReceiptPolicyContribution : IProcessToolReceiptPolicyContribution
{
    private static readonly HashSet<string> ProductMutationToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ToolContractCatalog.WorkspaceWriteFile,
        ToolContractCatalog.WorkspaceAppendFile,
        ToolContractCatalog.WorkspaceCopyPath,
        ToolContractCatalog.WorkspaceMovePath,
        ToolContractCatalog.WorkspaceDeletePath
    };

    private static readonly HashSet<string> ScriptExecutionToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ToolContractCatalog.WorkspacePowerShellRunScript,
        ToolContractCatalog.WorkspacePythonRunFile
    };

    public bool IsProductMutationReceipt(ToolExecutionReceiptRecord receipt)
        => ProductMutationToolNames.Contains(receipt.ToolName) ||
           (ScriptExecutionToolNames.Contains(receipt.ToolName) &&
            receipt.DeclaredSideEffectMode == ToolExecutionSideEffectMode.ProductMutation);

    public bool IsProductValidationTool(string toolName)
        => false;

    public ProcessToolReceiptRequirementMatch MatchRequirement(
        ToolExecutionReceiptRecord receipt,
        string requirement)
        => ProcessToolReceiptRequirementMatch.NotHandled;

    public IEnumerable<string> EnumerateRequirementSearchTerms(string requirement)
        => [];
}
