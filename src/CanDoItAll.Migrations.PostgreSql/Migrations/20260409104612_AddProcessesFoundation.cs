using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessesFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Processes_ArtifactExpectations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    TrustRequirement = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SensitivityLevel = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    AllowedFutureUsageSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ValidationRequirementSummary = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_ArtifactExpectations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_ArtifactRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArtifactKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TrustStatus = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SensitivityLevel = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ProvenanceSummary = table.Column<string>(type: "TEXT", nullable: false),
                    AllowedFutureUsageSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ManagedStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExternalReferenceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_ArtifactRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_ConformanceObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Severity = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Observation = table.Column<string>(type: "TEXT", nullable: false),
                    DeviationReason = table.Column<string>(type: "TEXT", nullable: false),
                    IsSafeNonAction = table.Column<bool>(type: "boolean", nullable: false),
                    ContainsSensitiveAssessment = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_ConformanceObservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_DecisionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    PolicyEvaluation = table.Column<string>(type: "TEXT", nullable: false),
                    DecidedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OperatingMode = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_DecisionRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_Definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ValueStatement = table.Column<string>(type: "TEXT", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InterfaceContractSummary = table.Column<string>(type: "TEXT", nullable: false),
                    GovernanceNotes = table.Column<string>(type: "TEXT", nullable: false),
                    Criticality = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    AutonomyLevel = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ActivePublishedVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_Definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_DefinitionVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ChangeSummary = table.Column<string>(type: "TEXT", nullable: false),
                    GovernancePolicySummary = table.Column<string>(type: "TEXT", nullable: false),
                    ConstitutionRuleSummary = table.Column<string>(type: "TEXT", nullable: false),
                    OperatingModeSummary = table.Column<string>(type: "TEXT", nullable: false),
                    SimulationReadinessSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ImportedFrom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImportWarnings = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_DefinitionVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_ImprovementCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProblemSummary = table.Column<string>(type: "TEXT", nullable: false),
                    EvidenceSummary = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    IsTrainingOpportunity = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresGovernanceReview = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_ImprovementCandidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_JournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OperatingMode = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EnvironmentMode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ReplayContextJson = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_JournalEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_RoleRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", nullable: false),
                    StaffingIntent = table.Column<string>(type: "TEXT", nullable: false),
                    PreferredExecutorKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PreferredProjectAssignmentRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsFallback = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresExplicitApproval = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultAllocationPercent = table.Column<int>(type: "integer", nullable: false),
                    RoleTemplateSourceKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RoleTemplateSnapshotName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SnapshotSummary = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_RoleRequirements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_RoleSkillRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumYearsExperience = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_RoleSkillRequirements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_RunAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExecutorKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BindingReason = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRegistryKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SnapshotSummary = table.Column<string>(type: "TEXT", nullable: false),
                    IsFallback = table.Column<bool>(type: "boolean", nullable: false),
                    IsCapabilityGap = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_RunAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_Runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    OperatingMode = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    TriggerReason = table.Column<string>(type: "TEXT", nullable: false),
                    GovernanceSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    PolicySnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    ExecutorSnapshotSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ReplayPackageKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric", nullable: false),
                    ActualCost = table.Column<decimal>(type: "numeric", nullable: false),
                    FirstTimeRightPercent = table.Column<int>(type: "integer", nullable: false),
                    SlaAttainmentPercent = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_Runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_StepDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subtitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    StepKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    AllowsManualSkip = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsSafeRefusal = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresDecisionRecord = table.Column<bool>(type: "boolean", nullable: false),
                    InputContractSummary = table.Column<string>(type: "TEXT", nullable: false),
                    OutputContractSummary = table.Column<string>(type: "TEXT", nullable: false),
                    EvidenceContractSummary = table.Column<string>(type: "TEXT", nullable: false),
                    DecisionRightsSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ExceptionPolicySummary = table.Column<string>(type: "TEXT", nullable: false),
                    TargetLeadHours = table.Column<int>(type: "integer", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    DependsOnStepId = table.Column<Guid>(type: "uuid", nullable: true),
                    CanvasX = table.Column<double>(type: "double precision", nullable: false),
                    CanvasY = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_StepRoleRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsibilityKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    FallbackOrder = table.Column<int>(type: "integer", nullable: false),
                    RebindPolicySummary = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepRoleRequirements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_StepRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StepKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    RoleSnapshotSummary = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentExecutorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CurrentExecutorPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionSummary = table.Column<string>(type: "TEXT", nullable: false),
                    BlockedReason = table.Column<string>(type: "TEXT", nullable: false),
                    RefusalReason = table.Column<string>(type: "TEXT", nullable: false),
                    ExceptionSummary = table.Column<string>(type: "TEXT", nullable: false),
                    InputQualitySummary = table.Column<string>(type: "TEXT", nullable: false),
                    ReadyAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WaitMinutes = table.Column<int>(type: "integer", nullable: false),
                    TouchMinutes = table.Column<int>(type: "integer", nullable: false),
                    BlockedMinutes = table.Column<int>(type: "integer", nullable: false),
                    ReworkCount = table.Column<int>(type: "integer", nullable: false),
                    CapabilityGapSeverity = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_WorkBriefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WorkBriefText = table.Column<string>(type: "TEXT", nullable: false),
                    HandoffSummary = table.Column<string>(type: "TEXT", nullable: false),
                    AssignmentReason = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedOutcome = table.Column<string>(type: "TEXT", nullable: false),
                    EvidenceExpectationSummary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_WorkBriefs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ArtifactExpectations_StepDefinitionId",
                table: "Processes_ArtifactExpectations",
                column: "StepDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ArtifactRecords_ProcessRunId",
                table: "Processes_ArtifactRecords",
                column: "ProcessRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ArtifactRecords_StepRunId",
                table: "Processes_ArtifactRecords",
                column: "StepRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ConformanceObservations_ProcessRunId",
                table: "Processes_ConformanceObservations",
                column: "ProcessRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ConformanceObservations_StepRunId",
                table: "Processes_ConformanceObservations",
                column: "StepRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_DecisionRecords_ProcessRunId_CreatedAtUtc",
                table: "Processes_DecisionRecords",
                columns: new[] { "ProcessRunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_DecisionRecords_StepRunId",
                table: "Processes_DecisionRecords",
                column: "StepRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Definitions_ProjectId",
                table: "Processes_Definitions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Definitions_Slug",
                table: "Processes_Definitions",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Definitions_Status",
                table: "Processes_Definitions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_DefinitionVersions_ProcessDefinitionId_Status",
                table: "Processes_DefinitionVersions",
                columns: new[] { "ProcessDefinitionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_DefinitionVersions_ProcessDefinitionId_VersionNum~",
                table: "Processes_DefinitionVersions",
                columns: new[] { "ProcessDefinitionId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ImprovementCandidates_ProcessDefinitionId",
                table: "Processes_ImprovementCandidates",
                column: "ProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ImprovementCandidates_ProcessRunId",
                table: "Processes_ImprovementCandidates",
                column: "ProcessRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ImprovementCandidates_Status",
                table: "Processes_ImprovementCandidates",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_JournalEntries_ProcessRunId_OccurredAtUtc",
                table: "Processes_JournalEntries",
                columns: new[] { "ProcessRunId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_JournalEntries_StepRunId",
                table: "Processes_JournalEntries",
                column: "StepRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleRequirements_ProcessDefinitionVersionId_Key",
                table: "Processes_RoleRequirements",
                columns: new[] { "ProcessDefinitionVersionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleSkillRequirements_RoleRequirementId_SkillId",
                table: "Processes_RoleSkillRequirements",
                columns: new[] { "RoleRequirementId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleSkillRequirements_SkillId",
                table: "Processes_RoleSkillRequirements",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RunAssignments_PartyId",
                table: "Processes_RunAssignments",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RunAssignments_ProcessRunId_RoleRequirementId_Ste~",
                table: "Processes_RunAssignments",
                columns: new[] { "ProcessRunId", "RoleRequirementId", "StepDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_ProcessDefinitionId",
                table: "Processes_Runs",
                column: "ProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_ProjectId",
                table: "Processes_Runs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_Status",
                table: "Processes_Runs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDefinitions_DependsOnStepId",
                table: "Processes_StepDefinitions",
                column: "DependsOnStepId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDefinitions_ProcessDefinitionVersionId_Key",
                table: "Processes_StepDefinitions",
                columns: new[] { "ProcessDefinitionVersionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDefinitions_ProcessDefinitionVersionId_OrderI~",
                table: "Processes_StepDefinitions",
                columns: new[] { "ProcessDefinitionVersionId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepRoleRequirements_StepDefinitionId_RoleRequire~",
                table: "Processes_StepRoleRequirements",
                columns: new[] { "StepDefinitionId", "RoleRequirementId", "ResponsibilityKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepRuns_ProcessRunId_Sequence",
                table: "Processes_StepRuns",
                columns: new[] { "ProcessRunId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepRuns_ProcessRunId_Status",
                table: "Processes_StepRuns",
                columns: new[] { "ProcessRunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepRuns_StepDefinitionId",
                table: "Processes_StepRuns",
                column: "StepDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_WorkBriefs_ProcessRunId",
                table: "Processes_WorkBriefs",
                column: "ProcessRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_WorkBriefs_StepRunId",
                table: "Processes_WorkBriefs",
                column: "StepRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Processes_ArtifactExpectations");

            migrationBuilder.DropTable(
                name: "Processes_ArtifactRecords");

            migrationBuilder.DropTable(
                name: "Processes_ConformanceObservations");

            migrationBuilder.DropTable(
                name: "Processes_DecisionRecords");

            migrationBuilder.DropTable(
                name: "Processes_Definitions");

            migrationBuilder.DropTable(
                name: "Processes_DefinitionVersions");

            migrationBuilder.DropTable(
                name: "Processes_ImprovementCandidates");

            migrationBuilder.DropTable(
                name: "Processes_JournalEntries");

            migrationBuilder.DropTable(
                name: "Processes_RoleRequirements");

            migrationBuilder.DropTable(
                name: "Processes_RoleSkillRequirements");

            migrationBuilder.DropTable(
                name: "Processes_RunAssignments");

            migrationBuilder.DropTable(
                name: "Processes_Runs");

            migrationBuilder.DropTable(
                name: "Processes_StepDefinitions");

            migrationBuilder.DropTable(
                name: "Processes_StepRoleRequirements");

            migrationBuilder.DropTable(
                name: "Processes_StepRuns");

            migrationBuilder.DropTable(
                name: "Processes_WorkBriefs");
        }
    }
}
