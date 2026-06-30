using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ProcessModuleArchitectureV3RuntimePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepBranchOutcomes_Processes_StepDefinitions_Step~",
                table: "Processes_StepBranchOutcomes");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepRuns_Processes_StepDefinitions_StepDefinition~",
                table: "Processes_StepRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepRuns_Processes_Runs_ProcessRunId",
                table: "Processes_StepRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_Definitions_Processes_DefinitionVersions_Id_Activ~",
                table: "Processes_Definitions");

            migrationBuilder.DropTable(
                name: "Processes_ArtifactRecords");

            migrationBuilder.DropTable(
                name: "Processes_ConformanceObservations");

            migrationBuilder.DropTable(
                name: "Processes_DecisionRecords");

            migrationBuilder.DropTable(
                name: "Processes_ImprovementCandidates");

            migrationBuilder.DropTable(
                name: "Processes_JournalEntries");

            migrationBuilder.DropTable(
                name: "Processes_LaunchApprovals");

            migrationBuilder.DropTable(
                name: "Processes_LaunchProvisioningRequests");

            migrationBuilder.DropTable(
                name: "Processes_Outbox");

            migrationBuilder.DropTable(
                name: "Processes_RoleMessagingPolicies");

            migrationBuilder.DropTable(
                name: "Processes_RoleSkillRequirements");

            migrationBuilder.DropTable(
                name: "Processes_StepArtifactInputs");

            migrationBuilder.DropTable(
                name: "Processes_StepDependencies");

            migrationBuilder.DropTable(
                name: "Processes_StepRoleRequirements");

            migrationBuilder.DropTable(
                name: "Processes_VerificationAuditRecords");

            migrationBuilder.DropTable(
                name: "Processes_WorkBriefs");

            migrationBuilder.DropTable(
                name: "Processes_WorkflowRunLinks");

            migrationBuilder.DropTable(
                name: "Processes_LaunchCandidates");

            migrationBuilder.DropTable(
                name: "Processes_ArtifactExpectations");

            migrationBuilder.DropTable(
                name: "Processes_RunAssignments");

            migrationBuilder.DropTable(
                name: "Processes_LaunchPlanRoles");

            migrationBuilder.DropTable(
                name: "Processes_LaunchPlans");

            migrationBuilder.DropTable(
                name: "Processes_StepDefinitions");

            migrationBuilder.DropTable(
                name: "Processes_RoleRequirements");

            migrationBuilder.DropTable(
                name: "Processes_Runs");

            migrationBuilder.DropTable(
                name: "Processes_StepRuns");

            migrationBuilder.DropTable(
                name: "Processes_StepBranchOutcomes");

            migrationBuilder.DropTable(
                name: "Processes_DefinitionVersions");

            migrationBuilder.DropTable(
                name: "Processes_Definitions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Processes_Outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CommandKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_VerificationAuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedCount = table.Column<int>(type: "integer", nullable: false),
                    AllowsFinalizerMutation = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsProcessMutation = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsTransitionMutation = table.Column<bool>(type: "boolean", nullable: false),
                    DeniedCount = table.Column<int>(type: "integer", nullable: false),
                    Lane = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NoMutationPerformed = table.Column<bool>(type: "boolean", nullable: false),
                    ObservationHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ResponseCount = table.Column<int>(type: "integer", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_VerificationAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_ArtifactExpectations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowedFutureUsageSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ArtifactKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    SensitivityLevel = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubprocessChildArtifactExpectationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubprocessChildArtifactTitle = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SubprocessChildStepKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TrustRequirement = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ValidationRequirementSummary = table.Column<string>(type: "TEXT", nullable: false),
                    WorkflowOutputId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    WorkflowOutputKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    WorkflowOutputName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false)
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
                    AllowedFutureUsageSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ArtifactExpectationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArtifactKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExternalReferenceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ManagedStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectionIdentityHash = table.Column<string>(type: "character varying(95)", maxLength: 95, nullable: false),
                    ProjectionLineageJson = table.Column<string>(type: "TEXT", nullable: false),
                    ProvenanceSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewSummary = table.Column<string>(type: "TEXT", nullable: false),
                    SensitivityLevel = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TrustStatus = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_ArtifactRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_ArtifactRecords_Processes_ArtifactExpectations_Ar~",
                        column: x => x.ArtifactExpectationId,
                        principalTable: "Processes_ArtifactExpectations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Processes_ConformanceObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ContainsSensitiveAssessment = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeviationReason = table.Column<string>(type: "TEXT", nullable: false),
                    IsSafeNonAction = table.Column<bool>(type: "boolean", nullable: false),
                    Observation = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Severity = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: true)
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
                    BranchOutcomeId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchOutcomeTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DecisionKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    OperatingMode = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    PolicyEvaluation = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
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
                    ActivePublishedVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AutonomyLevel = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Criticality = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GovernanceNotes = table.Column<string>(type: "TEXT", nullable: false),
                    InterfaceContractSummary = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NextVersionNumber = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    OwnerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValueStatement = table.Column<string>(type: "TEXT", nullable: false)
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
                    ChangeSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    ConstitutionRuleSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ContractMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GovernancePolicySummary = table.Column<string>(type: "TEXT", nullable: false),
                    ImportWarnings = table.Column<string>(type: "TEXT", nullable: false),
                    ImportedFrom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ManagerAgentOverrideId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManagerAgentOverrideName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OperatingModeSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SimulationReadinessSummary = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_DefinitionVersions", x => x.Id);
                    table.UniqueConstraint("AK_Processes_DefinitionVersions_ProcessDefinitionId_Id", x => new { x.ProcessDefinitionId, x.Id });
                    table.ForeignKey(
                        name: "FK_Processes_DefinitionVersions_Processes_Definitions_ProcessD~",
                        column: x => x.ProcessDefinitionId,
                        principalTable: "Processes_Definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_RoleRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowsFallback = table.Column<bool>(type: "boolean", nullable: false),
                    CanvasX = table.Column<double>(type: "double precision", nullable: false),
                    CanvasY = table.Column<double>(type: "double precision", nullable: false),
                    DefaultAllocationPercent = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PreferredExecutorKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PreferredProjectAssignmentRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PreferredWorkflowDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreferredWorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", nullable: false),
                    RequiresExplicitApproval = table.Column<bool>(type: "boolean", nullable: false),
                    RoleTemplateSnapshotName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RoleTemplateSourceKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SnapshotSummary = table.Column<string>(type: "TEXT", nullable: false),
                    StaffingIntent = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_RoleRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_RoleRequirements_Processes_DefinitionVersions_Pro~",
                        column: x => x.ProcessDefinitionVersionId,
                        principalTable: "Processes_DefinitionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_RoleMessagingPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    ProcessDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceRoleRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetRoleRequirementId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_RoleMessagingPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_RoleMessagingPolicies_Processes_DefinitionVersion~",
                        column: x => x.ProcessDefinitionVersionId,
                        principalTable: "Processes_DefinitionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Processes_RoleMessagingPolicies_Processes_RoleRequirements_~",
                        column: x => x.SourceRoleRequirementId,
                        principalTable: "Processes_RoleRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_RoleMessagingPolicies_Processes_RoleRequirements~1",
                        column: x => x.TargetRoleRequirementId,
                        principalTable: "Processes_RoleRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Processes_RoleSkillRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumYearsExperience = table.Column<int>(type: "integer", nullable: false),
                    RoleRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_RoleSkillRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_RoleSkillRequirements_Processes_RoleRequirements_~",
                        column: x => x.RoleRequirementId,
                        principalTable: "Processes_RoleRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_StepDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowedOperations = table.Column<string>(type: "TEXT", nullable: false),
                    AllowsManualSkip = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsSafeRefusal = table.Column<bool>(type: "boolean", nullable: false),
                    BranchCanvasX = table.Column<double>(type: "double precision", nullable: false),
                    BranchCanvasY = table.Column<double>(type: "double precision", nullable: false),
                    CanvasX = table.Column<double>(type: "double precision", nullable: false),
                    CanvasY = table.Column<double>(type: "double precision", nullable: false),
                    DecisionRightsSummary = table.Column<string>(type: "TEXT", nullable: false),
                    DecisionRoleRequirementId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceContractSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ExceptionPolicySummary = table.Column<string>(type: "TEXT", nullable: false),
                    InputContractSummary = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    OperationTargetScope = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    OutputContractSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresDecisionRecord = table.Column<bool>(type: "boolean", nullable: false),
                    StepKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SubprocessDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubprocessDefinitionSnapshotName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subtitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetLeadHours = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_StepDefinitions_Processes_DefinitionVersions_Proc~",
                        column: x => x.ProcessDefinitionVersionId,
                        principalTable: "Processes_DefinitionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Processes_StepDefinitions_Processes_Definitions_SubprocessD~",
                        column: x => x.SubprocessDefinitionId,
                        principalTable: "Processes_Definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_StepDefinitions_Processes_RoleRequirements_Decisi~",
                        column: x => x.DecisionRoleRequirementId,
                        principalTable: "Processes_RoleRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Processes_StepArtifactInputs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactExpectationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepArtifactInputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_StepArtifactInputs_Processes_ArtifactExpectations~",
                        column: x => x.ArtifactExpectationId,
                        principalTable: "Processes_ArtifactExpectations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_StepArtifactInputs_Processes_StepDefinitions_Step~",
                        column: x => x.StepDefinitionId,
                        principalTable: "Processes_StepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_StepBranchOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepBranchOutcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_StepBranchOutcomes_Processes_StepDefinitions_Step~",
                        column: x => x.StepDefinitionId,
                        principalTable: "Processes_StepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_StepRoleRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FallbackOrder = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    RebindPolicySummary = table.Column<string>(type: "TEXT", nullable: false),
                    ResponsibilityKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    RoleRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepRoleRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_StepRoleRequirements_Processes_RoleRequirements_R~",
                        column: x => x.RoleRequirementId,
                        principalTable: "Processes_RoleRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_StepRoleRequirements_Processes_StepDefinitions_St~",
                        column: x => x.StepDefinitionId,
                        principalTable: "Processes_StepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_StepDependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DependsOnBranchOutcomeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DependsOnStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_StepDependencies_Processes_StepBranchOutcomes_Dep~",
                        column: x => x.DependsOnBranchOutcomeId,
                        principalTable: "Processes_StepBranchOutcomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_StepDependencies_Processes_StepDefinitions_Depend~",
                        column: x => x.DependsOnStepId,
                        principalTable: "Processes_StepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_StepDependencies_Processes_StepDefinitions_StepDe~",
                        column: x => x.StepDefinitionId,
                        principalTable: "Processes_StepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_ImprovementCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EvidenceSummary = table.Column<string>(type: "TEXT", nullable: false),
                    IsTrainingOpportunity = table.Column<bool>(type: "boolean", nullable: false),
                    ProblemSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequiresGovernanceReview = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_ImprovementCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_ImprovementCandidates_Processes_Definitions_Proce~",
                        column: x => x.ProcessDefinitionId,
                        principalTable: "Processes_Definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_JournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    EnvironmentMode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OperatingMode = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplayContextJson = table.Column<string>(type: "TEXT", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_JournalEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_LaunchApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproverDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApproverKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ApproverPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollaborationThreadId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    HumanSubstituteName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HumanSubstitutePartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    LaunchPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestMessage = table.Column<string>(type: "TEXT", nullable: false),
                    ResolutionSummary = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_LaunchApprovals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_LaunchCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowsDirectMessaging = table.Column<bool>(type: "boolean", nullable: false),
                    AvailabilitySummary = table.Column<string>(type: "TEXT", nullable: false),
                    CandidateKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExecutorKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsRecommended = table.Column<bool>(type: "boolean", nullable: false),
                    LaunchPlanRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecommendationSummary = table.Column<string>(type: "TEXT", nullable: false),
                    RequiresProvisioning = table.Column<bool>(type: "boolean", nullable: false),
                    Score = table.Column<decimal>(type: "numeric", nullable: false),
                    SourceRegistryKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TechnicalAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_LaunchCandidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_LaunchPlanRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    LaunchPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreferredExecutorKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ReadinessSummary = table.Column<string>(type: "TEXT", nullable: false),
                    RecommendationSummary = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredSkillIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RequiresExplicitApproval = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresProvisioning = table.Column<bool>(type: "boolean", nullable: false),
                    RoleKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RoleRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedCandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelectionSummary = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_LaunchPlanRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchPlanRoles_Processes_RoleRequirements_RoleRe~",
                        column: x => x.RoleRequirementId,
                        principalTable: "Processes_RoleRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Processes_LaunchPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalThreadId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExecutedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FallbackStrategy = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    LatestApprovalRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OperatingMode = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecommendationStrategy = table.Column<string>(type: "TEXT", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    TriggerReason = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_LaunchPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchPlans_Processes_DefinitionVersions_ProcessD~",
                        columns: x => new { x.ProcessDefinitionId, x.ProcessDefinitionVersionId },
                        principalTable: "Processes_DefinitionVersions",
                        principalColumns: new[] { "ProcessDefinitionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchPlans_Processes_Definitions_ProcessDefiniti~",
                        column: x => x.ProcessDefinitionId,
                        principalTable: "Processes_Definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_LaunchProvisioningRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LaunchPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    LaunchPlanRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RequestPayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResultPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ResultTechnicalAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelectedCandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_LaunchProvisioningRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchProvisioningRequests_Processes_LaunchCandid~",
                        column: x => x.SelectedCandidateId,
                        principalTable: "Processes_LaunchCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchProvisioningRequests_Processes_LaunchPlanRo~",
                        column: x => x.LaunchPlanRoleId,
                        principalTable: "Processes_LaunchPlanRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchProvisioningRequests_Processes_LaunchPlans_~",
                        column: x => x.LaunchPlanId,
                        principalTable: "Processes_LaunchPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_RunAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowsDirectMessaging = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    BindingReason = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExecutorKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsCapabilityGap = table.Column<bool>(type: "boolean", nullable: false),
                    IsFallback = table.Column<bool>(type: "boolean", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotSummary = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRegistryKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_RunAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_RunAssignments_Processes_RoleRequirements_RoleReq~",
                        column: x => x.RoleRequirementId,
                        principalTable: "Processes_RoleRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_RunAssignments_Processes_StepDefinitions_StepDefi~",
                        column: x => x.StepDefinitionId,
                        principalTable: "Processes_StepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Processes_Runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualCost = table.Column<decimal>(type: "numeric", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "numeric", nullable: false),
                    ExecutorSnapshotSummary = table.Column<string>(type: "TEXT", nullable: false),
                    FirstTimeRightPercent = table.Column<int>(type: "integer", nullable: false),
                    GovernanceSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    HierarchyDepth = table.Column<int>(type: "integer", nullable: false),
                    ManagerAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManagerAgentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OperatingMode = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ParentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentStepRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicySnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReplayPackageKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RootRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    SlaAttainmentPercent = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    TriggerReason = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_Runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_Runs_Processes_DefinitionVersions_ProcessDefiniti~",
                        columns: x => new { x.ProcessDefinitionId, x.ProcessDefinitionVersionId },
                        principalTable: "Processes_DefinitionVersions",
                        principalColumns: new[] { "ProcessDefinitionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_Runs_Processes_Definitions_ProcessDefinitionId",
                        column: x => x.ProcessDefinitionId,
                        principalTable: "Processes_Definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Processes_Runs_Processes_Runs_ParentRunId",
                        column: x => x.ParentRunId,
                        principalTable: "Processes_Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Processes_StepRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutomationDispatchAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    AutomationDispatchClaimToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AutomationDispatchClaimedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AutomationDispatchClaimedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AutomationDispatchLeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BlockReasonCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BlockedMinutes = table.Column<int>(type: "integer", nullable: false),
                    BlockedReason = table.Column<string>(type: "TEXT", nullable: false),
                    CapabilityGapSeverity = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentExecutorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CurrentExecutorPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ExceptionSummary = table.Column<string>(type: "TEXT", nullable: false),
                    InputQualitySummary = table.Column<string>(type: "TEXT", nullable: false),
                    NextRecoveryAction = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadyAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecoveryOptionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RefusalReason = table.Column<string>(type: "TEXT", nullable: false),
                    ReworkCount = table.Column<int>(type: "integer", nullable: false),
                    RoleSnapshotSummary = table.Column<string>(type: "TEXT", nullable: false),
                    SelectedBranchOutcomeId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelectedBranchOutcomeTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TouchMinutes = table.Column<int>(type: "integer", nullable: false),
                    WaitMinutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_StepRuns_Processes_Runs_ProcessRunId",
                        column: x => x.ProcessRunId,
                        principalTable: "Processes_Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Processes_StepRuns_Processes_StepBranchOutcomes_SelectedBra~",
                        column: x => x.SelectedBranchOutcomeId,
                        principalTable: "Processes_StepBranchOutcomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Processes_StepRuns_Processes_StepDefinitions_StepDefinition~",
                        column: x => x.StepDefinitionId,
                        principalTable: "Processes_StepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Processes_WorkBriefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentReason = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EvidenceExpectationSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedOutcome = table.Column<string>(type: "TEXT", nullable: false),
                    HandoffSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WorkBriefText = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_WorkBriefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_WorkBriefs_Processes_Runs_ProcessRunId",
                        column: x => x.ProcessRunId,
                        principalTable: "Processes_Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Processes_WorkBriefs_Processes_StepRuns_StepRunId",
                        column: x => x.StepRunId,
                        principalTable: "Processes_StepRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Processes_WorkflowRunLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WorkflowBackend = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    WorkflowBackendRunId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_WorkflowRunLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_WorkflowRunLinks_Processes_RunAssignments_Assignm~",
                        column: x => x.AssignmentId,
                        principalTable: "Processes_RunAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_WorkflowRunLinks_Processes_Runs_ProcessRunId",
                        column: x => x.ProcessRunId,
                        principalTable: "Processes_Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Processes_WorkflowRunLinks_Processes_StepRuns_StepRunId",
                        column: x => x.StepRunId,
                        principalTable: "Processes_StepRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ArtifactExpectations_StepDefinitionId",
                table: "Processes_ArtifactExpectations",
                column: "StepDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ArtifactExpectations_SubprocessChildArtifactExpec~",
                table: "Processes_ArtifactExpectations",
                column: "SubprocessChildArtifactExpectationId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ArtifactRecords_ArtifactExpectationId",
                table: "Processes_ArtifactRecords",
                column: "ArtifactExpectationId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ArtifactRecords_ProcessRunId",
                table: "Processes_ArtifactRecords",
                column: "ProcessRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ArtifactRecords_ProcessRunId_ProjectionIdentityHa~",
                table: "Processes_ArtifactRecords",
                columns: new[] { "ProcessRunId", "ProjectionIdentityHash" },
                unique: true,
                filter: "\"ProjectionIdentityHash\" <> ''");

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
                name: "IX_Processes_DecisionRecords_BranchOutcomeId",
                table: "Processes_DecisionRecords",
                column: "BranchOutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_DecisionRecords_ProcessRunId_CreatedAtUtc",
                table: "Processes_DecisionRecords",
                columns: new[] { "ProcessRunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_DecisionRecords_StepRunId",
                table: "Processes_DecisionRecords",
                column: "StepRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Definitions_ActivePublishedVersionId",
                table: "Processes_Definitions",
                column: "ActivePublishedVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Definitions_Id_ActivePublishedVersionId",
                table: "Processes_Definitions",
                columns: new[] { "Id", "ActivePublishedVersionId" });

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
                name: "IX_Processes_DefinitionVersions_ManagerAgentOverrideId",
                table: "Processes_DefinitionVersions",
                column: "ManagerAgentOverrideId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_DefinitionVersions_ProcessDefinitionId_VersionNum~",
                table: "Processes_DefinitionVersions",
                columns: new[] { "ProcessDefinitionId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProcessVersions_DraftPerDef",
                table: "Processes_DefinitionVersions",
                columns: new[] { "ProcessDefinitionId", "Status" },
                unique: true,
                filter: "\"Status\" = 'Draft'");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessVersions_PubPerDef",
                table: "Processes_DefinitionVersions",
                column: "ProcessDefinitionId",
                unique: true,
                filter: "\"Status\" = 'Published'");

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
                name: "IX_Processes_LaunchApprovals_CollaborationThreadId",
                table: "Processes_LaunchApprovals",
                column: "CollaborationThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchApprovals_LaunchPlanId_CreatedAtUtc",
                table: "Processes_LaunchApprovals",
                columns: new[] { "LaunchPlanId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchApprovals_Status",
                table: "Processes_LaunchApprovals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchCandidates_LaunchPlanRoleId_Score",
                table: "Processes_LaunchCandidates",
                columns: new[] { "LaunchPlanRoleId", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchCandidates_PartyId",
                table: "Processes_LaunchCandidates",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchCandidates_TechnicalAgentId",
                table: "Processes_LaunchCandidates",
                column: "TechnicalAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchCandidates_WorkflowDefinitionId",
                table: "Processes_LaunchCandidates",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchCandidates_WorkflowVersionId",
                table: "Processes_LaunchCandidates",
                column: "WorkflowVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchPlanRoles_LaunchPlanId_DisplayOrder",
                table: "Processes_LaunchPlanRoles",
                columns: new[] { "LaunchPlanId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchPlanRoles_RoleRequirementId",
                table: "Processes_LaunchPlanRoles",
                column: "RoleRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchPlanRoles_SelectedCandidateId",
                table: "Processes_LaunchPlanRoles",
                column: "SelectedCandidateId");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessLaunchPlanRoles_Role",
                table: "Processes_LaunchPlanRoles",
                columns: new[] { "LaunchPlanId", "RoleRequirementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchPlans_GeneratedRunId",
                table: "Processes_LaunchPlans",
                column: "GeneratedRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchPlans_ProcessDefinitionId_CreatedAtUtc",
                table: "Processes_LaunchPlans",
                columns: new[] { "ProcessDefinitionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchPlans_ProcessDefinitionId_ProcessDefinition~",
                table: "Processes_LaunchPlans",
                columns: new[] { "ProcessDefinitionId", "ProcessDefinitionVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchPlans_ProjectId_CreatedAtUtc",
                table: "Processes_LaunchPlans",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchPlans_Status",
                table: "Processes_LaunchPlans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchProvisioningRequests_LaunchPlanId_Status",
                table: "Processes_LaunchProvisioningRequests",
                columns: new[] { "LaunchPlanId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchProvisioningRequests_LaunchPlanRoleId",
                table: "Processes_LaunchProvisioningRequests",
                column: "LaunchPlanRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchProvisioningRequests_SelectedCandidateId",
                table: "Processes_LaunchProvisioningRequests",
                column: "SelectedCandidateId");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessLaunchProvisioning_Role",
                table: "Processes_LaunchProvisioningRequests",
                columns: new[] { "LaunchPlanId", "LaunchPlanRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Outbox_ProcessDefinitionId_CreatedAtUtc",
                table: "Processes_Outbox",
                columns: new[] { "ProcessDefinitionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Outbox_ProcessRunId_CreatedAtUtc",
                table: "Processes_Outbox",
                columns: new[] { "ProcessRunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Outbox_ProjectId_CreatedAtUtc",
                table: "Processes_Outbox",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Outbox_Status_NextAttemptAtUtc_LeaseExpiresAtUtc",
                table: "Processes_Outbox",
                columns: new[] { "Status", "NextAttemptAtUtc", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleMessagingPolicies_ProcessDefinitionVersionId_~",
                table: "Processes_RoleMessagingPolicies",
                columns: new[] { "ProcessDefinitionVersionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleMessagingPolicies_SourceRoleRequirementId",
                table: "Processes_RoleMessagingPolicies",
                column: "SourceRoleRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleMessagingPolicies_TargetRoleRequirementId",
                table: "Processes_RoleMessagingPolicies",
                column: "TargetRoleRequirementId");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessRoleMessagingPolicies_SourceTarget",
                table: "Processes_RoleMessagingPolicies",
                columns: new[] { "ProcessDefinitionVersionId", "SourceRoleRequirementId", "TargetRoleRequirementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleRequirements_PreferredWorkflowDefinitionId",
                table: "Processes_RoleRequirements",
                column: "PreferredWorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleRequirements_PreferredWorkflowVersionId",
                table: "Processes_RoleRequirements",
                column: "PreferredWorkflowVersionId");

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
                name: "IX_Processes_RunAssignments_RoleRequirementId",
                table: "Processes_RunAssignments",
                column: "RoleRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RunAssignments_StepDefinitionId",
                table: "Processes_RunAssignments",
                column: "StepDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RunAssignments_WorkflowDefinitionId",
                table: "Processes_RunAssignments",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RunAssignments_WorkflowVersionId",
                table: "Processes_RunAssignments",
                column: "WorkflowVersionId");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessRunAssignments_RunScoped",
                table: "Processes_RunAssignments",
                columns: new[] { "ProcessRunId", "RoleRequirementId" },
                unique: true,
                filter: "\"StepDefinitionId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessRunAssignments_StepScoped",
                table: "Processes_RunAssignments",
                columns: new[] { "ProcessRunId", "RoleRequirementId", "StepDefinitionId" },
                unique: true,
                filter: "\"StepDefinitionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_ManagerAgentId",
                table: "Processes_Runs",
                column: "ManagerAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_ParentRunId",
                table: "Processes_Runs",
                column: "ParentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_ProcessDefinitionId",
                table: "Processes_Runs",
                column: "ProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_ProcessDefinitionId_ProcessDefinitionVersion~",
                table: "Processes_Runs",
                columns: new[] { "ProcessDefinitionId", "ProcessDefinitionVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_ProjectId",
                table: "Processes_Runs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_RootRunId",
                table: "Processes_Runs",
                column: "RootRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_Status",
                table: "Processes_Runs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessRuns_ParentStepRun",
                table: "Processes_Runs",
                column: "ParentStepRunId",
                unique: true,
                filter: "\"ParentStepRunId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepArtifactInputs_ArtifactExpectationId",
                table: "Processes_StepArtifactInputs",
                column: "ArtifactExpectationId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepArtifactInputs_StepDefinitionId",
                table: "Processes_StepArtifactInputs",
                column: "StepDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepArtifactInputs_StepDefinitionId_ArtifactExpec~",
                table: "Processes_StepArtifactInputs",
                columns: new[] { "StepDefinitionId", "ArtifactExpectationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepArtifactInputs_StepDefinitionId_DisplayOrder",
                table: "Processes_StepArtifactInputs",
                columns: new[] { "StepDefinitionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepBranchOutcomes_StepDefinitionId_DisplayOrder",
                table: "Processes_StepBranchOutcomes",
                columns: new[] { "StepDefinitionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepBranchOutcomes_StepDefinitionId_Key",
                table: "Processes_StepBranchOutcomes",
                columns: new[] { "StepDefinitionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDefinitions_DecisionRoleRequirementId",
                table: "Processes_StepDefinitions",
                column: "DecisionRoleRequirementId");

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
                name: "IX_Processes_StepDefinitions_SubprocessDefinitionId",
                table: "Processes_StepDefinitions",
                column: "SubprocessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDependencies_DependsOnBranchOutcomeId",
                table: "Processes_StepDependencies",
                column: "DependsOnBranchOutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDependencies_DependsOnStepId",
                table: "Processes_StepDependencies",
                column: "DependsOnStepId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDependencies_StepDefinitionId",
                table: "Processes_StepDependencies",
                column: "StepDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDependencies_StepDefinitionId_DisplayOrder",
                table: "Processes_StepDependencies",
                columns: new[] { "StepDefinitionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_ProcessStepDeps_Conditional",
                table: "Processes_StepDependencies",
                columns: new[] { "StepDefinitionId", "DependsOnStepId", "DependsOnBranchOutcomeId" },
                unique: true,
                filter: "\"DependsOnBranchOutcomeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessStepDeps_Unconditional",
                table: "Processes_StepDependencies",
                columns: new[] { "StepDefinitionId", "DependsOnStepId" },
                unique: true,
                filter: "\"DependsOnBranchOutcomeId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepRoleRequirements_RoleRequirementId",
                table: "Processes_StepRoleRequirements",
                column: "RoleRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepRoleRequirements_StepDefinitionId_RoleRequire~",
                table: "Processes_StepRoleRequirements",
                columns: new[] { "StepDefinitionId", "RoleRequirementId", "ResponsibilityKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepRuns_ProcessRunId_AutomationDispatchLeaseExpi~",
                table: "Processes_StepRuns",
                columns: new[] { "ProcessRunId", "AutomationDispatchLeaseExpiresAtUtc" });

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
                name: "IX_Processes_StepRuns_SelectedBranchOutcomeId",
                table: "Processes_StepRuns",
                column: "SelectedBranchOutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepRuns_StepDefinitionId",
                table: "Processes_StepRuns",
                column: "StepDefinitionId");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessStepRuns_RunStep",
                table: "Processes_StepRuns",
                columns: new[] { "ProcessRunId", "StepDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_VerificationAuditRecords_Lane",
                table: "Processes_VerificationAuditRecords",
                column: "Lane");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_VerificationAuditRecords_ObservationHash",
                table: "Processes_VerificationAuditRecords",
                column: "ObservationHash");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_VerificationAuditRecords_ProcessRunId_RecordedAtU~",
                table: "Processes_VerificationAuditRecords",
                columns: new[] { "ProcessRunId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_VerificationAuditRecords_RecordedAtUtc",
                table: "Processes_VerificationAuditRecords",
                column: "RecordedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_VerificationAuditRecords_StepRunId_RecordedAtUtc",
                table: "Processes_VerificationAuditRecords",
                columns: new[] { "StepRunId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_WorkBriefs_ProcessRunId",
                table: "Processes_WorkBriefs",
                column: "ProcessRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_WorkBriefs_StepRunId",
                table: "Processes_WorkBriefs",
                column: "StepRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_WorkflowRunLinks_AssignmentId",
                table: "Processes_WorkflowRunLinks",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_WorkflowRunLinks_ProcessRunId",
                table: "Processes_WorkflowRunLinks",
                column: "ProcessRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_WorkflowRunLinks_StepRunId_AssignmentId",
                table: "Processes_WorkflowRunLinks",
                columns: new[] { "StepRunId", "AssignmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_WorkflowRunLinks_WorkflowDefinitionId",
                table: "Processes_WorkflowRunLinks",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_WorkflowRunLinks_WorkflowRunId",
                table: "Processes_WorkflowRunLinks",
                column: "WorkflowRunId");

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_ArtifactExpectations_Processes_StepDefinitions_St~",
                table: "Processes_ArtifactExpectations",
                column: "StepDefinitionId",
                principalTable: "Processes_StepDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_ArtifactRecords_Processes_Runs_ProcessRunId",
                table: "Processes_ArtifactRecords",
                column: "ProcessRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_ArtifactRecords_Processes_StepRuns_StepRunId",
                table: "Processes_ArtifactRecords",
                column: "StepRunId",
                principalTable: "Processes_StepRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_ConformanceObservations_Processes_Runs_ProcessRun~",
                table: "Processes_ConformanceObservations",
                column: "ProcessRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_ConformanceObservations_Processes_StepRuns_StepRu~",
                table: "Processes_ConformanceObservations",
                column: "StepRunId",
                principalTable: "Processes_StepRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_DecisionRecords_Processes_Runs_ProcessRunId",
                table: "Processes_DecisionRecords",
                column: "ProcessRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_DecisionRecords_Processes_StepBranchOutcomes_Bran~",
                table: "Processes_DecisionRecords",
                column: "BranchOutcomeId",
                principalTable: "Processes_StepBranchOutcomes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_DecisionRecords_Processes_StepRuns_StepRunId",
                table: "Processes_DecisionRecords",
                column: "StepRunId",
                principalTable: "Processes_StepRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_Definitions_Processes_DefinitionVersions_Id_Activ~",
                table: "Processes_Definitions",
                columns: new[] { "Id", "ActivePublishedVersionId" },
                principalTable: "Processes_DefinitionVersions",
                principalColumns: new[] { "ProcessDefinitionId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_ImprovementCandidates_Processes_Runs_ProcessRunId",
                table: "Processes_ImprovementCandidates",
                column: "ProcessRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_JournalEntries_Processes_Runs_ProcessRunId",
                table: "Processes_JournalEntries",
                column: "ProcessRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_JournalEntries_Processes_StepRuns_StepRunId",
                table: "Processes_JournalEntries",
                column: "StepRunId",
                principalTable: "Processes_StepRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_LaunchApprovals_Processes_LaunchPlans_LaunchPlanId",
                table: "Processes_LaunchApprovals",
                column: "LaunchPlanId",
                principalTable: "Processes_LaunchPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_LaunchCandidates_Processes_LaunchPlanRoles_Launch~",
                table: "Processes_LaunchCandidates",
                column: "LaunchPlanRoleId",
                principalTable: "Processes_LaunchPlanRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_LaunchPlanRoles_Processes_LaunchPlans_LaunchPlanId",
                table: "Processes_LaunchPlanRoles",
                column: "LaunchPlanId",
                principalTable: "Processes_LaunchPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_LaunchPlans_Processes_Runs_GeneratedRunId",
                table: "Processes_LaunchPlans",
                column: "GeneratedRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_RunAssignments_Processes_Runs_ProcessRunId",
                table: "Processes_RunAssignments",
                column: "ProcessRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_Runs_Processes_StepRuns_ParentStepRunId",
                table: "Processes_Runs",
                column: "ParentStepRunId",
                principalTable: "Processes_StepRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
