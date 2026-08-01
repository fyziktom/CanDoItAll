using CanDoItAll.Modules.Memory.Components;
using CanDoItAll.Modules.Memory.Pages;
using CanDoItAll.Modules.Memory.Services;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.Memory;

public static class MemoryModuleServiceCollectionExtensions
{
    public static IServiceCollection AddMemoryUiModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IMemoryProviderUiSurfaceComponentRegistry, MemoryProviderUiSurfaceComponentRegistry>();
        services.TryAddSingleton<MemoryProviderProfileEditorMapper>();
        services.TryAddScoped<IMemoryProviderProfileConfigurationService, MemoryProviderProfileConfigurationService>();
        services.TryAddScoped<MemoryProviderUiSurfaceProjector>();
        services.TryAddScoped<MemoryProviderSnapshotReader>();
        services.TryAddScoped<MemoryProviderProfileUiService>();
        services.TryAddScoped<MemoryProviderUiRequestFactory>();
        services.TryAddScoped<MemoryProviderExecutableActionGuard>();
        services.TryAddScoped<MemoryProviderQueryUiService>();
        services.TryAddScoped<MemoryProviderLedgerActionUiService>();
        services.TryAddScoped<MemoryProviderIngestionUiService>();
        services.TryAddScoped<IMemoryProviderManagementUiService, MemoryProviderManagementUiService>();
        services.TryAddTransient<MemoryProvidersPageController>();
        services.AddSingleton(
            new MemoryProviderUiSurfaceComponentRegistration(
                MemoryProviderUiSurfaceKeys.MockProviderPanelComponent,
                typeof(MemoryMockProviderPanel)));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellNavigationContributor, MemoryShellNavigationContributor>());
        return services;
    }
}

public static class MemoryModuleAssemblyMarker;
