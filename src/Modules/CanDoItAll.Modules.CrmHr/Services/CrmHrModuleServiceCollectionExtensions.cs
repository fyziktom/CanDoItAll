using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Memory.Application;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.AddScoped<ICrmHrAgentQueryService, CrmHrAgentQueryService>();
        services.AddScoped<ProjectPartyAssignmentNodePolicy>();
        services.AddScoped<ProjectPartyIntegrationService>();
        services.AddScoped<ICrmHrSourceSnapshotProvider, CrmHrSourceSnapshotProvider>();
        services.AddMemorySourceGatewayAdapter<CrmHrMemorySourceGatewayAdapter>();
        services.AddScoped<IAutomationSignalSource, CrmHrAutomationSignalProvider>();
        services.AddScoped<IProjectPartyIntegrationBridge>(serviceProvider => serviceProvider.GetRequiredService<ProjectPartyIntegrationService>());
        services.TryAddScoped<IAiTechnicalAgentBridge, LegacyAiTechnicalAgentBridge>();
        return services;
    }
}

public static class CrmHrModuleAssemblyMarker;
