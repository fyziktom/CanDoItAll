using Microsoft.Extensions.DependencyInjection;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.CrmHr;

public static class CrmHrModuleServiceCollectionExtensions
{
    public static IServiceCollection AddCrmHrModule(this IServiceCollection services)
    {
        services.AddScoped<PartyDirectoryService>();
        services.AddScoped<PartyDirectoryManagementService>();
        services.AddScoped<CrmService>();
        services.AddScoped<HrService>();
        services.AddScoped<RecruitingService>();
        services.AddScoped<AiAgentService>();
        services.AddScoped<ProjectPartyAssignmentNodePolicy>();
        services.AddScoped<ProjectPartyIntegrationService>();
        services.AddScoped<IAutomationSignalSource, CrmHrAutomationSignalProvider>();
        services.AddScoped<IProjectPartyIntegrationBridge>(serviceProvider => serviceProvider.GetRequiredService<ProjectPartyIntegrationService>());
        return services;
    }
}

public static class CrmHrModuleAssemblyMarker;
