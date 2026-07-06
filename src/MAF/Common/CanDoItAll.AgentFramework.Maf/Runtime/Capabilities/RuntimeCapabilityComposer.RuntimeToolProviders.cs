using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed partial class RuntimeCapabilityComposer
{
    private async Task AttachRegisteredRuntimeToolProvidersAsync(
        RuntimeCapabilityComposition composition,
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements,
        WorkspaceScopeDescriptor contextWorkspaceScope,
        AgentRuntimeContextIntent contextIntent)
    {
        if (!contextIntent.RuntimeToolProvidersEnabled)
        {
            composition.State.ContextSources.Add(AgentRuntimeContextManifestSource.Excluded(
                AgentRuntimeContextSourceCategories.RuntimeToolProvider,
                "registered-runtime-tool-providers",
                "registered runtime tool providers disabled by execution context"));
            return;
        }

        if (!agent.Permissions.CanUseTools ||
            composition.RuntimeToolProviders.Count == 0)
        {
            return;
        }

        var context = new AgentRuntimeToolProviderContext(
            agent,
            provider,
            capabilities,
            suppressApprovalRequirements,
            MapRuntimeToolProviderPurpose(ResolveContextPolicyKind(agent, suppressApprovalRequirements)),
            RuntimeSessionKey: string.Empty,
            contextIntent,
            ResolveRuntimeToolProviderTags(contextWorkspaceScope));
        var result = await runtimeToolProviderComposer.AttachAsync(
            new RuntimeToolProviderAttachmentRequest(
                composition.State,
                composition.CapabilityAccessPlan,
                composition.RuntimeToolProviders,
                context,
                suppressApprovalRequirements),
            cancellationToken);
        await progressCallback(
            ExecutionState.Preparing,
            "Runtime tool providers",
            result.ProgressMessage);
    }

    private static bool IsToolCapabilityAllowedForProcessIntent(
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

        return IsWorkspaceTextWriteTool(capability.Name) &&
               HasAnyOperation(
                   contextIntent,
                   ProcessOperationContractNames.WriteManagedProcessArtifacts,
                   ProcessOperationContractNames.WriteExternalArtifactDestination,
                   ProcessOperationContractNames.RecoverArtifactsOnly);
    }

    private static bool HasAnyOperation(
        AgentRuntimeContextIntent contextIntent,
        params string[] operations)
    {
        return contextIntent.AllowedOperations.Any(operation =>
            operations.Contains(operation, StringComparer.OrdinalIgnoreCase));
    }

    private static AgentRuntimeToolProviderPurpose MapRuntimeToolProviderPurpose(AgentRuntimeContextPolicyKind policyKind)
        => policyKind switch
        {
            AgentRuntimeContextPolicyKind.GovernedProcessAutomation => AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
            AgentRuntimeContextPolicyKind.AutoApprovedNonInteractive => AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive,
            AgentRuntimeContextPolicyKind.A2AEndpoint => AgentRuntimeToolProviderPurpose.A2AEndpoint,
            AgentRuntimeContextPolicyKind.InteractiveChat => AgentRuntimeToolProviderPurpose.InteractiveChat,
            _ => AgentRuntimeToolProviderPurpose.InteractiveChat
        };

    private static IReadOnlyDictionary<string, string> ResolveRuntimeToolProviderTags(
        WorkspaceScopeDescriptor contextWorkspaceScope)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["workspaceScopeKind"] = contextWorkspaceScope.Kind.ToString()
        };
        if (!string.IsNullOrWhiteSpace(contextWorkspaceScope.Key))
        {
            tags["workspaceScopeKey"] = contextWorkspaceScope.Key;
        }

        return tags;
    }
}
