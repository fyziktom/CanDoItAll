using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CrmHr;

public enum InteractionType
{
    Meeting,
    Call,
    Email,
    Message,
    Note
}

public enum InteractionPartyRole
{
    Author,
    Account,
    Contact,
    Attendee,
    Recipient,
    Stakeholder
}

public enum CrmAccountRelationshipStage
{
    Prospect,
    ActiveCustomer,
    DormantCustomer,
    LostCustomer
}

public enum CrmAccountConnectionRole
{
    PrimaryContact,
    Stakeholder,
    BillingContact,
    ContractContact,
    AccountManager,
    DeliveryLead,
    Sponsor,
    TechnicalContact
}

public enum OpportunityStage
{
    Identified,
    Qualified,
    Proposal,
    Negotiation,
    Won,
    Lost
}

public enum OpportunitySource
{
    Direct,
    Partner,
    Renewal,
    Upsell
}

public enum OpportunityPartyRole
{
    Customer,
    Partner,
    Sponsor,
    TechnicalContact,
    BillingContact,
    DeliveryLead,
    Stakeholder
}

public enum WorkforceKind
{
    Employee,
    Contractor,
    Freelancer,
    DeliveryUnit
}

public enum SkillProficiencyLevel
{
    Basic,
    Working,
    Strong,
    Expert
}

public enum CapacityBlockKind
{
    Leave,
    Unavailable,
    Reserve,
    Tentative
}

public enum StaffingRequestStatus
{
    Draft,
    Open,
    Proposed,
    Confirmed,
    Closed,
    Cancelled
}

public enum RecruitmentStage
{
    Applied,
    Screening,
    Interviewing,
    Offer,
    Hired,
    Rejected,
    Withdrawn
}

public enum RecruitmentDecision
{
    Pending,
    Approved,
    Rejected,
    Withdrawn
}

public enum RecruitmentInterviewType
{
    Screening,
    Technical,
    Manager,
    Panel,
    Culture
}

public enum RecruitmentInterviewOutcome
{
    Pending,
    StrongYes,
    Yes,
    Mixed,
    No,
    StrongNo
}

public enum LifecycleTaskKind
{
    Onboarding,
    Offboarding,
    Training
}

public enum LifecycleTaskStatus
{
    NotStarted,
    InProgress,
    Completed,
    Cancelled
}

public enum AiExecutionMode
{
    Local,
    Remote,
    ThirdParty
}

public enum AiValidationStatus
{
    Draft,
    ReviewRequired,
    Approved,
    Suspended
}

public enum ProjectPartyAssignmentKind
{
    Customer,
    CustomerContact,
    DeliveryUnit,
    TeamMember,
    Manager,
    Partner,
    Vendor,
    Stakeholder,
    MeetingParticipant,
    WorkItemAssignee,
    Reviewer,
    AiAgent,
    BillingContact,
    TechnicalContact
}

public sealed class InteractionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public InteractionType InteractionType { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string NextActionText { get; set; } = string.Empty;
    public Guid? NextActionOwnerPartyId { get; set; }
    public DateTimeOffset? NextActionDueUtc { get; set; }
    public Guid? RelatedOpportunityId { get; set; }
    public Guid? RelatedProjectId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class InteractionPartyLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InteractionId { get; set; }
    public Guid PartyId { get; set; }
    public InteractionPartyRole Role { get; set; }
}

public sealed class CrmAccountProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountPartyId { get; set; }
    public CrmAccountRelationshipStage RelationshipStage { get; set; } = CrmAccountRelationshipStage.Prospect;
    public string CommercialNotes { get; set; } = string.Empty;
    public string ConstraintNotes { get; set; } = string.Empty;
    public string TimingRiskNotes { get; set; } = string.Empty;
    public string LastChangedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class CrmAccountConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountPartyId { get; set; }
    public Guid RelatedPartyId { get; set; }
    public CrmAccountConnectionRole Role { get; set; } = CrmAccountConnectionRole.Stakeholder;
    public bool IsPrimary { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class CrmAccountConnectionProjectLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountConnectionId { get; set; }
    public Guid ProjectId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class Opportunity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public OpportunityStage Stage { get; set; } = OpportunityStage.Identified;
    public string RelationshipStage { get; set; } = string.Empty;
    public Guid AccountPartyId { get; set; }
    public Guid OwnerPartyId { get; set; }
    public Guid? DeliveryUnitPartyId { get; set; }
    public Guid? LinkedProjectId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public int ProbabilityPercent { get; set; }
    public DateTimeOffset? ExpectedCloseDateUtc { get; set; }
    public OpportunitySource OpportunitySource { get; set; } = OpportunitySource.Direct;
    public string LostReason { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string ExtendedDataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class OpportunityPartyLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OpportunityId { get; set; }
    public Guid PartyId { get; set; }
    public OpportunityPartyRole Role { get; set; }
}

public sealed class OpportunityStageHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OpportunityId { get; set; }
    public OpportunityStage Stage { get; set; }
    public DateTimeOffset ChangedAtUtc { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public decimal? RecognizedAmount { get; set; }
    public string RecognizedCurrencyCode { get; set; } = string.Empty;
}

public sealed class WorkforceProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartyId { get; set; }
    public WorkforceKind WorkforceKind { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Discipline { get; set; } = string.Empty;
    public string Seniority { get; set; } = string.Empty;
    public Guid? HomeUnitPartyId { get; set; }
    public Guid? ManagerPartyId { get; set; }
    public DateTimeOffset? StartDateUtc { get; set; }
    public DateTimeOffset? EndDateUtc { get; set; }
    public string Location { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public decimal? InternalCostRate { get; set; }
    public decimal? ExternalBillingRate { get; set; }
    public ProjectResourceRateUnit RateUnit { get; set; } = ProjectResourceRateUnit.Hour;
    public string RateCurrencyCode { get; set; } = "USD";
    public decimal CapacityHoursPerWeek { get; set; } = 40m;
    public string Status { get; set; } = string.Empty;
    public string ExtendedDataJson { get; set; } = "{}";
    public string Notes { get; set; } = string.Empty;
}

public sealed class SkillDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class PartySkill
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartyId { get; set; }
    public Guid SkillId { get; set; }
    public SkillProficiencyLevel Proficiency { get; set; } = SkillProficiencyLevel.Basic;
    public int YearsExperience { get; set; }
    public string CertificationStatus { get; set; } = string.Empty;
    public DateTimeOffset? LastValidatedAtUtc { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class CapacityBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartyId { get; set; }
    public CapacityBlockKind BlockKind { get; set; }
    public DateTimeOffset StartDateUtc { get; set; }
    public DateTimeOffset EndDateUtc { get; set; }
    public decimal Percentage { get; set; }
    public Guid? RelatedProjectId { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class StaffingRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ProjectId { get; set; }
    public Guid? RequestedByPartyId { get; set; }
    public Guid? DeliveryUnitPartyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string NeededRole { get; set; } = string.Empty;
    public string NeededSkillsJson { get; set; } = "[]";
    public DateTimeOffset? StartDateUtc { get; set; }
    public DateTimeOffset? EndDateUtc { get; set; }
    public decimal AllocationPercent { get; set; }
    public StaffingRequestStatus Status { get; set; } = StaffingRequestStatus.Draft;
    public string Notes { get; set; } = string.Empty;
}

public sealed class RecruitmentApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartyId { get; set; }
    public Guid? TargetUnitPartyId { get; set; }
    public Guid? RecruiterPartyId { get; set; }
    public Guid? HiringManagerPartyId { get; set; }
    public string DesiredRole { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public RecruitmentStage Stage { get; set; } = RecruitmentStage.Applied;
    public DateTimeOffset? AvailableFromUtc { get; set; }
    public RecruitmentDecision Decision { get; set; } = RecruitmentDecision.Pending;
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class RecruitmentInterview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public DateTimeOffset ScheduledAtUtc { get; set; }
    public RecruitmentInterviewType InterviewType { get; set; }
    public Guid? InterviewerPartyId { get; set; }
    public RecruitmentInterviewOutcome Outcome { get; set; } = RecruitmentInterviewOutcome.Pending;
    public string Feedback { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

public sealed class OnboardingTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartyId { get; set; }
    public LifecycleTaskKind TaskKind { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? OwnerPartyId { get; set; }
    public DateTimeOffset? DueDateUtc { get; set; }
    public LifecycleTaskStatus Status { get; set; } = LifecycleTaskStatus.NotStarted;
    public string Notes { get; set; } = string.Empty;
    public Guid? RelatedProjectId { get; set; }
}

public sealed class AiAgentProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartyId { get; set; }
    public Guid? ProviderProfileId { get; set; }
    public string DefaultModel { get; set; } = string.Empty;
    public AiExecutionMode ExecutionMode { get; set; } = AiExecutionMode.Remote;
    public Guid? OwnerPartyId { get; set; }
    public string CapabilityJson { get; set; } = "[]";
    public AiValidationStatus ValidationStatus { get; set; } = AiValidationStatus.Draft;
    public DateTimeOffset? LastReviewedAtUtc { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string ExtendedDataJson { get; set; } = "{}";
}

public sealed class ProjectPartyAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid PartyId { get; set; }
    public Guid? PartyOrganizationAffiliationId { get; set; }
    public ProjectPartyAssignmentKind AssignmentKind { get; set; }
    public string NodeKey { get; set; } = string.Empty;
    public string PhaseName { get; set; } = string.Empty;
    public Guid? OpportunityId { get; set; }
    public decimal? AllocationPercent { get; set; }
    public DateTimeOffset? StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public bool IsPrimary { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class ProjectPartyAssignmentMoveReceipt
{
    public Guid OperationId { get; set; }
    public Guid SourceProjectId { get; set; }
    public Guid TargetProjectId { get; set; }
    public string NodeSetFingerprint { get; set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; set; }
}

internal sealed class InteractionRecordConfiguration : IEntityTypeConfiguration<InteractionRecord>
{
    public void Configure(EntityTypeBuilder<InteractionRecord> builder)
    {
        builder.ToTable("CrmHr_Interactions");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.InteractionType).HasConversion<string>().HasMaxLength(64);
        builder.Property(record => record.Subject).HasMaxLength(200).IsRequired();
        builder.Property(record => record.Summary).HasColumnType("TEXT");
        builder.Property(record => record.Notes).HasColumnType("TEXT");
        builder.Property(record => record.NextActionText).HasMaxLength(240);
        builder.HasIndex(record => record.RelatedOpportunityId);
        builder.HasIndex(record => record.RelatedProjectId);
    }
}

internal sealed class InteractionPartyLinkConfiguration : IEntityTypeConfiguration<InteractionPartyLink>
{
    public void Configure(EntityTypeBuilder<InteractionPartyLink> builder)
    {
        builder.ToTable("CrmHr_InteractionParties");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.Role).HasConversion<string>().HasMaxLength(64);
        builder.HasIndex(link => new { link.InteractionId, link.PartyId, link.Role });
        builder.HasIndex(link => new { link.PartyId, link.Role, link.InteractionId });
    }
}

internal sealed class CrmAccountProfileConfiguration : IEntityTypeConfiguration<CrmAccountProfile>
{
    public void Configure(EntityTypeBuilder<CrmAccountProfile> builder)
    {
        builder.ToTable("CrmHr_AccountProfiles");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.RelationshipStage).HasConversion<string>().HasMaxLength(64);
        builder.Property(profile => profile.CommercialNotes).HasColumnType("TEXT");
        builder.Property(profile => profile.ConstraintNotes).HasColumnType("TEXT");
        builder.Property(profile => profile.TimingRiskNotes).HasColumnType("TEXT");
        builder.Property(profile => profile.LastChangedBy).HasMaxLength(160);
        builder.HasIndex(profile => profile.AccountPartyId).IsUnique();
    }
}

internal sealed class CrmAccountConnectionConfiguration : IEntityTypeConfiguration<CrmAccountConnection>
{
    public void Configure(EntityTypeBuilder<CrmAccountConnection> builder)
    {
        builder.ToTable("CrmHr_AccountStakeholders");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.Role).HasConversion<string>().HasMaxLength(64);
        builder.Property(link => link.Notes).HasColumnType("TEXT");
        builder.HasIndex(link => new { link.AccountPartyId, link.RelatedPartyId, link.Role }).IsUnique();
        builder.HasIndex(link => link.RelatedPartyId);
    }
}

internal sealed class CrmAccountConnectionProjectLinkConfiguration
    : IEntityTypeConfiguration<CrmAccountConnectionProjectLink>
{
    public void Configure(EntityTypeBuilder<CrmAccountConnectionProjectLink> builder)
    {
        builder.ToTable("CrmHr_AccountConnectionProjects");
        builder.HasKey(link => link.Id);
        builder.HasIndex(link => new { link.AccountConnectionId, link.ProjectId }).IsUnique();
        builder.HasIndex(link => new { link.ProjectId, link.AccountConnectionId });
        builder.HasOne<CrmAccountConnection>()
            .WithMany()
            .HasForeignKey(link => link.AccountConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(link => link.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder.ToTable("CrmHr_Opportunities");
        builder.HasKey(opportunity => opportunity.Id);
        builder.Property(opportunity => opportunity.Title).HasMaxLength(200).IsRequired();
        builder.Property(opportunity => opportunity.Stage).HasConversion<string>().HasMaxLength(64);
        builder.Property(opportunity => opportunity.RelationshipStage).HasMaxLength(80);
        builder.Property(opportunity => opportunity.CurrencyCode).HasMaxLength(16);
        builder.Property(opportunity => opportunity.OpportunitySource).HasConversion<string>().HasMaxLength(64);
        builder.Property(opportunity => opportunity.LostReason).HasMaxLength(240);
        builder.Property(opportunity => opportunity.Summary).HasColumnType("TEXT");
        builder.Property(opportunity => opportunity.Notes).HasColumnType("TEXT");
        builder.Property(opportunity => opportunity.ExtendedDataJson).HasColumnType("TEXT");
        builder.Property(opportunity => opportunity.UpdatedAtUtc).IsConcurrencyToken();
        builder.HasIndex(opportunity => opportunity.Stage);
        builder.HasIndex(opportunity => opportunity.OwnerPartyId);
        builder.HasIndex(opportunity => new { opportunity.AccountPartyId, opportunity.Stage });
        builder.HasIndex(opportunity => new
        {
            opportunity.AccountPartyId,
            opportunity.UpdatedAtUtc,
            opportunity.Id
        }).IsDescending(false, true, false);
        builder.HasIndex(opportunity => opportunity.LinkedProjectId);
    }
}

internal sealed class OpportunityPartyLinkConfiguration : IEntityTypeConfiguration<OpportunityPartyLink>
{
    public void Configure(EntityTypeBuilder<OpportunityPartyLink> builder)
    {
        builder.ToTable("CrmHr_OpportunityParties");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.Role).HasConversion<string>().HasMaxLength(64);
        builder.HasIndex(link => new { link.OpportunityId, link.PartyId, link.Role });
    }
}

internal sealed class OpportunityStageHistoryConfiguration : IEntityTypeConfiguration<OpportunityStageHistory>
{
    public void Configure(EntityTypeBuilder<OpportunityStageHistory> builder)
    {
        builder.ToTable("CrmHr_OpportunityStageHistory");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Stage).HasConversion<string>().HasMaxLength(64);
        builder.Property(entry => entry.ChangedBy).HasMaxLength(160);
        builder.Property(entry => entry.Notes).HasColumnType("TEXT");
        builder.Property(entry => entry.RecognizedCurrencyCode).HasMaxLength(3).IsRequired();
        builder.HasIndex(entry => new { entry.OpportunityId, entry.ChangedAtUtc });
    }
}

internal sealed class WorkforceProfileConfiguration : IEntityTypeConfiguration<WorkforceProfile>
{
    public void Configure(EntityTypeBuilder<WorkforceProfile> builder)
    {
        builder.ToTable("CrmHr_WorkforceProfiles");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.WorkforceKind).HasConversion<string>().HasMaxLength(64);
        builder.Property(profile => profile.EmployeeCode).HasMaxLength(80);
        builder.Property(profile => profile.JobTitle).HasMaxLength(160);
        builder.Property(profile => profile.Discipline).HasMaxLength(120);
        builder.Property(profile => profile.Seniority).HasMaxLength(80);
        builder.Property(profile => profile.Location).HasMaxLength(160);
        builder.Property(profile => profile.TimeZone).HasMaxLength(80);
        builder.Property(profile => profile.RateUnit)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(ProjectResourceRateUnit.Hour);
        builder.Property(profile => profile.RateCurrencyCode)
            .HasMaxLength(3)
            .HasDefaultValue("USD")
            .IsRequired();
        builder.Property(profile => profile.Status).HasMaxLength(80);
        builder.Property(profile => profile.ExtendedDataJson).HasColumnType("TEXT");
        builder.Property(profile => profile.Notes).HasColumnType("TEXT");
        builder.HasIndex(profile => profile.PartyId).IsUnique();
        builder.HasIndex(profile => profile.HomeUnitPartyId);
        builder.HasIndex(profile => profile.ManagerPartyId);
        builder.HasIndex(profile => profile.Status);
    }
}

internal sealed class SkillDefinitionConfiguration : IEntityTypeConfiguration<SkillDefinition>
{
    public void Configure(EntityTypeBuilder<SkillDefinition> builder)
    {
        builder.ToTable("CrmHr_Skills");
        builder.HasKey(skill => skill.Id);
        builder.Property(skill => skill.Name).HasMaxLength(160).IsRequired();
        builder.Property(skill => skill.Category).HasMaxLength(120);
        builder.Property(skill => skill.Description).HasColumnType("TEXT");
        builder.HasIndex(skill => skill.Name).IsUnique();
    }
}

internal sealed class PartySkillConfiguration : IEntityTypeConfiguration<PartySkill>
{
    public void Configure(EntityTypeBuilder<PartySkill> builder)
    {
        builder.ToTable("CrmHr_PartySkills");
        builder.HasKey(skill => skill.Id);
        builder.Property(skill => skill.Proficiency).HasConversion<string>().HasMaxLength(32);
        builder.Property(skill => skill.CertificationStatus).HasMaxLength(120);
        builder.Property(skill => skill.Notes).HasColumnType("TEXT");
        builder.HasIndex(skill => new { skill.PartyId, skill.SkillId }).IsUnique();
    }
}

internal sealed class CapacityBlockConfiguration : IEntityTypeConfiguration<CapacityBlock>
{
    public void Configure(EntityTypeBuilder<CapacityBlock> builder)
    {
        builder.ToTable("CrmHr_CapacityBlocks");
        builder.HasKey(block => block.Id);
        builder.Property(block => block.BlockKind).HasConversion<string>().HasMaxLength(32);
        builder.Property(block => block.Notes).HasColumnType("TEXT");
        builder.HasIndex(block => new { block.PartyId, block.StartDateUtc, block.EndDateUtc });
    }
}

internal sealed class StaffingRequestConfiguration : IEntityTypeConfiguration<StaffingRequest>
{
    public void Configure(EntityTypeBuilder<StaffingRequest> builder)
    {
        builder.ToTable("CrmHr_StaffingRequests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Title).HasMaxLength(200).IsRequired();
        builder.Property(request => request.NeededRole).HasMaxLength(160);
        builder.Property(request => request.NeededSkillsJson).HasColumnType("TEXT");
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(request => request.Notes).HasColumnType("TEXT");
        builder.HasIndex(request => request.ProjectId);
        builder.HasIndex(request => request.DeliveryUnitPartyId);
    }
}

internal sealed class RecruitmentApplicationConfiguration : IEntityTypeConfiguration<RecruitmentApplication>
{
    public void Configure(EntityTypeBuilder<RecruitmentApplication> builder)
    {
        builder.ToTable("CrmHr_RecruitmentApplications");
        builder.HasKey(application => application.Id);
        builder.Property(application => application.DesiredRole).HasMaxLength(160);
        builder.Property(application => application.Source).HasMaxLength(120);
        builder.Property(application => application.Stage).HasConversion<string>().HasMaxLength(32);
        builder.Property(application => application.Decision).HasConversion<string>().HasMaxLength(32);
        builder.Property(application => application.Notes).HasColumnType("TEXT");
        builder.HasIndex(application => new { application.PartyId, application.Stage });
    }
}

internal sealed class RecruitmentInterviewConfiguration : IEntityTypeConfiguration<RecruitmentInterview>
{
    public void Configure(EntityTypeBuilder<RecruitmentInterview> builder)
    {
        builder.ToTable("CrmHr_RecruitmentInterviews");
        builder.HasKey(interview => interview.Id);
        builder.Property(interview => interview.InterviewType).HasConversion<string>().HasMaxLength(32);
        builder.Property(interview => interview.Outcome).HasConversion<string>().HasMaxLength(32);
        builder.Property(interview => interview.Feedback).HasColumnType("TEXT");
        builder.Property(interview => interview.Recommendation).HasColumnType("TEXT");
        builder.HasIndex(interview => new { interview.ApplicationId, interview.ScheduledAtUtc });
    }
}

internal sealed class OnboardingTaskConfiguration : IEntityTypeConfiguration<OnboardingTask>
{
    public void Configure(EntityTypeBuilder<OnboardingTask> builder)
    {
        builder.ToTable("CrmHr_OnboardingTasks");
        builder.HasKey(task => task.Id);
        builder.Property(task => task.TaskKind).HasConversion<string>().HasMaxLength(32);
        builder.Property(task => task.Title).HasMaxLength(200).IsRequired();
        builder.Property(task => task.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(task => task.Notes).HasColumnType("TEXT");
        builder.HasIndex(task => new { task.PartyId, task.TaskKind, task.Status });
    }
}

internal sealed class AiAgentProfileConfiguration : IEntityTypeConfiguration<AiAgentProfile>
{
    public void Configure(EntityTypeBuilder<AiAgentProfile> builder)
    {
        builder.ToTable("CrmHr_AiAgentProfiles");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.DefaultModel).HasMaxLength(160);
        builder.Property(profile => profile.ExecutionMode).HasConversion<string>().HasMaxLength(32);
        builder.Property(profile => profile.CapabilityJson).HasColumnType("TEXT");
        builder.Property(profile => profile.ValidationStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(profile => profile.Notes).HasColumnType("TEXT");
        builder.Property(profile => profile.ExtendedDataJson).HasColumnType("TEXT");
        builder.HasIndex(profile => profile.PartyId).IsUnique();
        builder.HasIndex(profile => profile.ProviderProfileId);
    }
}

internal sealed class ProjectPartyAssignmentConfiguration : IEntityTypeConfiguration<ProjectPartyAssignment>
{
    public void Configure(EntityTypeBuilder<ProjectPartyAssignment> builder)
    {
        builder.ToTable("CrmHr_ProjectPartyAssignments");
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.AssignmentKind).HasConversion<string>().HasMaxLength(48);
        builder.Property(assignment => assignment.NodeKey).HasMaxLength(160);
        builder.Property(assignment => assignment.PhaseName).HasMaxLength(160);
        builder.Property(assignment => assignment.Source).HasMaxLength(80);
        builder.Property(assignment => assignment.Notes).HasColumnType("TEXT");
        builder.HasIndex(assignment => new { assignment.ProjectId, assignment.PartyId, assignment.AssignmentKind, assignment.NodeKey });
        builder.HasIndex(assignment => new { assignment.ProjectId, assignment.AssignmentKind, assignment.NodeKey });
        builder.HasIndex(assignment => assignment.ProjectId);
        builder.HasIndex(assignment => assignment.PartyId);
        builder.HasIndex(assignment => assignment.PartyOrganizationAffiliationId);
        builder.HasIndex(assignment => assignment.OpportunityId);
        builder.HasOne<PartyOrganizationAffiliation>()
            .WithMany()
            .HasForeignKey(assignment => assignment.PartyOrganizationAffiliationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProjectPartyAssignmentMoveReceiptConfiguration
    : IEntityTypeConfiguration<ProjectPartyAssignmentMoveReceipt>
{
    public void Configure(EntityTypeBuilder<ProjectPartyAssignmentMoveReceipt> builder)
    {
        builder.ToTable("CrmHr_ProjectPartyAssignmentMoveReceipts");
        builder.HasKey(receipt => receipt.OperationId);
        builder.Property(receipt => receipt.NodeSetFingerprint)
            .HasMaxLength(64)
            .IsRequired();
        builder.HasIndex(receipt => new
        {
            receipt.SourceProjectId,
            receipt.TargetProjectId,
            receipt.CompletedAtUtc
        });
    }
}
