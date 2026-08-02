using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Access;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class RuntimeCapabilityAccessPlanner
{
    private readonly RuntimeCapabilityDescriptorCatalog descriptorCatalog;
    private readonly ICapabilityAccessPolicyEvaluator evaluator;

    public RuntimeCapabilityAccessPlanner(
        RuntimeCapabilityDescriptorCatalog descriptorCatalog,
        ICapabilityAccessPolicyEvaluator evaluator)
    {
        this.descriptorCatalog = descriptorCatalog ?? throw new ArgumentNullException(nameof(descriptorCatalog));
        this.evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
    }

    public RuntimeCapabilityAccessPlan CreateRuntimeCapabilityAccessPlan(
        AgentDefinition agent,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        AgentWorkspaceToolAccessSettings workspaceToolAccess,
        AgentRuntimeContextIntent contextIntent,
        RuntimeStorageToolAvailability storageAvailability)
    {
        var catalogDescriptors = capabilities
            .Select(descriptorCatalog.CreateCatalogCapabilityDescriptor)
            .ToList();
        var catalogByIdentity = catalogDescriptors
            .Zip(capabilities)
            .ToDictionary(pair => pair.First.Identity, pair => pair.Second);
        var configuredWorkspaceDescriptors = RuntimeConfiguredWorkspaceToolDescriptorCatalog.CreateConfiguredWorkspaceToolDescriptors(
            workspaceToolAccess,
            storageAvailability);
        var candidates = DistinctCapabilityDescriptors(
            catalogDescriptors.Concat(configuredWorkspaceDescriptors));
        var policies = RuntimeCapabilityAccessPolicyBuilder.BuildRuntimeCapabilityAccessPolicies(workspaceToolAccess, contextIntent);
        var correlationId = ResolveCapabilityAccessCorrelationId(contextIntent);
        var requiredCapabilities = contextIntent.CapabilityScopeOverride?.RequiredCapabilities ?? [];
        var result = evaluator.Evaluate(new CapabilityAccessEvaluationContext(
            candidates,
            requiredCapabilities,
            policies,
            correlationId));
        var allowedCatalogIdentities = result.AllowedCapabilities
            .Select(capability => capability.Identity)
            .Where(catalogByIdentity.ContainsKey)
            .ToHashSet();

        return new RuntimeCapabilityAccessPlan(
            result.ToEffectiveSet(),
            capabilities
                .Where(capability => allowedCatalogIdentities.Contains(RuntimeCapabilityDescriptorCatalog.CreateCatalogCapabilityIdentity(capability)))
                .ToList(),
            policies,
            evaluator,
            result.AllowedCapabilities,
            result.Diagnostics,
            catalogByIdentity,
            candidates.ToDictionary(CreateCapabilityDescriptorKey, StringComparer.OrdinalIgnoreCase),
            correlationId);
    }

    private static IReadOnlyList<CapabilityExposureDescriptor> DistinctCapabilityDescriptors(
        IEnumerable<CapabilityExposureDescriptor> descriptors)
        => descriptors
            .GroupBy(CreateCapabilityDescriptorKey, StringComparer.OrdinalIgnoreCase)
            .Select(MergeCapabilityDescriptors)
            .ToList();

    private static CapabilityExposureDescriptor MergeCapabilityDescriptors(
        IGrouping<string, CapabilityExposureDescriptor> descriptors)
    {
        var first = descriptors.First();
        var tags = descriptors
            .SelectMany(descriptor => descriptor.Tags)
            .ToHashSet();
        var operationClassifications = descriptors
            .SelectMany(descriptor => descriptor.OperationClassifications)
            .ToHashSet();

        return first with
        {
            Tags = tags,
            OperationClassifications = operationClassifications
        };
    }

    private static string CreateCapabilityDescriptorKey(CapabilityExposureDescriptor descriptor)
    {
        var runtimeToolName = descriptor.RuntimeToolName?.Value ?? string.Empty;
        var mcpServerKey = descriptor.McpServerKey?.Value ?? string.Empty;
        var mcpToolName = descriptor.McpToolName?.Value ?? string.Empty;
        return $"{descriptor.Identity.Kind}:{descriptor.Identity.Key.Value}:{runtimeToolName}:{mcpServerKey}:{mcpToolName}";
    }

    private static string ResolveCapabilityAccessCorrelationId(AgentRuntimeContextIntent contextIntent)
    {
        var auditCorrelationId = WorkspaceExecutionAuditContext.Current?.CorrelationId;
        if (!string.IsNullOrWhiteSpace(auditCorrelationId))
        {
            return auditCorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(contextIntent.ProcessRunId) ||
            !string.IsNullOrWhiteSpace(contextIntent.ProcessStepId))
        {
            return $"{contextIntent.ProcessRunId}:{contextIntent.ProcessStepId}";
        }

        return "maf-runtime-composition";
    }

    public static void AttachInitialCapabilityAccessState(
        RuntimeCapabilityState state,
        RuntimeCapabilityAccessPlan accessPlan)
    {
        state.EffectiveCapabilityDescriptors.AddRange(accessPlan.InitialAllowedCapabilities);
        state.CapabilityAccessDiagnostics.AddRange(accessPlan.InitialDiagnostics);

        foreach (var diagnostic in accessPlan.InitialDiagnostics)
        {
            var source = CreateAccessDiagnosticContextSource(diagnostic, accessPlan);
            if (source is not null)
            {
                state.ContextSources.Add(source);
            }
        }
    }

    private static AgentRuntimeContextManifestSource? CreateAccessDiagnosticContextSource(
        SuppressedCapabilityDiagnostic diagnostic,
        RuntimeCapabilityAccessPlan accessPlan)
    {
        if (accessPlan.CatalogCapabilitiesByIdentity.TryGetValue(diagnostic.Identity, out var catalogCapability))
        {
            if (catalogCapability.Kind == ModelCapabilityKind.Skill)
            {
                return AgentRuntimeContextManifestSource.Excluded(
                    AgentRuntimeContextSourceCategories.Skills,
                    "agent-skills-provider",
                    diagnostic.Reason);
            }

            return AgentRuntimeContextManifestSource.Excluded(
                AgentRuntimeContextSourceCategories.CatalogCapability,
                catalogCapability.Key,
                diagnostic.Reason);
        }

        var descriptor = accessPlan.DescriptorsByKey.Values.FirstOrDefault(item => item.Identity == diagnostic.Identity);
        if (descriptor?.RuntimeToolName is { } runtimeToolName &&
            IsWorkspaceOrStorageRuntimeTool(runtimeToolName.Value))
        {
            return AgentRuntimeContextManifestSource.Excluded(
                AgentRuntimeContextSourceCategories.WorkspaceTools,
                runtimeToolName.Value,
                diagnostic.Reason);
        }

        return null;
    }

    private static bool IsWorkspaceOrStorageRuntimeTool(string runtimeToolName)
        => runtimeToolName.StartsWith("workspace_", StringComparison.OrdinalIgnoreCase) ||
           runtimeToolName.StartsWith("storage_", StringComparison.OrdinalIgnoreCase) ||
           ToolContractCatalog.WorkspaceToolNames.Contains(runtimeToolName, StringComparer.OrdinalIgnoreCase);

}
