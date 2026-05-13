using Microsoft.Extensions.DependencyInjection;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;

namespace CanDoItAll.Modules.Security;

public static class SecurityModuleServiceCollectionExtensions
{
    public static IServiceCollection AddSecurityModule(this IServiceCollection services)
        => services.AddSecurityModule(configuration: null);

    public static IServiceCollection AddSecurityModule(this IServiceCollection services, IConfiguration? configuration)
    {
        var optionsBuilder = services.AddOptions<SecretVaultOptions>();
        if (configuration is not null)
        {
            optionsBuilder.Bind(configuration.GetSection(SecretVaultOptions.SectionName));
        }

        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddSingleton<ISecretVault>(serviceProvider =>
            SecretVaultFactory.CreateDefault(serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SecretVaultOptions>>().Value));
        services.AddSingleton<ISecretRuntimeResolver, SecretRuntimeResolver>();
        services.AddSingleton<IStorageSecretResolver, StorageSecretResolver>();
        services.AddScoped<SecretService>();
        return services;
    }
}

public static class SecurityModuleAssemblyMarker;
