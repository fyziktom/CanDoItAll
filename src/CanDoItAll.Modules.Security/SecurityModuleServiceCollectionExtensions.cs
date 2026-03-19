using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Security;

public static class SecurityModuleServiceCollectionExtensions
{
    public static IServiceCollection AddSecurityModule(this IServiceCollection services)
    {
        services.AddScoped<ISecretProtector, DataProtectionSecretProtector>();
        services.AddScoped<SecretService>();
        return services;
    }
}

public static class SecurityModuleAssemblyMarker;
