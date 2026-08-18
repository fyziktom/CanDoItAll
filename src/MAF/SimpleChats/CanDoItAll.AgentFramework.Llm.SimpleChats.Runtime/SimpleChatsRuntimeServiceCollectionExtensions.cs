using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.Conversations;
using CanDoItAll.AgentFramework.Llm.ProviderRuntime;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime;

public static class SimpleChatsRuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddSimpleChatsRuntime(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddProviderBackedLlmInvocationPort();
        services.TryAddSingleton<IProviderModelCapabilityResolver, ProviderModelCapabilityResolver>();
        services.TryAddSingleton<ILlmConversationContextWindowPolicy, RecencyBoundedContextWindowPolicy>();
        services.TryAddSingleton<ILlmChatOperationScopeAccessor, LlmChatOperationScopeAccessor>();
        services.TryAddScoped<CanonicalLlmChatProviderResolver>();
        services.TryAddScoped<ILlmChatProviderResolver>(provider =>
            provider.GetRequiredService<CanonicalLlmChatProviderResolver>());
        services.TryAddScoped<LlmChatConversationEngineFactory>();
        services.TryAddScoped<ILlmChatConversationEngine>(provider =>
            provider.GetRequiredService<LlmChatConversationEngineFactory>().Create());
        return services;
    }
}
