using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Memory.Application;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.CrmHr;

public static class CrmHrModuleServiceCollectionExtensions
{
    public static IServiceCollection AddCrmHrModule(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectTransferTargetStateParticipant,
            CrmHrProjectTransferTargetStateParticipant>());
        services.AddScoped<PartyDirectoryService>();
        services.AddScoped<PartyDirectoryManagementService>();
        services.AddScoped<ICrmPartyCommandService, CrmPartyCommandService>();
        services.AddScoped<IPartyOrganizationAffiliationService, PartyOrganizationAffiliationService>();
        services.AddScoped<IPartyRecordQueryService, PartyRecordQueryService>();
        services.AddScoped<IWorkforceRecordQueryService, WorkforceRecordQueryService>();
        services.AddScoped<ICrmHrHomeQueryService, CrmHrHomeQueryService>();
        services.AddScoped<CrmService>();
        services.AddScoped<IOpportunityPipelineQueryService, OpportunityPipelineQueryService>();
        services.AddScoped<ICrmFinancialSnapshotQueryService, CrmFinancialSnapshotQueryService>();
        services.AddScoped<HrService>();
        services.AddScoped<RecruitingService>();
        services.AddScoped<AiAgentService>();
        services.AddScoped<IAiAgentDirectoryQueryService, AiAgentDirectoryQueryService>();
        services.AddScoped<ICrmHrAgentQueryService, CrmHrAgentQueryService>();
        services.AddScoped<ProjectPartyAssignmentNodePolicy>();
        services.AddScoped<ProjectPartyAffiliationContextService>();
        services.AddScoped<ProjectPartyIntegrationService>();
        services.AddScoped<ICrmHrSourceSnapshotProvider, CrmHrSourceSnapshotProvider>();
        services.AddMemorySourceGatewayAdapter<CrmHrMemorySourceGatewayAdapter>();
        services.AddScoped<IAutomationSignalSource, CrmHrAutomationSignalProvider>();
        services.AddScoped<IProjectPartyIntegrationBridge>(serviceProvider => serviceProvider.GetRequiredService<ProjectPartyIntegrationService>());
        services.AddScoped<IProjectPartyCostRateBridge>(serviceProvider => serviceProvider.GetRequiredService<ProjectPartyIntegrationService>());
        services.TryAddScoped<IAiTechnicalAgentBridge, LegacyAiTechnicalAgentBridge>();
        return services;
    }
}

public static class CrmHrModuleAssemblyMarker;
