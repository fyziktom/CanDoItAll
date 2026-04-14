using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessRuntimeRowSingularity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Processes_RunAssignments_ProcessRunId_RoleRequirementId_StepDefinitionId",
                table: "Processes_RunAssignments");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessStepRuns_RunStep",
                table: "Processes_StepRuns",
                columns: new[] { "ProcessRunId", "StepDefinitionId" },
                unique: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ProcessStepRuns_RunStep",
                table: "Processes_StepRuns");

            migrationBuilder.DropIndex(
                name: "UX_ProcessRunAssignments_RunScoped",
                table: "Processes_RunAssignments");

            migrationBuilder.DropIndex(
                name: "UX_ProcessRunAssignments_StepScoped",
                table: "Processes_RunAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RunAssignments_ProcessRunId_RoleRequirementId_StepDefinitionId",
                table: "Processes_RunAssignments",
                columns: new[] { "ProcessRunId", "RoleRequirementId", "StepDefinitionId" });
        }
    }
}
