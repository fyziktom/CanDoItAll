using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Configuration;
using Quartz;

namespace CanDoItAll.Modules.Automation;

public static class AutomationModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAutomationModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var backgroundWorkersEnabled = LocalRuntimeHostedWorkerPolicy.AreBackgroundHostedWorkersEnabled(
            configuration[LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey],
            configuration["LaneKind"]);

        services.AddOptions<AutomationRuntimeOptions>()
            .BindConfiguration(AutomationRuntimeOptions.SectionName);
        services.AddQuartz(options =>
        {
            options.SchedulerId = "CanDoItAll.Automation";
            options.SchedulerName = "CanDoItAll Automation";
        });
        services.AddScoped<AutomationWorkspaceService>();
        services.TryAddScoped<IAutomationSignalProvider, CompositeAutomationSignalProvider>();
        services.AddScoped<AutomationSubscriptionRegistry>();
        services.AddScoped<IAutomationMessagePublisher, AutomationMessagePublisher>();
        services.AddScoped<IAutomationMessageDispatcher, AutomationMessageDispatcher>();
        services.AddScoped<IAutomationTriggerRegistry, AutomationTriggerRegistry>();
        services.AddScoped<QuartzAutomationSchedulerBridge>();
        services.AddScoped<IPluginIngressInbox, PluginIngressInbox>();
        services.AddScoped<IAutomationTelemetryPublisher, AutomationTelemetryPublisher>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAutomationTelemetryBridge, MqttAutomationTelemetryBridge>());
        services.AddScoped<IAutomationBackgroundJobScheduler, AutomationBackgroundJobScheduler>();
        services.AddScoped<IAutomationMessageHandler, AutomationBackgroundJobMessageHandler>();
        services.AddScoped<IAutomationRuntimeInspectionService, AutomationRuntimeInspectionService>();

        if (backgroundWorkersEnabled)
        {
            services.AddQuartzHostedService(options =>
            {
                options.WaitForJobsToComplete = true;
            });
            services.AddHostedService<AutomationSchedulerProjectionHostedService>();
            services.AddHostedService<AutomationMessagePumpWorker>();
            services.AddHostedService<ConnectorOutboxDrainWorker>();
            services.AddHostedService<LegacyBackgroundJobQueueBridgeWorker>();
        }

        return services;
    }
}

public static class AutomationModuleAssemblyMarker;


