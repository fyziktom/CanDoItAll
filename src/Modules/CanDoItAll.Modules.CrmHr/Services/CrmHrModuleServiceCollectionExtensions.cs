using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Memory.Application;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.CrmHr;

public static class CrmHrModuleServiceCollectionExtensions
{
    public static IServiceCollection AddCrmHrModule(this IServiceCollection services) {
        services.AddPooledDbContextFactory<CrmHrDbContext>((provider, options) => {
            AppDbContextOptionsConfigurator.Configure(options, provider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        });
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
        services.TryAddSingleton<AgentToolPolicyCatalog>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAgentRuntimeToolProvider, CrmPlanningAgentRuntimeToolProvider>());
        foreach (var policy in CrmPlanningToolPolicy.Capabilities) {
            if (!services.Any(descriptor => ReferenceEquals(descriptor.ImplementationInstance, policy))) {
                services.AddSingleton(policy);
            }
        }
        services.AddScoped<ProjectPartyAssignmentNodePolicy>();
        services.AddScoped<ProjectPartyAffiliationContextService>();
        services.AddScoped<IProjectWorkAssignmentPartyFacts, ProjectWorkAssignmentPartyFacts>();
        services.AddScoped<ProjectPartyIntegrationService>();
        services.AddScoped<IProjectPartyDeletionStateQuery>(provider => provider.GetRequiredService<ProjectPartyIntegrationService>());
        services.AddScoped<ICrmHrSourceSnapshotProvider, CrmHrSourceSnapshotProvider>();
        services.AddMemorySourceGatewayAdapter<CrmHrMemorySourceGatewayAdapter>();
        services.AddScoped<IAutomationSignalSource, CrmHrAutomationSignalProvider>();
        services.AddScoped<IProjectPartyIntegrationBridge>(serviceProvider => serviceProvider.GetRequiredService<ProjectPartyIntegrationService>());
        services.AddScoped<IProjectPartyCostRateBridge>(serviceProvider => serviceProvider.GetRequiredService<ProjectPartyIntegrationService>());
        services.TryAddScoped<IAiTechnicalAgentProjectionStore, AiTechnicalAgentProjectionStore>();
        services.TryAddScoped<IAiTechnicalAgentBridge, LegacyAiTechnicalAgentBridge>();
        return services;
    }
}

public static class CrmHrModuleAssemblyMarker;
