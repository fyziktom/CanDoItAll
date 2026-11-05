using CanDoItAll.Memory.SourceGateway;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Memory.Application;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Modules.CrmHr;

public static class CrmHrModuleServiceCollectionExtensions
{
    public static IServiceCollection AddCrmHrModule(this IServiceCollection services) {
        services.TryAddSingleton<EditingChangeFeed>();
        services.TryAddScoped<EditingOperationContext>();
        services.TryAddScoped<IEditingUiAccess, UnavailableEditingUiAccess>();
        services.TryAddScoped(provider => {
            var database = provider.GetRequiredService<ICanonicalRuntimeDatabase>();
            return new CrmHrChanges(provider.GetRequiredService<EditingChangeFeed>(),
                new(EditingScopeKind.Dataset, database.Profile.Profile.Id, database.Generation),
                provider.GetRequiredService<EditingOperationContext>());
        });
        services.AddPooledDbContextFactory<CrmHrDbContext>((provider, options) => {
            AppDbContextOptionsConfigurator.Configure(options, provider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        });
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IProjectTransferTargetStateParticipant,
            CrmHrProjectTransferTargetStateParticipant>());
        services.AddScoped<PartyDirectoryService>();
        services.AddScoped<PartyRelationshipService>();
        services.AddScoped<IPartyRelationshipService>(provider => provider.GetRequiredService<PartyRelationshipService>());
        services.AddScoped<PartyDirectoryManagementService>();
        services.AddScoped<PartyMergePreflight>();
        services.AddScoped<ICrmPartyCommandService, CrmPartyCommandService>();
        services.AddScoped<IPartyOrganizationAffiliationService, PartyOrganizationAffiliationService>();
        services.AddScoped<IPartyOrganizationAffiliationReader>(provider => provider.GetRequiredService<IPartyOrganizationAffiliationService>());
        services.AddScoped<IPartyRecordQueryService, PartyRecordQueryService>();
        services.AddScoped<IWorkforceRecordQueryService, WorkforceRecordQueryService>();
        services.AddScoped<ICrmHrHomeQueryService, CrmHrHomeQueryService>();
        services.AddScoped<CrmService>();
        services.AddScoped<ICrmOpportunityEditorService>(provider => provider.GetRequiredService<CrmService>());
        services.AddScoped<ICrmOpportunityConversionService>(provider => provider.GetRequiredService<CrmService>());
        services.AddScoped<IOpportunityPipelineQueryService, OpportunityPipelineQueryService>();
        services.AddScoped<ICrmFinancialSnapshotQueryService, CrmFinancialSnapshotQueryService>();
        services.AddScoped<HrService>();
        services.AddScoped<IStaffingRequestEditing, StaffingRequestEditingService>();
        services.AddScoped<WorkforceProfileEditingService>();
        services.AddScoped<IWorkforceProfileEditing>(services => services.GetRequiredService<WorkforceProfileEditingService>());
        services.AddScoped<WorkforceSkillEditingService>();
        services.AddScoped<WorkforceCapacityEditingService>();
        services.AddScoped<WorkforceSearchProjection>();
        services.AddScoped<RecruitmentApplicationEditingService>();
        services.AddScoped<IRecruitmentApplicationEditing>(services => services.GetRequiredService<RecruitmentApplicationEditingService>());
        services.AddScoped<RecruitmentInterviewEditingService>();
        services.AddScoped<RecruitmentLifecycleEditingService>();
        services.AddScoped<RecruitmentSearchProjection>();
        services.AddScoped<RecruitmentConversionService>();
        services.AddScoped<RecruitingService>();
        services.AddScoped<AiAgentService>();
        services.AddScoped<AiStewardProfileEditingService>();
        services.AddScoped<IAiStewardProfileEditing>(provider => provider.GetRequiredService<AiStewardProfileEditingService>());
        services.AddScoped<AiAgentSearchProjection>();
        services.AddScoped<IAiAgentCreationGuard, AiAgentCreationGuard>();
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
        // CRM/HR is the owner behind the Projects party bridges: replace the Projects no-op defaults whatever the order.
        services.Replace(ServiceDescriptor.Scoped<IProjectPartyIntegrationBridge>(serviceProvider => serviceProvider.GetRequiredService<ProjectPartyIntegrationService>()));
        services.Replace(ServiceDescriptor.Scoped<IProjectPartyCostRateBridge>(serviceProvider => serviceProvider.GetRequiredService<ProjectPartyIntegrationService>()));
        services.TryAddScoped<IAiTechnicalAgentProjectionStore, AiTechnicalAgentProjectionStore>();
        services.TryAddScoped<IAiTechnicalAgentBridge, LegacyAiTechnicalAgentBridge>();
        return services;
    }
}

public static class CrmHrModuleAssemblyMarker;
