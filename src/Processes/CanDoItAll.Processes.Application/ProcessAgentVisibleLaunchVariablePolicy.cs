using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

internal static class ProcessAgentVisibleLaunchVariablePolicy
{
    private static readonly HashSet<string> InternalLaunchVariableKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "AgentId",
        "AgentName",
        "MachineName",
        "RepositoryRoot",
        "BranchName",
        "SessionId",
        ProcessRuntimeLaunchVariables.ProductCompletionRequiredPathsByStep,
        ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep,
        ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceiptsByStep,
        ProcessRuntimeLaunchVariables.ProductMutationRequiredBranchOutcomeKeys,
        ProcessRuntimeLaunchVariables.ProductMutationRequiredBranchOutcomeKeysByStep,
        ProcessRuntimeLaunchVariables.ProductMutationBeforeManagedOutputRequiredStepKeys,
        ProcessRuntimeLaunchVariables.ProductMutationToolNames,
        ProcessRuntimeLaunchVariables.RuntimeRoutedBranchOutcomeKeys,
        ProcessRuntimeLaunchVariables.RuntimeRoutedBranchOutcomeKeysByStep,
        ProcessRuntimeLaunchVariables.ExecutorPreferredSpecializationTags,
        ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredStepKeys,
        ProcessRuntimeLaunchVariables.ProductSourceInspectionRequiredBranchOutcomeKeysByStep,
        ProcessRuntimeLaunchVariables.ProductSourceInspectionExcludedPathFragmentsByStep,
        ProcessRuntimeLaunchVariables.CompletionIssueRoutes,
        ProcessRuntimeLaunchVariables.CompletionIssueRoutesByStep,
        ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix,
        ProcessRuntimeLaunchVariables.AcceptanceCriteriaAcceptedBranchOutcomeKeys,
        ProcessRuntimeLaunchVariables.ProcessStepRuntimeOwnedExecutorKey,
        ProcessRuntimeLaunchVariables.ProcessStepScopedLaunchVariablePrefixesByStep
    };

    internal static bool IsVisible(string key)
        => !InternalLaunchVariableKeys.Contains(key);
}
