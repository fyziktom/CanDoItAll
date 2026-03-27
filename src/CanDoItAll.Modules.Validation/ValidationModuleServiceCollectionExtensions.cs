using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Validation;

public static class ValidationModuleServiceCollectionExtensions
{
    public static IServiceCollection AddValidationModule(this IServiceCollection services)
    {
        services.AddScoped<ValidationService>();
        return services;
    }
}

public static class ValidationModuleAssemblyMarker;


