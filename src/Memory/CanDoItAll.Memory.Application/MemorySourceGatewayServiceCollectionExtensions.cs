using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Application;

public static class MemorySourceGatewayServiceCollectionExtensions
{
    public static IServiceCollection AddMemorySourceGatewayAdapter<TAdapter>(this IServiceCollection services)
        where TAdapter : class, IMemorySourceGatewayAdapter
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IMemorySourceGatewayAdapter, TAdapter>();
        return services;
    }
}
