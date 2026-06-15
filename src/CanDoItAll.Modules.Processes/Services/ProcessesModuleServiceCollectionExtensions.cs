using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Processes;

public static class ProcessesModuleServiceCollectionExtensions
{
    public static IServiceCollection AddProcessesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(ProcessModuleRewriteState.Disabled);
        return services;
    }
}

public sealed record ProcessModuleRewriteState(bool IsEnabled)
{
    public static ProcessModuleRewriteState Disabled { get; } = new(false);
}

public static class ProcessesModuleAssemblyMarker;
