using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Agents.SimpleChats;

public static class HrSimpleChatServiceCollectionExtensions {
    public static IServiceCollection AddHrSimpleChatDefinitionTools(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IAgentToolAdmissionVerifier))) {
            throw new InvalidOperationException("Register the durable tool-batch admission verifier before enabling HR Simple Chat administration.");
        }

        services.TryAddSingleton<AgentToolPolicyCatalog>();
        foreach (var policy in HrSimpleChatToolPolicy.Capabilities) {
            if (!services.Any(descriptor => ReferenceEquals(descriptor.ImplementationInstance, policy))) {
                services.AddSingleton(policy);
            }
        }
        var codec = new HrSimpleChatProposalCodec();
        services.TryAddSingleton(codec);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAgentToolProposalPreparer>(codec));
        services.TryAddScoped<HrSimpleChatRuntimeAuthorization>();
        services.TryAddScoped<HrSimpleChatAdministration>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, HrSimpleChatRuntimeToolProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentToolReceiptReconciliationProvider, HrSimpleChatRuntimeToolProvider>());
        return services;
    }
}
