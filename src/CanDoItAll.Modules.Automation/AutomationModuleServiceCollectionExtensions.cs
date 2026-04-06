using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CanDoItAll.SharedKernel;
using Quartz;

namespace CanDoItAll.Modules.Automation;

public static class AutomationModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAutomationModule(this IServiceCollection services)
    {
        services.AddOptions<AutomationRuntimeOptions>()
            .BindConfiguration(AutomationRuntimeOptions.SectionName);
        services.AddQuartz(options =>
        {
            options.SchedulerId = "CanDoItAll.Automation";
            options.SchedulerName = "CanDoItAll Automation";
        });
        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
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
        services.AddHostedService<AutomationSchedulerProjectionHostedService>();
        services.AddHostedService<AutomationMessagePumpWorker>();
        services.AddHostedService<ConnectorOutboxDrainWorker>();
        services.AddHostedService<LegacyBackgroundJobQueueBridgeWorker>();
        return services;
    }
}

public static class AutomationModuleAssemblyMarker;


