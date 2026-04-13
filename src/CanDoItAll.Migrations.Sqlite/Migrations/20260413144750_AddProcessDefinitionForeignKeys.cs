using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessDefinitionForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_Processes_DefinitionVersions_Processes_Definitions_ProcessDefinitionId",
                table: "Processes_DefinitionVersions",
                column: "ProcessDefinitionId",
                principalTable: "Processes_Definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_RoleRequirements_Processes_DefinitionVersions_ProcessDefinitionVersionId",
                table: "Processes_RoleRequirements",
                column: "ProcessDefinitionVersionId",
                principalTable: "Processes_DefinitionVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_RoleSkillRequirements_Processes_RoleRequirements_RoleRequirementId",
                table: "Processes_RoleSkillRequirements",
                column: "RoleRequirementId",
                principalTable: "Processes_RoleRequirements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_StepDefinitions_Processes_DefinitionVersions_ProcessDefinitionVersionId",
                table: "Processes_StepDefinitions",
                column: "ProcessDefinitionVersionId",
                principalTable: "Processes_DefinitionVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_StepDefinitions_Processes_RoleRequirements_DecisionRoleRequirementId",
                table: "Processes_StepDefinitions",
                column: "DecisionRoleRequirementId",
                principalTable: "Processes_RoleRequirements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Processes_DefinitionVersions_Processes_Definitions_ProcessDefinitionId",
                table: "Processes_DefinitionVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_RoleRequirements_Processes_DefinitionVersions_ProcessDefinitionVersionId",
                table: "Processes_RoleRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_RoleSkillRequirements_Processes_RoleRequirements_RoleRequirementId",
                table: "Processes_RoleSkillRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepDefinitions_Processes_DefinitionVersions_ProcessDefinitionVersionId",
                table: "Processes_StepDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepDefinitions_Processes_RoleRequirements_DecisionRoleRequirementId",
                table: "Processes_StepDefinitions");
        }
    }
}
