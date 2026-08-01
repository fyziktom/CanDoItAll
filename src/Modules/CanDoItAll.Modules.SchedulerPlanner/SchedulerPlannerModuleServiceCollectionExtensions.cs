using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace CanDoItAll.Modules.SchedulerPlanner;

public static class SchedulerPlannerModuleServiceCollectionExtensions
{
    public static IServiceCollection AddSchedulerPlannerModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var backgroundWorkersEnabled = LocalRuntimeHostedWorkerPolicy.AreBackgroundHostedWorkersEnabled(
            configuration[LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey],
            configuration["LaneKind"]);

        services.AddQuartz(options =>
        {
            options.SchedulerId = "CanDoItAll.SchedulerPlanner";
            options.SchedulerName = "CanDoItAll SchedulerPlanner";
        });
        services.AddScoped<ICronDescriptionService, QuartzCronDescriptionService>();
        services.AddScoped<ISchedulerWorkflowInputSchemaService, SchedulerWorkflowInputSchemaService>();
        services.AddScoped<ISchedulerWorkflowInputOptionService, SchedulerWorkflowInputOptionService>();
        services.AddScoped<ISchedulerTargetLauncher, SchedulerTargetLauncher>();
        services.AddScoped<ISchedulerPlannerTriggerScheduler, SchedulerPlannerTriggerScheduler>();
        services.AddScoped<ISchedulerPlannerRunDispatcher, SchedulerPlannerRunDispatcher>();
        services.AddScoped<ISchedulerPlannerService, SchedulerPlannerService>();
        services.AddScoped<SchedulerAgentRuntimeAuthorizationService>();
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, SchedulerAgentRuntimeToolProvider>());

        if (backgroundWorkersEnabled)
        {
            services.AddQuartzHostedService(options =>
            {
                options.WaitForJobsToComplete = true;
            });
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, SchedulerPlannerProjectionHostedService>());
        }

        return services;
    }
}

public static class SchedulerPlannerModuleAssemblyMarker;
