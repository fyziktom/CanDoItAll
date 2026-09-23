using CanDoItAll.Modules.Processes;
using CanDoItAll.Agents.Storage;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Agents.SimpleChats;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Support;

public static class ProductToolPolicyTestRegistration {
    public static IReadOnlyList<ToolCapabilityMetadata> MovedProductPolicies { get; } = Array.AsReadOnly<ToolCapabilityMetadata>([
        .. HrAgentToolPolicy.Capabilities,
        .. HrSimpleChatToolPolicy.Capabilities,
        .. ProjectStructureToolPolicy.Capabilities,
        .. SchedulerToolPolicy.Capabilities,
        .. WorkflowToolPolicy.Capabilities,
        .. WorkflowCuratorToolPolicy.Capabilities,
        .. CapabilityCuratorToolPolicy.Capabilities,
        .. ImageGenerationToolPolicy.Capabilities
    ]);

    public static AgentToolPolicyCatalog ProductToolPolicies { get; } = new(
        MovedProductPolicies.Concat(PromptGalleryToolPolicy.Capabilities).Concat(StorageToolPolicy.Capabilities).Concat(ProcessCompatibilityToolPolicy.Capabilities));

    public static IServiceCollection AddProductToolPolicies(this IServiceCollection services) {
        services.TryAddSingleton<AgentToolPolicyCatalog>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAgentRuntimeCapabilityPolicyContributor, ProcessRuntimeCapabilityPolicyContributor>());
        foreach (var policy in MovedProductPolicies.Concat(PromptGalleryToolPolicy.Capabilities).Concat(StorageToolPolicy.Capabilities).Concat(ProcessCompatibilityToolPolicy.Capabilities)) {
            if (!services.Any(descriptor => ReferenceEquals(descriptor.ImplementationInstance, policy))) {
                services.AddSingleton(policy);
            }
        }
        return services;
    }
}
