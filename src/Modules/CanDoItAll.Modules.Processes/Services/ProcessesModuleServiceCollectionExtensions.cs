using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Modules.Processes;

public static class ProcessesModuleServiceCollectionExtensions
{
    public static IServiceCollection AddProcessesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<ProcessPersistenceDbContext>(ConfigureProcessPersistenceDbContext);
        services.AddDbContextFactory<ProcessPersistenceDbContext>(
            ConfigureProcessPersistenceDbContext,
            ServiceLifetime.Scoped);

        services.TryAddSingleton<IProcessProjectionClock, SystemProcessProjectionClock>();
        services.TryAddSingleton(ProcessProjectionJsonCodec.Default);
        services.TryAddSingleton<ProcessTemplatePackLoader>();
        services.TryAddScoped<EfProcessRuntimeUnitOfWork>();
        services.TryAddScoped<IProcessRuntimeUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<EfProcessRuntimeUnitOfWork>());
        services.TryAddScoped<IProcessRuntimeStateStore>(serviceProvider => serviceProvider.GetRequiredService<EfProcessRuntimeUnitOfWork>());
        services.TryAddScoped<IProcessRuntimeRunHierarchyStore>(serviceProvider => serviceProvider.GetRequiredService<EfProcessRuntimeUnitOfWork>());
        services.TryAddScoped<IProcessIdempotencyStore>(serviceProvider => serviceProvider.GetRequiredService<EfProcessRuntimeUnitOfWork>());
        services.TryAddScoped<EfProcessRuntimeEventStore>();
        services.TryAddScoped<IProcessRuntimeEventStore>(serviceProvider => serviceProvider.GetRequiredService<EfProcessRuntimeEventStore>());
        services.TryAddScoped<IProcessRuntimeEventReplayStore>(serviceProvider => serviceProvider.GetRequiredService<EfProcessRuntimeEventStore>());
        services.TryAddScoped<IProcessOutboxWriter, EfProcessOutboxStore>();
        services.TryAddScoped<IProcessArtifactLedgerStore, EfProcessArtifactLedgerStore>();
        services.TryAddScoped<IProcessProjectionStore, EfProcessProjectionStore>();
        services.TryAddScoped<IProcessInstancePlanStore, EfProcessInstancePlanStore>();
        services.TryAddScoped<IProcessRuntimeStepAssignmentStore, EfProcessRuntimeStepAssignmentStore>();
        services.TryAddScoped<IProcessHistoricalRunCostReader, EfProcessHistoricalRunCostReader>();
        services.TryAddScoped<IProcessRuntimeProjector, ProcessRuntimeProjectionProjector>();
        services.TryAddScoped<ProcessRuntimeProjectionCatchupService>();
        services.TryAddScoped<ProcessRuntimeBranchSignalApplicationService>();
        services.TryAddScoped<IProcessStepBriefBuilder, AgentFrameworkProcessStepBriefBuilder>();
        services.TryAddScoped<IProcessExecutionAdapter, AgentFrameworkProcessExecutionAdapter>();
        services.TryAddScoped<IProcessExecutionObservationReader, AgentFrameworkProcessExecutionObservationReader>();
        services.TryAddScoped<IProcessRuntimeUsageTelemetryReader, AgentFrameworkProcessRuntimeUsageTelemetryReader>();
        services.TryAddScoped<AgentFrameworkProcessExecutionClaimRecoveryCoordinator>();
        services.TryAddScoped<AgentFrameworkProcessExecutionClaimRecoveryReconciler>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentExecutionRecoveryObserver, AgentFrameworkProcessExecutionRecoveryObserver>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IProcessRuntimeRunCancellationObserver, AgentFrameworkProcessRuntimeCancellationObserver>());
        services.TryAddScoped<IProcessLaunchDriverCatalogProvider, StandardProcessLaunchDriverCatalogProvider>();
        services.TryAddScoped<IProcessLaunchExecutorResolver, AgentFrameworkProcessLaunchExecutorResolver>();
        services.TryAddScoped<IProcessRuntimeStepAssignmentRepairService, AgentFrameworkProcessRuntimeStepAssignmentRepairService>();
        services.TryAddScoped<IProcessLaunchArtifactInitializer, WorkspaceProcessLaunchArtifactInitializer>();
        services.TryAddScoped<IProcessRuntimeStrategyFactoryResolver, StandardProcessRuntimeStrategyFactoryResolver>();
        services.Configure<ProcessRuntimeDispatchQueueOptions>(
            configuration.GetSection(ProcessRuntimeDispatchQueueOptions.ConfigurationSectionName));
        services.TryAddSingleton<ProcessRuntimeDispatchQueue>();
        services.TryAddSingleton<IProcessRuntimeDispatchQueue>(serviceProvider => serviceProvider.GetRequiredService<ProcessRuntimeDispatchQueue>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ProcessRuntimeDispatchQueueWorker>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, AgentFrameworkProcessExecutionClaimRecoveryWorker>());
        services.TryAddScoped<ProcessLaunchApplicationService>();
        services.TryAddScoped<ProcessRuntimeDispatchApplicationService>();
        services.TryAddScoped<ProcessRuntimeOperatorApplicationService>();
        services.TryAddScoped<ProcessRuntimeProjectionQueryService>();
        services.TryAddScoped<ProcessDefinitionCatalogProjectionService>();
        services.TryAddScoped<ProcessDefinitionEditorProjectionService>();
        services.TryAddScoped<ProcessDefinitionRoleEditorProjectionService>();
        services.TryAddScoped<ProcessDefinitionCanvasEditorProjectionService>();
        services.TryAddScoped<ProcessDefinitionStepEditorProjectionService>();
        services.TryAddScoped<ProcessTemplateCatalogProjectionService>();
        services.TryAddScoped<ProcessWorkspaceShellProjectionService>();
        services.TryAddSingleton<IProcessWorkspaceProjectionClient, ProcessWorkspaceProjectionClient>();
        services.TryAddSingleton<ProcessWorkspaceMockProjectionFactory>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IShellNavigationContributor, ProcessesShellNavigationContributor>());
        services.AddSingleton<ProcessModuleRewriteState>(ProcessModuleRewriteState.Enabled);
        return services;
    }

    private static void ConfigureProcessPersistenceDbContext(
        IServiceProvider serviceProvider,
        DbContextOptionsBuilder options)
    {
        var profile = serviceProvider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        AppDbContextOptionsConfigurator.ConfigureModelCacheKey(options);

        switch (profile.Profile.ProviderKind)
        {
            case DatabaseProviderKind.InMemory:
                options.UseInMemoryDatabase(string.IsNullOrWhiteSpace(profile.ConnectionString)
                    ? $"processes-{profile.Profile.Id:D}"
                    : profile.ConnectionString);
                break;

            case DatabaseProviderKind.PostgreSql:
                options.UseNpgsql(
                    profile.ConnectionString,
                    builder => builder.MigrationsAssembly("CanDoItAll.Migrations.PostgreSql"));
                break;

            default:
                throw new InvalidOperationException($"Unsupported process database provider '{profile.Profile.ProviderKind}'.");
        }
    }
}

public sealed record ProcessModuleRewriteState(bool IsEnabled)
{
    public static ProcessModuleRewriteState Disabled { get; } = new(false);

    public static ProcessModuleRewriteState Enabled { get; } = new(true);
}

public static class ProcessesModuleAssemblyMarker;
