using CanDoItAll.Modules.Automation;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.SchedulerPlanner;

public static class SchedulerPlannerModuleServiceCollectionExtensions
{
    public static IServiceCollection AddSchedulerPlannerModule(this IServiceCollection services)
    {
        services.AddScoped<ICronDescriptionService, QuartzCronDescriptionService>();
        services.AddScoped<ISchedulerTargetLauncher, SchedulerTargetLauncher>();
        services.AddScoped<ISchedulerPlannerService, SchedulerPlannerService>();
        services.AddScoped<IAutomationMessageHandler, SchedulerPlannerTriggerFireHandler>();
        return services;
    }
}

public static class SchedulerPlannerModuleAssemblyMarker;
