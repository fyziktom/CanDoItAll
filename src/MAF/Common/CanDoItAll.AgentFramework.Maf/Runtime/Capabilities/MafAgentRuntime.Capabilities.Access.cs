using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Access;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private static readonly RuntimeToolName[] StorageRuntimeToolNames =
    [
        RuntimeToolName.Create("storage_catalog_list"),
        RuntimeToolName.Create("storage_read_text_file"),
        RuntimeToolName.Create("storage_write_text_file"),
        RuntimeToolName.Create("storage_delete_object")
    ];

    private RuntimeCapabilityAccessPlan CreateRuntimeCapabilityAccessPlan(
        AgentDefinition agent,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        AgentWorkspaceToolAccessSettings workspaceToolAccess,
        AgentRuntimeContextIntent contextIntent,
        bool storageToolsAvailable)
    {
        var catalogDescriptors = capabilities
            .Select(CreateCatalogCapabilityDescriptor)
            .ToList();
        var catalogByIdentity = catalogDescriptors
            .Zip(capabilities)
            .ToDictionary(pair => pair.First.Identity, pair => pair.Second);
        var configuredWorkspaceDescriptors = CreateConfiguredWorkspaceToolDescriptors(workspaceToolAccess, storageToolsAvailable);
        var candidates = DistinctCapabilityDescriptors(
            catalogDescriptors.Concat(configuredWorkspaceDescriptors));
        var policies = BuildRuntimeCapabilityAccessPolicies(workspaceToolAccess, contextIntent);
        var evaluator = services.GetService(typeof(ICapabilityAccessPolicyEvaluator)) as ICapabilityAccessPolicyEvaluator
            ?? new CapabilityAccessPolicyEvaluator();
        var correlationId = ResolveCapabilityAccessCorrelationId(contextIntent);
        var result = evaluator.Evaluate(new CapabilityAccessEvaluationContext(
            candidates,
            RequiredCapabilities: [],
            policies,
            correlationId));
        var allowedCatalogIdentities = result.AllowedCapabilities
            .Select(capability => capability.Identity)
            .Where(catalogByIdentity.ContainsKey)
            .ToHashSet();

        return new RuntimeCapabilityAccessPlan(
            result.ToEffectiveSet(),
            capabilities
                .Where(capability => allowedCatalogIdentities.Contains(CreateCatalogCapabilityIdentity(capability)))
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

    private static string NormalizeTemplatePathSegment(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : string.Concat(value.Trim().Select(character =>
                char.IsLetterOrDigit(character) || character is '-' or '_'
                    ? char.ToLowerInvariant(character)
                    : '-'));
    }

    private static string ToKebab(string value)
        => string.Concat(value.Select((character, index) =>
            index > 0 && char.IsUpper(character)
                ? "-" + char.ToLowerInvariant(character)
                : char.ToLowerInvariant(character).ToString()));

    private static void AttachInitialCapabilityAccessState(
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

    private static CapabilityAccessEvaluationResult EvaluateRuntimeToolAccess(
        RuntimeCapabilityAccessPlan accessPlan,
        IReadOnlyList<CapabilityExposureDescriptor> candidates)
    {
        return accessPlan.Evaluator.Evaluate(new CapabilityAccessEvaluationContext(
            candidates,
            RequiredCapabilities: [],
            accessPlan.Policies,
            accessPlan.CorrelationId));
    }

    private static void AppendRuntimeToolAccessResult(
        RuntimeCapabilityState state,
        CapabilityAccessEvaluationResult result)
    {
        state.EffectiveCapabilityDescriptors.AddRange(result.AllowedCapabilities);
        state.CapabilityAccessDiagnostics.AddRange(result.Diagnostics);
    }

    private sealed record RuntimeCapabilityAccessPlan(
        EffectiveCapabilitySet EffectiveCapabilities,
        IReadOnlyList<CapabilityCatalogItem> AllowedCatalogCapabilities,
        IReadOnlyList<CapabilityAccessPolicy> Policies,
        ICapabilityAccessPolicyEvaluator Evaluator,
        IReadOnlyList<CapabilityExposureDescriptor> InitialAllowedCapabilities,
        IReadOnlyList<SuppressedCapabilityDiagnostic> InitialDiagnostics,
        IReadOnlyDictionary<CapabilityIdentity, CapabilityCatalogItem> CatalogCapabilitiesByIdentity,
        IReadOnlyDictionary<string, CapabilityExposureDescriptor> DescriptorsByKey,
        string CorrelationId);
}
