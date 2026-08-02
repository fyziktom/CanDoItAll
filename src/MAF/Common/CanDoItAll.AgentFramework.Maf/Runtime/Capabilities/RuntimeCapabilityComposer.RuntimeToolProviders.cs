using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class RuntimeRegisteredToolProviderAttacher(IRuntimeToolProviderComposer runtimeToolProviderComposer)
{
    public async Task AttachAsync(
        RuntimeCapabilityComposition composition,
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements,
        AgentRuntimeContextPolicyKind policyKind,
        WorkspaceScopeDescriptor contextWorkspaceScope,
        AgentRuntimeContextIntent contextIntent,
        string runtimeSessionKey,
        IReadOnlyList<AgentChatContextAttachmentEnvelope>? contextAttachments)
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

        var effectiveContextIntent = contextIntent.WorkspaceScope is null
            ? contextIntent with { WorkspaceScope = contextWorkspaceScope }
            : contextIntent;
        var context = new AgentRuntimeToolProviderContext(
            agent,
            provider,
            capabilities,
            suppressApprovalRequirements,
            MapRuntimeToolProviderPurpose(policyKind),
            RuntimeSessionKey: runtimeSessionKey,
            effectiveContextIntent,
            ResolveRuntimeToolProviderTags(contextWorkspaceScope),
            contextAttachments);
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

    private static AgentRuntimeToolProviderPurpose MapRuntimeToolProviderPurpose(AgentRuntimeContextPolicyKind policyKind)
        => policyKind switch
        {
            AgentRuntimeContextPolicyKind.GovernedProcessAutomation => AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
            AgentRuntimeContextPolicyKind.AutoApprovedNonInteractive => AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive,
            AgentRuntimeContextPolicyKind.A2AEndpoint => AgentRuntimeToolProviderPurpose.A2AEndpoint,
            AgentRuntimeContextPolicyKind.InteractiveChat => AgentRuntimeToolProviderPurpose.InteractiveChat,
            _ => throw new ArgumentOutOfRangeException(
                nameof(policyKind),
                policyKind,
                "Runtime context policy kind is not supported.")
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
