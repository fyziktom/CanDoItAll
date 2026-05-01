using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Components.Mermaid.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllMermaid(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
