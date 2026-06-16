using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.Processes;

public static class ProcessesModuleServiceCollectionExtensions
{
    public static IServiceCollection AddProcessesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton<IProcessProjectionClock, SystemProcessProjectionClock>();
        services.TryAddScoped<ProcessDefinitionCatalogProjectionService>();
        services.TryAddScoped<ProcessDefinitionEditorProjectionService>();
        services.TryAddScoped<ProcessDefinitionRoleEditorProjectionService>();
        services.TryAddScoped<ProcessDefinitionCanvasEditorProjectionService>();
        services.TryAddScoped<ProcessDefinitionStepEditorProjectionService>();
        services.TryAddScoped<ProcessWorkspaceShellProjectionService>();
        services.TryAddScoped<IProcessWorkspaceProjectionClient, ProcessWorkspaceProjectionClient>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellNavigationContributor, ProcessesShellNavigationContributor>());
        services.AddSingleton<ProcessModuleRewriteState>(ProcessModuleRewriteState.Enabled);
        return services;
    }
}

public sealed record ProcessModuleRewriteState(bool IsEnabled)
{
    public static ProcessModuleRewriteState Disabled { get; } = new(false);

    public static ProcessModuleRewriteState Enabled { get; } = new(true);
}

public static class ProcessesModuleAssemblyMarker;
