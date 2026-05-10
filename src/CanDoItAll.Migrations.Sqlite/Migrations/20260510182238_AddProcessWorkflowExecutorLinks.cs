using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessWorkflowExecutorLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowDefinitionId",
                table: "Processes_RunAssignments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowVersionId",
                table: "Processes_RunAssignments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreferredWorkflowDefinitionId",
                table: "Processes_RoleRequirements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreferredWorkflowVersionId",
                table: "Processes_RoleRequirements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowDefinitionId",
                table: "Processes_LaunchCandidates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowVersionId",
                table: "Processes_LaunchCandidates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Processes_WorkflowRunLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StepRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowVersionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkflowBackend = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    WorkflowBackendRunId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 48, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_WorkflowRunLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_WorkflowRunLinks_Processes_RunAssignments_AssignmentId",
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
                name: "IX_Processes_RunAssignments_WorkflowDefinitionId",
                table: "Processes_RunAssignments",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RunAssignments_WorkflowVersionId",
                table: "Processes_RunAssignments",
                column: "WorkflowVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleRequirements_PreferredWorkflowDefinitionId",
                table: "Processes_RoleRequirements",
                column: "PreferredWorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleRequirements_PreferredWorkflowVersionId",
                table: "Processes_RoleRequirements",
                column: "PreferredWorkflowVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchCandidates_WorkflowDefinitionId",
                table: "Processes_LaunchCandidates",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchCandidates_WorkflowVersionId",
                table: "Processes_LaunchCandidates",
                column: "WorkflowVersionId");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Processes_WorkflowRunLinks");

            migrationBuilder.DropIndex(
                name: "IX_Processes_RunAssignments_WorkflowDefinitionId",
                table: "Processes_RunAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Processes_RunAssignments_WorkflowVersionId",
                table: "Processes_RunAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Processes_RoleRequirements_PreferredWorkflowDefinitionId",
                table: "Processes_RoleRequirements");

            migrationBuilder.DropIndex(
                name: "IX_Processes_RoleRequirements_PreferredWorkflowVersionId",
                table: "Processes_RoleRequirements");

            migrationBuilder.DropIndex(
                name: "IX_Processes_LaunchCandidates_WorkflowDefinitionId",
                table: "Processes_LaunchCandidates");

            migrationBuilder.DropIndex(
                name: "IX_Processes_LaunchCandidates_WorkflowVersionId",
                table: "Processes_LaunchCandidates");

            migrationBuilder.DropColumn(
                name: "WorkflowDefinitionId",
                table: "Processes_RunAssignments");

            migrationBuilder.DropColumn(
                name: "WorkflowVersionId",
                table: "Processes_RunAssignments");

            migrationBuilder.DropColumn(
                name: "PreferredWorkflowDefinitionId",
                table: "Processes_RoleRequirements");

            migrationBuilder.DropColumn(
                name: "PreferredWorkflowVersionId",
                table: "Processes_RoleRequirements");

            migrationBuilder.DropColumn(
                name: "WorkflowDefinitionId",
                table: "Processes_LaunchCandidates");

            migrationBuilder.DropColumn(
                name: "WorkflowVersionId",
                table: "Processes_LaunchCandidates");
        }
    }
}
