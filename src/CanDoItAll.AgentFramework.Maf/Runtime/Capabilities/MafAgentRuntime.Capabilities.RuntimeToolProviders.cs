using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private async Task AttachRegisteredRuntimeToolProvidersAsync(
        RuntimeCapabilityComposition composition,
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements,
        WorkspaceScopeDescriptor contextWorkspaceScope)
    {
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
            ResolveRuntimeToolProviderTags(contextWorkspaceScope));
        var attachedToolCount = 0;
        var attachmentSummaries = new List<RuntimeToolProviderAttachmentSummary>();

        foreach (var registration in composition.RuntimeToolProviders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var toolProvider = registration.Provider;
            IReadOnlyList<AITool> providerTools;
            try
            {
                providerTools = await toolProvider.CreateToolsAsync(context, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"Runtime tool provider '{DescribeRuntimeToolProvider(toolProvider)}' failed to create tools. {exception.Message}",
                    exception);
            }

            if (providerTools is null)
            {
                throw new InvalidOperationException(
                    $"Runtime tool provider '{DescribeRuntimeToolProvider(toolProvider)}' returned a null tool list.");
            }

            var tools = providerTools
                .Select(tool => WrapRuntimeProviderToolForApproval(tool, suppressApprovalRequirements))
                .ToList();
            EnsureRuntimeToolProviderNamesAreValid(toolProvider, tools);
            EnsureRuntimeToolProviderDoesNotDuplicateExistingTools(toolProvider, composition.State.Tools, tools);
            var toolMetadata = ResolveRuntimeToolMetadata(registration, context, tools);

            composition.State.RuntimeToolProviderDescriptors.Add(registration.Descriptor);
            composition.State.RuntimeToolMetadata.AddRange(toolMetadata);
            composition.State.Tools.AddRange(tools);
            attachedToolCount += tools.Count;
            attachmentSummaries.Add(new RuntimeToolProviderAttachmentSummary(
                registration.Descriptor.ProviderKey,
                registration.Descriptor.DisplayName,
                tools.Count));
        }

        composition.State.HasApprovalTools |= composition.State.Tools.Any(tool => tool is ApprovalRequiredAIFunction);
        await progressCallback(
            ExecutionState.Preparing,
            "Runtime tool providers",
            BuildRuntimeToolProviderAttachmentMessage(
                attachedToolCount,
                composition.RuntimeToolProviders.Count,
                attachmentSummaries));
    }

    private static AITool WrapRuntimeProviderToolForApproval(
        AITool tool,
        bool suppressApprovalRequirements)
    {
        if (suppressApprovalRequirements ||
            tool is not AIFunction function ||
            !AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(tool.Name))
        {
            return tool;
        }

        return new ApprovalRequiredAIFunction(function);
    }

    private static void EnsureRuntimeToolProviderNamesAreValid(
        IAgentRuntimeToolProvider toolProvider,
        IReadOnlyList<AITool> tools)
    {
        var unnamedToolCount = tools.Count(tool => string.IsNullOrWhiteSpace(tool.Name));
        if (unnamedToolCount > 0)
        {
            throw new InvalidOperationException(
                $"Runtime tool provider '{DescribeRuntimeToolProvider(toolProvider)}' returned {unnamedToolCount} tool(s) without a name.");
        }

        var duplicateNames = tools
            .GroupBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (duplicateNames.Count > 0)
        {
            throw new InvalidOperationException(
                $"Runtime tool provider '{DescribeRuntimeToolProvider(toolProvider)}' returned duplicate tool name(s): {string.Join(", ", duplicateNames)}.");
        }
    }

    private static void EnsureRuntimeToolProviderKeysAreUnique(
        IReadOnlyList<RuntimeToolProviderRegistration> registrations)
    {
        var duplicateKeys = registrations
            .GroupBy(registration => registration.Descriptor.ProviderKey, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (duplicateKeys.Count > 0)
        {
            throw new InvalidOperationException(
                $"Runtime tool provider key(s) must be unique: {string.Join(", ", duplicateKeys)}.");
        }
    }

    private static void EnsureRuntimeToolProviderDoesNotDuplicateExistingTools(
        IAgentRuntimeToolProvider toolProvider,
        IReadOnlyList<AITool> existingTools,
        IReadOnlyList<AITool> providerTools)
    {
        var duplicateNames = providerTools
            .Select(tool => tool.Name)
            .Where(toolName => existingTools.Any(existingTool => string.Equals(existingTool.Name, toolName, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (duplicateNames.Count > 0)
        {
            throw new InvalidOperationException(
                $"Runtime tool provider '{DescribeRuntimeToolProvider(toolProvider)}' returned tool name(s) already registered by the runtime: {string.Join(", ", duplicateNames)}.");
        }
    }

    private static RuntimeToolProviderRegistration CreateRuntimeToolProviderRegistration(
        IAgentRuntimeToolProvider provider,
        int index)
    {
        var descriptor = provider.Descriptor ?? new AgentRuntimeToolProviderDescriptor(
            $"legacy:{provider.GetType().FullName ?? provider.GetType().Name}:{index}",
            provider.GetType().Name,
            "Legacy runtime tool provider without explicit descriptor metadata.",
            supportedPurposes: Enum.GetValues<AgentRuntimeToolProviderPurpose>());

        return new RuntimeToolProviderRegistration(provider, descriptor);
    }

    private static IReadOnlyList<AgentRuntimeToolMetadata> ResolveRuntimeToolMetadata(
        RuntimeToolProviderRegistration registration,
        AgentRuntimeToolProviderContext context,
        IReadOnlyList<AITool> tools)
    {
        var declaredMetadata = registration.Provider.GetToolMetadata(context);
        if (declaredMetadata is null)
        {
            throw new InvalidOperationException(
                $"Runtime tool provider '{DescribeRuntimeToolProvider(registration.Provider)}' returned a null tool metadata list.");
        }

        var duplicateMetadataNames = declaredMetadata
            .GroupBy(metadata => metadata.ToolName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (duplicateMetadataNames.Count > 0)
        {
            throw new InvalidOperationException(
                $"Runtime tool provider '{DescribeRuntimeToolProvider(registration.Provider)}' returned duplicate metadata for tool name(s): {string.Join(", ", duplicateMetadataNames)}.");
        }

        var toolNames = tools
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownMetadataNames = declaredMetadata
            .Select(metadata => metadata.ToolName)
            .Where(toolName => !toolNames.Contains(toolName))
            .OrderBy(toolName => toolName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (unknownMetadataNames.Count > 0)
        {
            throw new InvalidOperationException(
                $"Runtime tool provider '{DescribeRuntimeToolProvider(registration.Provider)}' declared metadata for unknown tool name(s): {string.Join(", ", unknownMetadataNames)}.");
        }

        var metadataByToolName = declaredMetadata.ToDictionary(
            metadata => metadata.ToolName,
            StringComparer.OrdinalIgnoreCase);

        return tools
            .Select(tool => metadataByToolName.TryGetValue(tool.Name, out var metadata)
                ? metadata
                : new AgentRuntimeToolMetadata(
                    registration.Descriptor.ProviderKey,
                    tool.Name,
                    MapRuntimeToolOperationKind(AgentToolInvocationPolicyMetadata.Classify(tool.Name)),
                    AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(tool.Name),
                    registration.Descriptor.DomainTags))
            .ToList();
    }

    private static AgentRuntimeToolOperationKind MapRuntimeToolOperationKind(
        ToolInvocationClassification classification)
        => classification switch
        {
            ToolInvocationClassification.Read => AgentRuntimeToolOperationKind.Read,
            ToolInvocationClassification.Mutation => AgentRuntimeToolOperationKind.Mutation,
            ToolInvocationClassification.Validation => AgentRuntimeToolOperationKind.Validation,
            ToolInvocationClassification.HostedProviderNative => AgentRuntimeToolOperationKind.HostedProviderNative,
            ToolInvocationClassification.LocalMcp => AgentRuntimeToolOperationKind.LocalMcp,
            ToolInvocationClassification.HostedMcp => AgentRuntimeToolOperationKind.HostedMcp,
            _ => AgentRuntimeToolOperationKind.Unknown
        };

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

    private static string DescribeRuntimeToolProvider(IAgentRuntimeToolProvider toolProvider)
    {
        return toolProvider.GetType().FullName ?? toolProvider.GetType().Name;
    }

    private static string BuildRuntimeToolProviderAttachmentMessage(
        int attachedToolCount,
        int registeredProviderCount,
        IReadOnlyList<RuntimeToolProviderAttachmentSummary> attachmentSummaries)
    {
        var message = $"Attached {attachedToolCount} tool(s) from {registeredProviderCount} registered runtime tool provider(s).";
        if (attachmentSummaries.Count == 0)
        {
            return message;
        }

        var providers = string.Join(
            "; ",
            attachmentSummaries.Select(summary =>
                $"{summary.ProviderKey} ({summary.ProviderName}, {summary.ToolCount} tool(s))"));
        return $"{message} Providers: {providers}.";
    }

    private sealed record RuntimeToolProviderAttachmentSummary(
        string ProviderKey,
        string ProviderName,
        int ToolCount);
}
