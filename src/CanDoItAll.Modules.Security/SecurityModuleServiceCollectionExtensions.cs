using Microsoft.Extensions.DependencyInjection;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Security;

public static class SecurityModuleServiceCollectionExtensions
{
    public static IServiceCollection AddSecurityModule(this IServiceCollection services)
    {
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddSingleton<IStorageSecretResolver, StorageSecretResolver>();
        services.AddScoped<SecretService>();
        return services;
    }
}

public static class SecurityModuleAssemblyMarker;
