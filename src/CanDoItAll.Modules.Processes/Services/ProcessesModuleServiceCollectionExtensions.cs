using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.Processes;

public static class ProcessesModuleServiceCollectionExtensions
{
    public static IServiceCollection AddProcessesModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var backgroundWorkersEnabled = LocalRuntimeHostedWorkerPolicy.AreBackgroundHostedWorkersEnabled(
            configuration[LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey],
            configuration["LaneKind"]);
        var runtimeOptions = configuration
            .GetSection(ProcessRuntimeOptions.SectionName)
            .Get<ProcessRuntimeOptions>() ?? new ProcessRuntimeOptions();

        services.AddOptions<ProcessTemplatePackOptions>()
            .BindConfiguration(ProcessTemplatePackOptions.SectionName);
        services.AddOptions<ProcessRuntimeOptions>()
            .BindConfiguration(ProcessRuntimeOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                options => options.StepDispatchClaimLeaseDuration > TimeSpan.Zero,
                "Processes:Runtime:StepDispatchClaimLeaseDuration must be positive.")
            .Validate(
                options => options.StepDispatchHeartbeatInterval > TimeSpan.Zero,
                "Processes:Runtime:StepDispatchHeartbeatInterval must be positive.")
            .Validate(
                options => options.StepDispatchHeartbeatInterval < options.StepDispatchClaimLeaseDuration,
                "Processes:Runtime:StepDispatchHeartbeatInterval must be shorter than StepDispatchClaimLeaseDuration.")
            .ValidateOnStart();
        services.AddOptions<ProcessObservationCacheOptions>()
            .BindConfiguration(ProcessObservationCacheOptions.SectionName);
        services.AddScoped<ProcessesService>();
        services.AddScoped<ProcessOutboxService>();
        services.AddScoped<ProcessWorkflowRunCoordinator>();
        services.AddScoped<IProcessAutomationExecutionClient, ProcessAutomationExecutionClient>();
        services.AddScoped<IProcessRunAutomationDispatchService, ProcessRunAutomationDispatchService>();
        services.AddScoped<IProcessDefinitionListQueryService, ProcessDefinitionListQueryService>();
        services.AddScoped<IProcessRuntimeReadQueryService, ProcessRuntimeReadQueryService>();
        services.AddSingleton<ProcessObservationCache>();
        services.AddSingleton<IProcessObservationInvalidator>(provider => provider.GetRequiredService<ProcessObservationCache>());
        services.AddScoped<IProcessObservationService, ProcessObservationService>();
        services.AddScoped<IProcessManagerChatService, ProcessManagerChatService>();
        services.AddScoped<IProcessObservationIntentResolver, ProcessObservationIntentResolver>();
        services.AddScoped<IProcessRuntimeEvidenceSourceProvider, ProcessRuntimeEvidenceSourceProvider>();
        services.AddScoped<ProcessObservationDashboardState>();
        services.AddScoped<ProcessRuntimeStateOverviewService>();
        services.AddScoped<ProcessWorkspaceRunDetailsLoader>();
        services.AddScoped<ProcessRunRecoveryService>();
        services.AddSingleton<ProcessRunRecoveryStartupGate>();
        services.AddSingleton<ProcessRuntimeSession>();
        services.AddScoped<IProcessEscalationService, ProcessEscalationService>();
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
        services.AddScoped<IDatabaseTransferHandler, ProcessDefinitionsDatabaseTransferHandler>();
        services.AddScoped<ProcessTemplateLibraryService>();
        services.AddScoped<ProcessTemplateProjectionService>();
        services.AddScoped<ProcessTemplateMermaidExporter>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, ProcessAgentRuntimeToolProvider>());
        services.AddScoped<ProcessDevelopmentSeedService>();
        services.AddScoped<ProcessCatalogWarmupService>();
        services.TryAddScoped<IProcessProjectStructureBridge, NoopProcessProjectStructureBridge>();
        services.AddScoped<IProcessExecutorRegistryBridge, NoopProcessExecutorRegistryBridge>();

        if (backgroundWorkersEnabled)
        {
            services.AddHostedService<ProcessCatalogWarmupWorker>();
            if (runtimeOptions.RecoverActiveRunsOnStartup)
            {
                services.AddHostedService<ProcessRunRecoveryWorker>();
            }

            services.AddHostedService<ProcessOutboxDrainWorker>();
        }

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

    Task<IReadOnlyList<string>> ListLaunchContextAsync(
        AppDbContext dbContext,
        Guid projectId,
        ProcessProjectStructureContext? projectStructureContext,
        CancellationToken cancellationToken = default);

    Task<ProcessProjectStructureContext?> TryResolveLaunchContextAsync(
        AppDbContext dbContext,
        Guid projectId,
        Guid processDefinitionId,
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

    public Task<IReadOnlyList<string>> ListLaunchContextAsync(
        AppDbContext dbContext,
        Guid projectId,
        ProcessProjectStructureContext? projectStructureContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    public Task<ProcessProjectStructureContext?> TryResolveLaunchContextAsync(
        AppDbContext dbContext,
        Guid projectId,
        Guid processDefinitionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        return Task.FromResult<ProcessProjectStructureContext?>(null);
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
