using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

public static class PluginsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddPluginsModule(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        string? contentRootPath = null)
    {
        var packageOptions = PluginPackageOptions.FromConfiguration(configuration, contentRootPath);
        services.TryAddSingleton(packageOptions);
        RuntimePluginAssemblyRegistrar.RegisterInstalledPackages(services, packageOptions);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPluginCatalogSource, BundledPluginCatalogSource>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPluginCatalogSource, InstalledPluginPackageCatalogSource>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutorDescriptorSource, PluginWorkflowExecutorDescriptorSource>());
        services.AddHttpClient();
        services.AddScoped<PluginInstallationStore>();
        services.AddScoped<PluginGrantStore>();
        services.AddScoped<PluginConnectionStore>();
        services.AddScoped<PluginGrantEvaluator>();
        services.AddScoped<PluginOAuthService>();
        services.AddScoped<PluginLogStore>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkflowExecutorExecutionAuditSink, PluginWorkflowExecutorExecutionObserver>());
        services.TryAddScoped<IPluginExecutionEvents, DurablePluginExecutionEvents>();
        services.AddScoped<PluginHostToolRecipeCatalogService>();
        services.AddScoped<PluginSettingsService>();
        services.AddScoped<PluginCatalogService>();
        services.AddScoped<PluginPackageManifestStore>();
        services.AddScoped<PluginPackageService>();
        services.AddScoped<PluginPackageAssetService>();
        services.TryAddSingleton<PluginRuntimeRestartService>();
        services.AddHostedService<PluginPackageActivationHostedService>();
        return services;
    }
}

public static class PluginsModuleAssemblyMarker;
