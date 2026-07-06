using CanDoItAll.Modules.Memory.Components;
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
        services.TryAddScoped<IMemoryProviderManagementUiService, MemoryProviderManagementUiService>();
        services.AddSingleton(
            new MemoryProviderUiSurfaceComponentRegistration(
                MemoryProviderUiSurfaceKeys.MockProviderPanelComponent,
                typeof(MemoryMockProviderPanel)));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellNavigationContributor, MemoryShellNavigationContributor>());
        return services;
    }
}

public static class MemoryModuleAssemblyMarker;
