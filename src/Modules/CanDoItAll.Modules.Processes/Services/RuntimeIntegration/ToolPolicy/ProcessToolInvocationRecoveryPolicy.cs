using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessToolInvocationRecoveryPolicy {
    private const string OwnPrimaryManagedOutputReadDenialMarker =
        "cannot read, stat, list, or search its own primary managed output";
    private const string OwnPrimaryManagedOutputPreCreationMarker = "before creating it";
    private const string OwnPrimaryManagedOutputInProgressWriteDenialMarker =
        "cannot write primary managed output";
    private const string OwnPrimaryManagedOutputBlockedPlaceholderWriteDenialMarker =
        "cannot write a status-only Blocked placeholder";
    internal const string ProductMutationBeforeManagedOutputDenialMarker =
        "before a successful current-execution product-target mutation";
    internal const string ProductMutationBranchOutcomeRequiredDenialMarker =
        "must declare exactly one valid Branch outcome key";
    private const string GovernedDotnetNewForceDeniedMarker =
        $"cannot run {ToolContractCatalog.WorkspaceDotNetNew} with force=true";

    private static readonly HashSet<string> RecoverableWorkspaceReadDiscoveryTools = new(StringComparer.OrdinalIgnoreCase) {
        ToolContractCatalog.WorkspaceListDirectory,
        ToolContractCatalog.WorkspaceListFiles,
        ToolContractCatalog.WorkspaceSearch,
        ToolContractCatalog.WorkspaceReadFile,
        ToolContractCatalog.WorkspaceStatPath,
        ToolContractCatalog.WorkspaceHashPath,
        ToolContractCatalog.WorkspaceDiffText
    };
    private static readonly HashSet<string> RecoverableGovernedBrowserProofTools = new(StringComparer.OrdinalIgnoreCase) {
        ToolContractCatalog.BrowserSnapshot,
        ToolContractCatalog.BrowserTakeScreenshot
    };
    private static readonly HashSet<string> RecoverableGovernedWorkspaceBoundaryTools = new(StringComparer.OrdinalIgnoreCase) {
        ToolContractCatalog.WorkspaceCreateDirectory,
        ToolContractCatalog.WorkspaceWriteFile,
        ToolContractCatalog.WorkspaceAppendFile,
        ToolContractCatalog.WorkspaceCopyPath,
        ToolContractCatalog.WorkspaceMovePath,
        ToolContractCatalog.WorkspaceDeletePath,
        ToolContractCatalog.WorkspaceDotNetNew,
        ToolContractCatalog.WorkspaceDotNetRestore,
        ToolContractCatalog.WorkspaceDotNetBuild,
        ToolContractCatalog.WorkspaceDotNetTest,
        ToolContractCatalog.WorkspaceDotNetRun,
        ToolContractCatalog.WorkspacePowerShellRunScript,
        ToolContractCatalog.WorkspacePythonRunFile,
        ToolContractCatalog.WorkspaceCommandRun
    };

    public static bool TryCreateRecoverableDeniedResult(
        string toolName,
        ToolInvocationPolicyDecision decision,
        ToolInvocationPolicyContext context,
        out string result) {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(context);

        result = string.Empty;
        if (decision.Kind is not ToolInvocationDecisionKind.Deny and not ToolInvocationDecisionKind.SkipExecution) {
            return false;
        }

        if (!IsRecoverableGovernedProcessStep(context)) {
            return false;
        }

        if (context.Classification == ToolInvocationClassification.Read &&
            RecoverableWorkspaceReadDiscoveryTools.Contains(toolName) &&
            IsRecoverableCurrentStepOwnOutputPreCreationReadDenial(decision)) {
            result = $"PolicyDenied: Tool '{toolName}' was denied for this governed process step. {decision.Reason} This is not a missing tool permission and not a blocker. Do not retry the read, stat, list, or search. Do not write a status-only InProgress or Blocked placeholder and stop. Continue the step's required product, validation, or external work from launch variables, upstream artifacts, project-structure context, or product readback. When recording the step outcome, create or overwrite the named primary managed artifact with workspace_write_file or workspace_append_file, then return submit_process_step_outcome with evidenceRefs containing that managed ref. Submit Blocked only if the artifact write is denied, or if a required tool is denied or fails on a concrete environment boundary.";
            return true;
        }

        if (context.Classification == ToolInvocationClassification.Mutation &&
            IsManagedOutputWriteTool(toolName) &&
            IsRecoverableProductMutationBeforeManagedOutputDenial(decision)) {
            result = decision.Reason.Contains(
                ProductMutationBranchOutcomeRequiredDenialMarker,
                StringComparison.OrdinalIgnoreCase)
                ? $"PolicyDenied: Tool '{toolName}' was denied for this governed process step. {decision.Reason} This is a branch-selection rule, not a missing tool permission and not a blocker. Do not retry the same artifact blindly. Select one declared branch outcome. If it requires a product mutation, mutate the grounded external target, read it back, and run focused proof before writing final evidence. If it is a proof-only branch, record its current-run proof and branch key before the final write."
                : $"PolicyDenied: Tool '{toolName}' was denied for this governed process step. {decision.Reason} This is an ordering rule, not a missing tool permission and not a blocker. Do not retry the primary managed artifact write yet. First mutate the grounded external-target product file with an allowed product-mutation tool, read the changed file back, and run the required focused proof. Only then create or overwrite the primary managed artifact with final evidence and return submit_process_step_outcome.";
            return true;
        }

        if (context.Classification == ToolInvocationClassification.Mutation &&
            IsManagedOutputWriteTool(toolName) &&
            IsRecoverableCurrentStepOwnOutputPlaceholderWriteDenial(decision)) {
            result = $"PolicyDenied: Tool '{toolName}' was denied for this governed process step. {decision.Reason} This is not a missing tool permission and not a blocker. Do not retry the placeholder write. Continue the step's required product, validation, or external work from launch variables, upstream artifacts, project-structure context, or product readback. When the work is complete, create or overwrite the primary managed artifact with final evidence and Status: Completed, Failed, Blocked, WaitingApproval, or Refused, then return submit_process_step_outcome with matching evidenceRefs.";
            return true;
        }

        if (context.Classification == ToolInvocationClassification.Read &&
            RecoverableWorkspaceReadDiscoveryTools.Contains(toolName)) {
            result = $"PolicyDenied: Tool '{toolName}' was denied for this governed process step. {decision.Reason} Use the grounded external-target alias or current-run artifact folder named in the tool boundary, then retry with narrower arguments. When the denial gives a replacement external-target alias, retry the same structured workspace tool with that alias before finalizing Blocked. If this was only an optional context probe for an evidence-producing step, continue from launch variables or project-structure context and create the managed artifact instead of blocking.";
            return true;
        }

        if (IsRecoverableScriptInspectionDenial(toolName, decision)) {
            result = $"PolicyDenied: Tool '{toolName}' was denied for this governed process step. {decision.Reason} Treat this as helper-script ordering, not as missing permission. Create or overwrite the current-run helper script with workspace_write_file, verify that exact helper path with workspace_stat_path or workspace_read_file, then retry {toolName} with the same helper path. Do not submit Blocked only because a pre-creation script invocation was denied; submit Blocked only if the verified retry is denied or fails on a concrete policy, permission, or environment boundary.";
            return true;
        }

        if (IsRecoverableDotnetNewForceDenial(toolName, decision)) {
            result = $"PolicyDenied: Tool '{toolName}' was denied for this governed process step. {decision.Reason} Treat this as an unsafe scaffold overwrite request, not as missing permission. Retry without force only when the target scaffold is absent; when files already exist, inspect them and repair precise drift with governed product-mutation tools or a reviewed ProductMutation helper script.";
            return true;
        }

        if (RecoverableGovernedWorkspaceBoundaryTools.Contains(toolName) &&
            IsRecoverableWorkspaceBoundaryDenial(decision)) {
            result = $"PolicyDenied: Tool '{toolName}' was denied for this governed process step. {decision.Reason} Treat this as a wrong tool argument, not as missing permission. Retry with the grounded current-run external-target alias or current-run artifact path named in the denial before finalizing Blocked.";
            return true;
        }

        if (IsRecoverableGovernedBrowserProofBoundsDenial(toolName, decision, context)) {
            result = $"PolicyDenied: Tool '{toolName}' was denied by governed browser proof bounds. {decision.Reason} Retry once with the bounded browser-proof arguments named in this denial. Do not report the process blocked until that bounded retry fails.";
            return true;
        }

        return false;
    }

    private static bool IsRecoverableCurrentStepOwnOutputPreCreationReadDenial(
        ToolInvocationPolicyDecision decision) {
        return decision.Reason.Contains(OwnPrimaryManagedOutputReadDenialMarker, StringComparison.OrdinalIgnoreCase) &&
               decision.Reason.Contains(OwnPrimaryManagedOutputPreCreationMarker, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecoverableCurrentStepOwnOutputPlaceholderWriteDenial(
        ToolInvocationPolicyDecision decision) {
        return decision.Reason.Contains(OwnPrimaryManagedOutputInProgressWriteDenialMarker, StringComparison.OrdinalIgnoreCase) ||
               decision.Reason.Contains(OwnPrimaryManagedOutputBlockedPlaceholderWriteDenialMarker, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecoverableProductMutationBeforeManagedOutputDenial(
        ToolInvocationPolicyDecision decision)
        => decision.Reason.Contains(
            ProductMutationBeforeManagedOutputDenialMarker,
            StringComparison.OrdinalIgnoreCase) ||
           decision.Reason.Contains(
               ProductMutationBranchOutcomeRequiredDenialMarker,
               StringComparison.OrdinalIgnoreCase);

    private static bool IsManagedOutputWriteTool(string toolName) {
        return string.Equals(toolName, ToolContractCatalog.WorkspaceWriteFile, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, ToolContractCatalog.WorkspaceAppendFile, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecoverableScriptInspectionDenial(
        string toolName,
        ToolInvocationPolicyDecision decision) {
        if (!string.Equals(toolName, ToolContractCatalog.WorkspacePowerShellRunScript, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(toolName, ToolContractCatalog.WorkspacePythonRunFile, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        return decision.Reason.Contains("could not be inspected", StringComparison.OrdinalIgnoreCase) ||
               decision.Reason.Contains("must be inspected", StringComparison.OrdinalIgnoreCase) ||
               (decision.Reason.Contains("script path", StringComparison.OrdinalIgnoreCase) &&
                decision.Reason.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsRecoverableDotnetNewForceDenial(
        string toolName,
        ToolInvocationPolicyDecision decision) {
        return string.Equals(toolName, ToolContractCatalog.WorkspaceDotNetNew, StringComparison.OrdinalIgnoreCase) &&
               decision.Reason.Contains(GovernedDotnetNewForceDeniedMarker, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsRecoverableGovernedProcessStep(ToolInvocationPolicyContext context) {
        return string.Equals(context.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(context.ProcessRunId) &&
               !string.IsNullOrWhiteSpace(context.ProcessStepId);
    }

    private static bool IsRecoverableWorkspaceBoundaryDenial(ToolInvocationPolicyDecision decision) {
        return decision.Reason.Contains("current-run", StringComparison.OrdinalIgnoreCase) ||
               decision.Reason.Contains("workspace boundary", StringComparison.OrdinalIgnoreCase) ||
               decision.Reason.Contains("outside the current run boundary", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecoverableGovernedBrowserProofBoundsDenial(
        string toolName,
        ToolInvocationPolicyDecision decision,
        ToolInvocationPolicyContext context) {
        if (!RecoverableGovernedBrowserProofTools.Contains(toolName) ||
            context.Classification is not (ToolInvocationClassification.Read or ToolInvocationClassification.Validation) ||
            !ProcessStepAllowsOperation(context, ProcessOperationContractNames.CaptureRuntimeProof)) {
            return false;
        }

        return decision.Reason.StartsWith("Governed process browser snapshots", StringComparison.Ordinal) ||
               decision.Reason.StartsWith("Governed process browser screenshots", StringComparison.Ordinal);
    }

    private static bool ProcessStepAllowsOperation(ToolInvocationPolicyContext context, string operationName) {
        return context.ProcessStepAllowedOperations?.Contains(operationName, StringComparer.OrdinalIgnoreCase) == true;
    }

}
