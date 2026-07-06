using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;

namespace CanDoItAll.AgentFramework.Maf;

internal static class RuntimeToolProcessIntentPolicy
{
    public static bool IsToolCapabilityAllowedForProcessIntent(
        ToolCapabilityMetadata capability,
        AgentRuntimeContextIntent contextIntent)
    {
        if (capability.Classification == ToolInvocationClassification.Read)
        {
            return true;
        }

        return capability.OperationRequirementKind switch
        {
            ToolCapabilityOperationRequirementKind.None => capability.Classification == ToolInvocationClassification.Validation &&
                                                           HasAnyOperation(
                                                               contextIntent,
                                                               ProcessOperationContractNames.RunValidation,
                                                               ProcessOperationContractNames.LaunchRuntime,
                                                               ProcessOperationContractNames.CaptureRuntimeProof),
            ToolCapabilityOperationRequirementKind.Static => AllStaticRequirementsSatisfied(capability, contextIntent),
            ToolCapabilityOperationRequirementKind.WorkspaceFileMutation => IsWorkspaceFileMutationAllowedForProcessIntent(capability, contextIntent),
            ToolCapabilityOperationRequirementKind.WorkspaceScript => HasAnyOperation(
                contextIntent,
                ProcessOperationContractNames.ExecuteExternalAction,
                ProcessOperationContractNames.MutateProductTarget),
            ToolCapabilityOperationRequirementKind.DotNetRun => HasAnyOperation(
                contextIntent,
                ProcessOperationContractNames.RunValidation,
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.CaptureRuntimeProof),
            ToolCapabilityOperationRequirementKind.ProcessArtifactWrite => HasAnyOperation(
                contextIntent,
                ProcessOperationContractNames.WriteManagedProcessArtifacts,
                ProcessOperationContractNames.WriteExternalArtifactDestination,
                ProcessOperationContractNames.RecoverArtifactsOnly),
            _ => false
        };
    }

    public static bool HasAnyOperation(
        AgentRuntimeContextIntent contextIntent,
        params string[] operations)
    {
        return contextIntent.AllowedOperations.Any(operation =>
            operations.Contains(operation, StringComparer.OrdinalIgnoreCase));
    }

    private static bool AllStaticRequirementsSatisfied(
        ToolCapabilityMetadata capability,
        AgentRuntimeContextIntent contextIntent)
    {
        if (capability.OperationRequirements.Count == 0)
        {
            return false;
        }

        var allowedOperations = contextIntent.AllowedOperations.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return capability.OperationRequirements.All(requirement =>
            requirement.AnyOf.Count == 0 ||
            requirement.AnyOf.Any(allowedOperations.Contains));
    }

    private static bool IsWorkspaceFileMutationAllowedForProcessIntent(
        ToolCapabilityMetadata capability,
        AgentRuntimeContextIntent contextIntent)
    {
        if (contextIntent.AllowsProductMutation &&
            HasAnyOperation(contextIntent, ProcessOperationContractNames.MutateProductTarget))
        {
            return true;
        }

        return RuntimeToolCapabilityDescriptorFactory.IsWorkspaceManagedArtifactWriteTool(capability.Name) &&
               HasAnyOperation(
                   contextIntent,
                   ProcessOperationContractNames.WriteManagedProcessArtifacts,
                   ProcessOperationContractNames.WriteExternalArtifactDestination,
                   ProcessOperationContractNames.RecoverArtifactsOnly);
    }
}
