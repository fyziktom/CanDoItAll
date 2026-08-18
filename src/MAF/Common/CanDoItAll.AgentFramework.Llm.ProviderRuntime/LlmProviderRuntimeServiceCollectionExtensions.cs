using CanDoItAll.AgentFramework.Llm.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Llm.ProviderRuntime;

public static class LlmProviderRuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddProviderBackedLlmInvocationPort(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ILlmInvocationPort, ProviderBackedLlmInvocationAdapter>();
        services.TryAddSingleton<ILlmStreamingInvocationPort, ProviderBackedLlmStreamingInvocationAdapter>();
        return services;
    }
}
