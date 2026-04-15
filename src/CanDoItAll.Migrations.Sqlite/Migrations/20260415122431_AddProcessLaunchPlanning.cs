using System;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260415122431_AddProcessLaunchPlanning")]
    public partial class AddProcessLaunchPlanning : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Processes_LaunchPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessDefinitionVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OperatingMode = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    TriggerReason = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    RecommendationStrategy = table.Column<string>(type: "TEXT", nullable: false),
                    FallbackStrategy = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ApprovalThreadId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LatestApprovalRecordId = table.Column<Guid>(type: "TEXT", nullable: true),
                    GeneratedRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RequestedBy = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ExecutedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_LaunchPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchPlans_Processes_DefinitionVersions_ProcessDefinitionId_ProcessDefinitionVersionId",
                        columns: x => new { x.ProcessDefinitionId, x.ProcessDefinitionVersionId },
                        principalTable: "Processes_DefinitionVersions",
                        principalColumns: new[] { "ProcessDefinitionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchPlans_Processes_Definitions_ProcessDefinitionId",
                        column: x => x.ProcessDefinitionId,
                        principalTable: "Processes_Definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchPlans_Processes_Runs_GeneratedRunId",
                        column: x => x.GeneratedRunId,
                        principalTable: "Processes_Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Processes_LaunchApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LaunchPlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    ApproverPartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ApproverDisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ApproverKind = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    HumanSubstitutePartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    HumanSubstituteName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CollaborationThreadId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RequestMessage = table.Column<string>(type: "TEXT", nullable: false),
                    ResolutionSummary = table.Column<string>(type: "TEXT", nullable: false),
                    DecidedBy = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_LaunchApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchApprovals_Processes_LaunchPlans_LaunchPlanId",
                        column: x => x.LaunchPlanId,
                        principalTable: "Processes_LaunchPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_LaunchPlanRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LaunchPlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleRequirementId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PreferredExecutorKind = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    RequiredSkillIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RecommendationSummary = table.Column<string>(type: "TEXT", nullable: false),
                    SelectionSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ReadinessSummary = table.Column<string>(type: "TEXT", nullable: false),
                    SelectedCandidateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresExplicitApproval = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresProvisioning = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsResolved = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_LaunchPlanRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchPlanRoles_Processes_LaunchPlans_LaunchPlanId",
                        column: x => x.LaunchPlanId,
                        principalTable: "Processes_LaunchPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchPlanRoles_Processes_RoleRequirements_RoleRequirementId",
                        column: x => x.RoleRequirementId,
                        principalTable: "Processes_RoleRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Processes_LaunchCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LaunchPlanRoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CandidateKind = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TechnicalAgentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ExecutorKind = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Score = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsRecommended = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowsDirectMessaging = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresProvisioning = table.Column<bool>(type: "INTEGER", nullable: false),
                    RecommendationSummary = table.Column<string>(type: "TEXT", nullable: false),
                    AvailabilitySummary = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRegistryKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_LaunchCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchCandidates_Processes_LaunchPlanRoles_LaunchPlanRoleId",
                        column: x => x.LaunchPlanRoleId,
                        principalTable: "Processes_LaunchPlanRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_LaunchProvisioningRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LaunchPlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LaunchPlanRoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SelectedCandidateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    RequestKind = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RequestPayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResultPartyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResultTechnicalAgentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResultSummary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_LaunchProvisioningRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchProvisioningRequests_Processes_LaunchCandidates_SelectedCandidateId",
                        column: x => x.SelectedCandidateId,
                        principalTable: "Processes_LaunchCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchProvisioningRequests_Processes_LaunchPlanRoles_LaunchPlanRoleId",
                        column: x => x.LaunchPlanRoleId,
                        principalTable: "Processes_LaunchPlanRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchProvisioningRequests_Processes_LaunchPlans_LaunchPlanId",
                        column: x => x.LaunchPlanId,
                        principalTable: "Processes_LaunchPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_Processes_LaunchPlans_ProcessDefinitionId_ProcessDefinitionVersionId",
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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Processes_LaunchApprovals");

            migrationBuilder.DropTable(
                name: "Processes_LaunchProvisioningRequests");

            migrationBuilder.DropTable(
                name: "Processes_LaunchCandidates");

            migrationBuilder.DropTable(
                name: "Processes_LaunchPlanRoles");

            migrationBuilder.DropTable(
                name: "Processes_LaunchPlans");
        }
    }
}
