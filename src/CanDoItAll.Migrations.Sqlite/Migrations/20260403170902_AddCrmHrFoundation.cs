using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmHrFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrmHr_AiAgentProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProviderProfileId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DefaultModel = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ExecutionMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    OwnerPartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CapabilityJson = table.Column<string>(type: "TEXT", nullable: false),
                    ValidationStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    LastReviewedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    ExtendedDataJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_AiAgentProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    DetailJson = table.Column<string>(type: "TEXT", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    IsSensitive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_CapacityBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BlockKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StartDateUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndDateUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Percentage = table.Column<decimal>(type: "TEXT", nullable: false),
                    RelatedProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_CapacityBlocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_ConfidentialNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    NoteText = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_ConfidentialNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_InteractionParties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InteractionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_InteractionParties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_Interactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InteractionType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    NextActionText = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    NextActionOwnerPartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    NextActionDueUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RelatedOpportunityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RelatedProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_Interactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_LookupOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CatalogKind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSystemDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_LookupOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_OnboardingTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OwnerPartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DueDateUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    RelatedProjectId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_OnboardingTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_Opportunities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Stage = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RelationshipStage = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    AccountPartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerPartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeliveryUnitPartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LinkedProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: true),
                    ProbabilityPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpectedCloseDateUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    OpportunitySource = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LostReason = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    ExtendedDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_Opportunities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_OpportunityParties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_OpportunityParties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_OpportunityStageHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Stage = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ChangedBy = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_OpportunityStageHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_Parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LifecycleStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    LegalName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PreferredName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ExternalCode = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Region = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CountryCode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    TimeZone = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    IsSensitive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExtendedDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    LastChangedBy = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_Parties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_PartyAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AddressType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Line1 = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Line2 = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Region = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CountryCode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_PartyAddresses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_PartyContactPoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContactType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    NormalizedValue = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_PartyContactPoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_PartyRelationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourcePartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetPartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RelationshipKind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartDateUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndDateUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_PartyRelationships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_PartyRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleKind = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    ValidFromUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ValidToUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_PartyRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_PartySkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SkillId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Proficiency = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    YearsExperience = table.Column<int>(type: "INTEGER", nullable: false),
                    CertificationStatus = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    LastValidatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_PartySkills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_ProjectPartyAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssignmentKind = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    NodeKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    PhaseName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    OpportunityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AllocationPercent = table.Column<decimal>(type: "TEXT", nullable: true),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_ProjectPartyAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_RecruitmentApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetUnitPartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RecruiterPartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    HiringManagerPartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DesiredRole = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Stage = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AvailableFromUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Decision = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_RecruitmentApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_RecruitmentInterviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScheduledAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    InterviewType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    InterviewerPartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Feedback = table.Column<string>(type: "TEXT", nullable: false),
                    Recommendation = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_RecruitmentInterviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_Skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_Skills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_StaffingRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RequestedByPartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeliveryUnitPartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NeededRole = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    NeededSkillsJson = table.Column<string>(type: "TEXT", nullable: false),
                    StartDateUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndDateUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    AllocationPercent = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_StaffingRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_WorkforceProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkforceKind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EmployeeCode = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    JobTitle = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Discipline = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Seniority = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    HomeUnitPartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ManagerPartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StartDateUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndDateUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    TimeZone = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    InternalCostRate = table.Column<decimal>(type: "TEXT", nullable: true),
                    ExternalBillingRate = table.Column<decimal>(type: "TEXT", nullable: true),
                    CapacityHoursPerWeek = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ExtendedDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_WorkforceProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AiAgentProfiles_PartyId",
                table: "CrmHr_AiAgentProfiles",
                column: "PartyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AiAgentProfiles_ProviderProfileId",
                table: "CrmHr_AiAgentProfiles",
                column: "ProviderProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AuditEntries_EntityType_EntityId",
                table: "CrmHr_AuditEntries",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_CapacityBlocks_PartyId_StartDateUtc_EndDateUtc",
                table: "CrmHr_CapacityBlocks",
                columns: new[] { "PartyId", "StartDateUtc", "EndDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_ConfidentialNotes_PartyId",
                table: "CrmHr_ConfidentialNotes",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_InteractionParties_InteractionId_PartyId_Role",
                table: "CrmHr_InteractionParties",
                columns: new[] { "InteractionId", "PartyId", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Interactions_RelatedOpportunityId",
                table: "CrmHr_Interactions",
                column: "RelatedOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Interactions_RelatedProjectId",
                table: "CrmHr_Interactions",
                column: "RelatedProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_LookupOptions_CatalogKind_Key",
                table: "CrmHr_LookupOptions",
                columns: new[] { "CatalogKind", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_OnboardingTasks_PartyId_TaskKind_Status",
                table: "CrmHr_OnboardingTasks",
                columns: new[] { "PartyId", "TaskKind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Opportunities_AccountPartyId",
                table: "CrmHr_Opportunities",
                column: "AccountPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Opportunities_LinkedProjectId",
                table: "CrmHr_Opportunities",
                column: "LinkedProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Opportunities_OwnerPartyId",
                table: "CrmHr_Opportunities",
                column: "OwnerPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Opportunities_Stage",
                table: "CrmHr_Opportunities",
                column: "Stage");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_OpportunityParties_OpportunityId_PartyId_Role",
                table: "CrmHr_OpportunityParties",
                columns: new[] { "OpportunityId", "PartyId", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_OpportunityStageHistory_OpportunityId_ChangedAtUtc",
                table: "CrmHr_OpportunityStageHistory",
                columns: new[] { "OpportunityId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Parties_DisplayName",
                table: "CrmHr_Parties",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Parties_ExternalCode",
                table: "CrmHr_Parties",
                column: "ExternalCode");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Parties_PartyType_LifecycleStatus",
                table: "CrmHr_Parties",
                columns: new[] { "PartyType", "LifecycleStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartyAddresses_PartyId_IsPrimary",
                table: "CrmHr_PartyAddresses",
                columns: new[] { "PartyId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartyContactPoints_NormalizedValue",
                table: "CrmHr_PartyContactPoints",
                column: "NormalizedValue");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartyContactPoints_PartyId_IsPrimary",
                table: "CrmHr_PartyContactPoints",
                columns: new[] { "PartyId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartyRelationships_SourcePartyId_TargetPartyId_RelationshipKind",
                table: "CrmHr_PartyRelationships",
                columns: new[] { "SourcePartyId", "TargetPartyId", "RelationshipKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartyRelationships_TargetPartyId",
                table: "CrmHr_PartyRelationships",
                column: "TargetPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartyRoles_PartyId_RoleKind",
                table: "CrmHr_PartyRoles",
                columns: new[] { "PartyId", "RoleKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartySkills_PartyId_SkillId",
                table: "CrmHr_PartySkills",
                columns: new[] { "PartyId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_ProjectPartyAssignments_OpportunityId",
                table: "CrmHr_ProjectPartyAssignments",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_ProjectPartyAssignments_PartyId",
                table: "CrmHr_ProjectPartyAssignments",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_ProjectPartyAssignments_ProjectId",
                table: "CrmHr_ProjectPartyAssignments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_ProjectPartyAssignments_ProjectId_PartyId_AssignmentKind_NodeKey",
                table: "CrmHr_ProjectPartyAssignments",
                columns: new[] { "ProjectId", "PartyId", "AssignmentKind", "NodeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_RecruitmentApplications_PartyId_Stage",
                table: "CrmHr_RecruitmentApplications",
                columns: new[] { "PartyId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_RecruitmentInterviews_ApplicationId_ScheduledAtUtc",
                table: "CrmHr_RecruitmentInterviews",
                columns: new[] { "ApplicationId", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Skills_Name",
                table: "CrmHr_Skills",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_StaffingRequests_DeliveryUnitPartyId",
                table: "CrmHr_StaffingRequests",
                column: "DeliveryUnitPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_StaffingRequests_ProjectId",
                table: "CrmHr_StaffingRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_WorkforceProfiles_HomeUnitPartyId",
                table: "CrmHr_WorkforceProfiles",
                column: "HomeUnitPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_WorkforceProfiles_ManagerPartyId",
                table: "CrmHr_WorkforceProfiles",
                column: "ManagerPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_WorkforceProfiles_PartyId",
                table: "CrmHr_WorkforceProfiles",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_WorkforceProfiles_Status",
                table: "CrmHr_WorkforceProfiles",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrmHr_AiAgentProfiles");

            migrationBuilder.DropTable(
                name: "CrmHr_AuditEntries");

            migrationBuilder.DropTable(
                name: "CrmHr_CapacityBlocks");

            migrationBuilder.DropTable(
                name: "CrmHr_ConfidentialNotes");

            migrationBuilder.DropTable(
                name: "CrmHr_InteractionParties");

            migrationBuilder.DropTable(
                name: "CrmHr_Interactions");

            migrationBuilder.DropTable(
                name: "CrmHr_LookupOptions");

            migrationBuilder.DropTable(
                name: "CrmHr_OnboardingTasks");

            migrationBuilder.DropTable(
                name: "CrmHr_Opportunities");

            migrationBuilder.DropTable(
                name: "CrmHr_OpportunityParties");

            migrationBuilder.DropTable(
                name: "CrmHr_OpportunityStageHistory");

            migrationBuilder.DropTable(
                name: "CrmHr_Parties");

            migrationBuilder.DropTable(
                name: "CrmHr_PartyAddresses");

            migrationBuilder.DropTable(
                name: "CrmHr_PartyContactPoints");

            migrationBuilder.DropTable(
                name: "CrmHr_PartyRelationships");

            migrationBuilder.DropTable(
                name: "CrmHr_PartyRoles");

            migrationBuilder.DropTable(
                name: "CrmHr_PartySkills");

            migrationBuilder.DropTable(
                name: "CrmHr_ProjectPartyAssignments");

            migrationBuilder.DropTable(
                name: "CrmHr_RecruitmentApplications");

            migrationBuilder.DropTable(
                name: "CrmHr_RecruitmentInterviews");

            migrationBuilder.DropTable(
                name: "CrmHr_Skills");

            migrationBuilder.DropTable(
                name: "CrmHr_StaffingRequests");

            migrationBuilder.DropTable(
                name: "CrmHr_WorkforceProfiles");
        }
    }
}
