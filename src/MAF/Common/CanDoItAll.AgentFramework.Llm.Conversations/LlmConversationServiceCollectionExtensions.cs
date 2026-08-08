using CanDoItAll.AgentFramework.Llm.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Llm.Conversations;

/// <summary>
/// Registers the ordinary LLM conversation foundation: the file-backed store, the default
/// non-destructive context-window policy, and the application service above the already-registered
/// stateless <see cref="ILlmInvocationPort"/>. No agent, tool, memory, workspace-authority, or process
/// service participates in this composition.
/// </summary>
public static class LlmConversationServiceCollectionExtensions
{
    public static IServiceCollection AddLlmConversations(
        this IServiceCollection services,
        Func<IServiceProvider, string> storageRootResolver,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(storageRootResolver);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ILlmConversationContextWindowPolicy, RecencyBoundedContextWindowPolicy>();
        services.TryAdd(ServiceDescriptor.Describe(
            typeof(ILlmConversationStore),
            serviceProvider => new FileLlmConversationStore(storageRootResolver(serviceProvider)),
            lifetime));
        services.TryAdd(ServiceDescriptor.Describe(
            typeof(ILlmConversationService),
            serviceProvider => new LlmConversationService(
                serviceProvider.GetRequiredService<ILlmInvocationPort>(),
                serviceProvider.GetRequiredService<ILlmConversationStore>(),
                serviceProvider.GetRequiredService<ILlmConversationContextWindowPolicy>(),
                serviceProvider.GetRequiredService<TimeProvider>()),
            lifetime));
        return services;
    }
}
