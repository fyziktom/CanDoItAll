using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CrmHr;

public sealed class CrmHrDbContext(DbContextOptions<CrmHrDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyConfiguration(new AiResourceBindingConfiguration());
        modelBuilder.ApplyConfiguration(new AiTechnicalProjectionCursorConfiguration());
        modelBuilder.ApplyConfiguration(new InteractionRecordConfiguration());
        modelBuilder.ApplyConfiguration(new InteractionPartyLinkConfiguration());
        modelBuilder.ApplyConfiguration(new CrmAccountProfileConfiguration());
        modelBuilder.ApplyConfiguration(new CrmAccountConnectionConfiguration());
        modelBuilder.ApplyConfiguration(new CrmAccountConnectionProjectLinkConfiguration());
        modelBuilder.ApplyConfiguration(new OpportunityConfiguration());
        modelBuilder.ApplyConfiguration(new OpportunityPartyLinkConfiguration());
        modelBuilder.ApplyConfiguration(new OpportunityStageHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new WorkforceProfileConfiguration());
        modelBuilder.ApplyConfiguration(new SkillDefinitionConfiguration());
        modelBuilder.ApplyConfiguration(new PartySkillConfiguration());
        modelBuilder.ApplyConfiguration(new CapacityBlockConfiguration());
        modelBuilder.ApplyConfiguration(new StaffingRequestConfiguration());
        modelBuilder.ApplyConfiguration(new RecruitmentApplicationConfiguration());
        modelBuilder.ApplyConfiguration(new RecruitmentInterviewConfiguration());
        modelBuilder.ApplyConfiguration(new OnboardingTaskConfiguration());
        modelBuilder.ApplyConfiguration(new AiAgentProfileConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectPartyAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectPartyAssignmentMoveReceiptConfiguration());
        modelBuilder.ApplyConfiguration(new PartyConfiguration());
        modelBuilder.ApplyConfiguration(new PartyRoleAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new PartyContactPointConfiguration());
        modelBuilder.ApplyConfiguration(new PartyAddressConfiguration());
        modelBuilder.ApplyConfiguration(new PartyRelationshipConfiguration());
        modelBuilder.ApplyConfiguration(new PartyConfidentialNoteConfiguration());
        modelBuilder.ApplyConfiguration(new CrmHrAuditEntryConfiguration());
        modelBuilder.ApplyConfiguration(new CrmHrLookupOptionConfiguration());
        modelBuilder.ApplyConfiguration(new PartyOrganizationAffiliationConfiguration());
        modelBuilder.Ignore<Project>();
        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) {
        ApplicationManagedConcurrencyTokens.Stamp(ChangeTracker);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) {
        ApplicationManagedConcurrencyTokens.Stamp(ChangeTracker);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
