using CanDoItAll.Memory.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Memory.Mock;

public static class MockMemoryServiceCollectionExtensions
{
    public static IServiceCollection AddDeterministicMockMemoryProviderDriver(
        this IServiceCollection services)
    {
        services.TryAddSingleton<DeterministicMockMemoryProviderDriver>();
        services.AddSingleton<IMemoryProviderDriver>(provider =>
            provider.GetRequiredService<DeterministicMockMemoryProviderDriver>());
        return services;
    }
}
