using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Agents.Storage;

public static class StorageAgentToolServiceCollectionExtensions {
    public static IServiceCollection AddAgentStorageTools(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<AgentToolPolicyCatalog>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, StorageAgentRuntimeToolProvider>());
        foreach (var policy in StorageToolPolicy.Capabilities) {
            if (!services.Any(descriptor => ReferenceEquals(descriptor.ImplementationInstance, policy))) {
                services.AddSingleton(policy);
            }
        }
        return services;
    }
}
