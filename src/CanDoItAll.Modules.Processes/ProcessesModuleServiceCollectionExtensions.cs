using CanDoItAll.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.Processes;

public static class ProcessesModuleServiceCollectionExtensions
{
    public static IServiceCollection AddProcessesModule(this IServiceCollection services)
    {
        services.AddOptions<ProcessTemplatePackOptions>()
            .BindConfiguration(ProcessTemplatePackOptions.SectionName);
        services.AddScoped<ProcessesService>();
        services.AddScoped<ProcessOutboxService>();
        services.AddScoped<IProcessRunAutomationDispatchService, ProcessRunAutomationDispatchService>();
        services.AddScoped<IProcessDefinitionListQueryService, ProcessDefinitionListQueryService>();
        services.AddScoped<IProcessRuntimeReadQueryService, ProcessRuntimeReadQueryService>();
        services.AddScoped<ProcessWorkspaceRunDetailsLoader>();
        services.AddScoped<ProcessRunRecoveryService>();
        services.AddScoped<ProcessCanvasSurfaceFactory>();
        services.AddScoped<ProcessCanvasRecompositionService>();
        services.AddScoped<ProcessCanvasChromeCatalogService>();
        // Keep the template pack scoped until the loaded graph becomes deeply immutable.
        services.AddScoped(provider =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProcessTemplatePackOptions>>().Value;
            return new ProcessTemplatePackLoader(options.PackRoot);
        });
        services.AddScoped<ProcessTemplateCatalogService>();
        services.AddScoped<ProcessTemplateLibraryService>();
        services.AddScoped<ProcessTemplateProjectionService>();
        services.AddScoped<ProcessTemplateMermaidExporter>();
        services.AddScoped<ProcessDevelopmentSeedService>();
        services.AddScoped<ProcessCatalogWarmupService>();
        services.TryAddScoped<IProcessProjectStructureBridge, NoopProcessProjectStructureBridge>();
        services.AddScoped<IProcessExecutorRegistryBridge, NoopProcessExecutorRegistryBridge>();
        services.AddHostedService<ProcessCatalogWarmupWorker>();
        services.AddHostedService<ProcessOutboxDrainWorker>();
        services.AddHostedService<ProcessRunRecoveryWorker>();
        return services;
    }
}

public interface IProcessProjectStructureBridge
{
    Task SyncRunAsync(
        AppDbContext dbContext,
        ProcessRun run,
        IReadOnlyCollection<ProcessStepRun> stepRuns,
        CancellationToken cancellationToken = default);
}

internal sealed class NoopProcessProjectStructureBridge : IProcessProjectStructureBridge
{
    public Task SyncRunAsync(
        AppDbContext dbContext,
        ProcessRun run,
        IReadOnlyCollection<ProcessStepRun> stepRuns,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(stepRuns);
        return Task.CompletedTask;
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
