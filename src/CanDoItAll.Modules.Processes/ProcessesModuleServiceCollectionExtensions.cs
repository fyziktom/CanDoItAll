using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Processes;

public static class ProcessesModuleServiceCollectionExtensions
{
    public static IServiceCollection AddProcessesModule(this IServiceCollection services)
    {
        services.AddScoped<ProcessesService>();
        services.AddScoped<ProcessCanvasSurfaceFactory>();
        services.AddScoped<ProcessCanvasChromeCatalogService>();
        services.AddScoped<ProcessTemplatePackLoader>();
        services.AddScoped<ProcessTemplateCatalogService>();
        services.AddScoped<ProcessTemplateProjectionService>();
        services.AddScoped<ProcessTemplateMermaidExporter>();
        services.AddScoped<ProcessDevelopmentSeedService>();
        services.AddScoped<IProcessExecutorRegistryBridge, NoopProcessExecutorRegistryBridge>();
        return services;
    }
}

public interface IProcessExecutorRegistryBridge
{
    Task<IReadOnlyList<ProcessExecutorRegistryOption>> ListOptionsAsync(CancellationToken cancellationToken = default);
}

public sealed record ProcessExecutorRegistryOption(
    string RegistryKey,
    string DisplayName,
    string ExecutorKind,
    string Steward,
    string CapabilitySummary);

internal sealed class NoopProcessExecutorRegistryBridge : IProcessExecutorRegistryBridge
{
    public Task<IReadOnlyList<ProcessExecutorRegistryOption>> ListOptionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ProcessExecutorRegistryOption>>([]);
    }
}

public static class ProcessesModuleAssemblyMarker
{
}
