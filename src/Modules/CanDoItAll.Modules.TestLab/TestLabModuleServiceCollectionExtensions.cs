using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.TestLab;

public static class TestLabModuleServiceCollectionExtensions
{
    public static IServiceCollection AddTestLabModule(this IServiceCollection services)
    {
        services.AddScoped<TestLabService>();
        return services;
    }
}

public static class TestLabModuleAssemblyMarker;


