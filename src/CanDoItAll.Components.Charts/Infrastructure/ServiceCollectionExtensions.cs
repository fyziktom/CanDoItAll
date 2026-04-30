using ApexCharts;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Components.Charts;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllCharts(this IServiceCollection services)
    {
        services.AddApexCharts();
        return services;
    }
}
