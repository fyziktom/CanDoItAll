using CanDoItAll.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Security.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
            SecretVaultFactory.CreateDefault(
                serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SecretVaultOptions>>().Value,
                serviceProvider.GetRequiredService<DurableFileWriter>(),
                serviceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment()));
        services.AddSingleton<SecretVaultCapabilityState>();
        services.AddSingleton<ISecretVaultCapabilityState>(serviceProvider =>
            serviceProvider.GetRequiredService<SecretVaultCapabilityState>());
        services.AddHostedService<SecretVaultStartupValidator>();
        services.AddSingleton<ISecretRuntimeResolver, SecretRuntimeResolver>();
        services.AddSingleton<IStorageSecretResolver, StorageSecretResolver>();
        services.TryAddSingleton<ISecretMigrationAuditSink, NullSecretMigrationAuditSink>();
        services.TryAddSingleton<ISecretMigrationInterruptionObserver, NullSecretMigrationInterruptionObserver>();
        services.TryAddSingleton<ISecretMigrationCoordinatorFactory, SecretMigrationCoordinatorFactory>();
        services.AddScoped<IPluginSecretBroker, PluginSecretBroker>();
        services.AddScoped<SecretService>();
        return services;
    }
}

public static class SecurityModuleAssemblyMarker;
