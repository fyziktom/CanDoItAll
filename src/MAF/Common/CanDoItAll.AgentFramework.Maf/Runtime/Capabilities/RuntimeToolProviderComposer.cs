using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal interface IRuntimeToolProviderComposer
{
    IReadOnlyList<RuntimeToolProviderRegistration> ComposeRegistrations(
        IEnumerable<IAgentRuntimeToolProvider> providers);

    Task<RuntimeToolProviderAttachmentResult> AttachAsync(
        RuntimeToolProviderAttachmentRequest request,
        CancellationToken cancellationToken);
}

internal sealed class RuntimeToolProviderComposer(
    IRuntimeToolProviderAccessFilter accessFilter) : IRuntimeToolProviderComposer
{
    public static IRuntimeToolProviderComposer Default { get; } = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter());

    public IReadOnlyList<RuntimeToolProviderRegistration> ComposeRegistrations(
        IEnumerable<IAgentRuntimeToolProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var registrations = providers
            .Select(CreateRuntimeToolProviderRegistration)
            .OrderBy(registration => registration.Provider.Order)
            .ThenBy(registration => registration.Descriptor.ProviderKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(registration => registration.Provider.GetType().FullName, StringComparer.Ordinal)
            .ToList();
        EnsureRuntimeToolProviderKeysAreUnique(registrations);
        return registrations;
    }

    public async Task<RuntimeToolProviderAttachmentResult> AttachAsync(
        RuntimeToolProviderAttachmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var attachedToolCount = 0;
        var attachmentSummaries = new List<RuntimeToolProviderAttachmentSummary>();

        foreach (var registration in request.RuntimeToolProviders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var toolProvider = registration.Provider;
            IReadOnlyList<AITool> providerTools;
            try
            {
                providerTools = await toolProvider.CreateToolsAsync(request.Context, cancellationToken);
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

            EnsureRuntimeToolProviderNamesAreValid(toolProvider, providerTools);
            var allToolMetadata = ResolveRuntimeToolMetadata(registration, request.Context, providerTools);
            var filtered = accessFilter.Filter(new RuntimeToolProviderAccessFilterRequest(
                registration,
                request.State,
                request.AccessPlan,
                providerTools,
                allToolMetadata));
            var tools = filtered.Tools
                .Select(tool => WrapRuntimeProviderToolForApproval(tool, request.SuppressApprovalRequirements))
                .ToList();
            EnsureRuntimeToolProviderDoesNotDuplicateExistingTools(toolProvider, request.State.Tools, tools);
            var toolMetadata = filtered.Metadata;
            if (tools.Count == 0)
            {
                request.State.ContextSources.Add(AgentRuntimeContextManifestSource.Excluded(
                    AgentRuntimeContextSourceCategories.RuntimeToolProvider,
                    registration.Descriptor.ProviderKey,
                    filtered.ExcludedToolCount > 0
                        ? $"registered runtime tool provider pruned {filtered.ExcludedToolCount} tool(s) that are outside this governed process step operation contract"
                        : "registered runtime tool provider returned no tools for this run"));
                attachmentSummaries.Add(new RuntimeToolProviderAttachmentSummary(
                    registration.Descriptor.ProviderKey,
                    registration.Descriptor.DisplayName,
                    ToolCount: 0,
                    filtered.ExcludedToolCount));
                continue;
            }

            request.State.RuntimeToolProviderDescriptors.Add(registration.Descriptor);
            request.State.RuntimeToolMetadata.AddRange(toolMetadata);
            request.State.Tools.AddRange(tools);
            request.State.ContextSources.Add(AgentRuntimeContextManifestSource.Included(
                AgentRuntimeContextSourceCategories.RuntimeToolProvider,
                registration.Descriptor.ProviderKey,
                "registered runtime tool provider selected for this run",
                tools.Count,
                MafContextManifestBuilder.EstimateToolSchemaChars(tools)));
            attachedToolCount += tools.Count;
            attachmentSummaries.Add(new RuntimeToolProviderAttachmentSummary(
                registration.Descriptor.ProviderKey,
                registration.Descriptor.DisplayName,
                tools.Count,
                filtered.ExcludedToolCount));
        }

        request.State.HasApprovalTools |= request.State.Tools.Any(tool => tool is ApprovalRequiredAIFunction);
        return new RuntimeToolProviderAttachmentResult(
            attachedToolCount,
            request.RuntimeToolProviders.Count,
            attachmentSummaries,
            BuildRuntimeToolProviderAttachmentMessage(
                attachedToolCount,
                request.RuntimeToolProviders.Count,
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
                summary.ExcludedToolCount > 0
                    ? $"{summary.ProviderKey} ({summary.ProviderName}, {summary.ToolCount} tool(s), {summary.ExcludedToolCount} pruned)"
                    : $"{summary.ProviderKey} ({summary.ProviderName}, {summary.ToolCount} tool(s))"));
        return $"{message} Providers: {providers}.";
    }
}

internal interface IRuntimeToolProviderAccessFilter
{
    FilteredRuntimeToolProviderTools Filter(RuntimeToolProviderAccessFilterRequest request);
}

internal sealed class RuntimeToolProviderAccessFilter : IRuntimeToolProviderAccessFilter
{
    public FilteredRuntimeToolProviderTools Filter(RuntimeToolProviderAccessFilterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var metadataByToolName = request.Metadata.ToDictionary(
            item => item.ToolName,
            StringComparer.OrdinalIgnoreCase);
        var candidates = request.Tools
            .Select(tool =>
            {
                if (!metadataByToolName.TryGetValue(tool.Name, out var toolMetadata))
                {
                    throw new InvalidOperationException(
                        $"Runtime tool provider '{DescribeRuntimeToolProvider(request.Registration.Provider)}' did not resolve metadata for tool '{tool.Name}'.");
                }

                return RuntimeToolCapabilityDescriptorFactory.CreateRuntimeToolCapabilityDescriptor(
                    tool.Name,
                    $"Runtime provider tool {tool.Name}",
                    $"Tool exposed by runtime provider '{request.Registration.Descriptor.ProviderKey}'.",
                    ResolveRuntimeToolProviderCapabilityTags(request.Registration.Descriptor),
                    ResolveRuntimeToolOperationClassifications(toolMetadata),
                    RuntimeToolProviderCapabilityTags.CreateToolImplementationKey(
                        request.Registration.Descriptor.ProviderKey,
                        RuntimeToolName.Create(tool.Name)));
            })
            .ToList();
        var accessResult = request.AccessPlan.Evaluator.Evaluate(new CapabilityAccessEvaluationContext(
            candidates,
            RequiredCapabilities: [],
            request.AccessPlan.Policies,
            request.AccessPlan.CorrelationId));
        request.State.EffectiveCapabilityDescriptors.AddRange(accessResult.AllowedCapabilities);
        request.State.CapabilityAccessDiagnostics.AddRange(accessResult.Diagnostics);

        var allowedToolNames = accessResult.AllowedCapabilities
            .Select(capability => capability.RuntimeToolName?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var includedTools = new List<AITool>(request.Tools.Count);
        var includedMetadata = new List<AgentRuntimeToolMetadata>(request.Metadata.Count);
        foreach (var tool in request.Tools)
        {
            if (!metadataByToolName.TryGetValue(tool.Name, out var toolMetadata))
            {
                throw new InvalidOperationException(
                    $"Runtime tool provider '{DescribeRuntimeToolProvider(request.Registration.Provider)}' did not resolve metadata for tool '{tool.Name}'.");
            }

            if (!allowedToolNames.Contains(tool.Name))
            {
                continue;
            }

            includedTools.Add(tool);
            includedMetadata.Add(toolMetadata);
        }

        return new FilteredRuntimeToolProviderTools(
            includedTools,
            includedMetadata,
            request.Tools.Count - includedTools.Count);
    }

    private static IReadOnlySet<CapabilityOperationClassification> ResolveRuntimeToolOperationClassifications(
        AgentRuntimeToolMetadata metadata)
    {
        if (ToolCapabilityRegistry.TryResolve(metadata.ToolName, out _))
        {
            return RuntimeToolCapabilityDescriptorFactory.ResolveRuntimeToolOperationClassifications(metadata.ToolName);
        }

        return metadata.OperationKind switch
        {
            AgentRuntimeToolOperationKind.Read => RuntimeToolCapabilityDescriptorFactory.ToClassificationSet(CapabilityOperationClassification.Read),
            AgentRuntimeToolOperationKind.Validation => RuntimeToolCapabilityDescriptorFactory.ToClassificationSet(CapabilityOperationClassification.Validation),
            AgentRuntimeToolOperationKind.Mutation => RuntimeToolCapabilityDescriptorFactory.ToClassificationSet(CapabilityOperationClassification.Mutation),
            AgentRuntimeToolOperationKind.HostedProviderNative => RuntimeToolCapabilityDescriptorFactory.ToClassificationSet(CapabilityOperationClassification.ProviderNative),
            AgentRuntimeToolOperationKind.LocalMcp or AgentRuntimeToolOperationKind.HostedMcp => RuntimeToolCapabilityDescriptorFactory.ToClassificationSet(CapabilityOperationClassification.McpTool),
            _ => RuntimeToolCapabilityDescriptorFactory.ToClassificationSet(CapabilityOperationClassification.Read)
        };
    }

    private static IReadOnlyList<string> ResolveRuntimeToolProviderCapabilityTags(
        AgentRuntimeToolProviderDescriptor descriptor)
    {
        var tags = new List<string>
        {
            RuntimeToolProviderCapabilityTags.RuntimeProviderTagValue,
            RuntimeToolProviderCapabilityTags.CreateProviderKeyTagValue(descriptor.ProviderKey)
        };
        tags.AddRange(descriptor.DomainTags);
        return tags
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string DescribeRuntimeToolProvider(IAgentRuntimeToolProvider toolProvider)
    {
        return toolProvider.GetType().FullName ?? toolProvider.GetType().Name;
    }
}
