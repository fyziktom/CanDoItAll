using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessBranching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SelectedBranchOutcomeId",
                table: "Processes_StepRuns",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedBranchOutcomeTitle",
                table: "Processes_StepRuns",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "DecisionRoleRequirementId",
                table: "Processes_StepDefinitions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DependsOnBranchOutcomeId",
                table: "Processes_StepDefinitions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchOutcomeId",
                table: "Processes_DecisionRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BranchOutcomeTitle",
                table: "Processes_DecisionRecords",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Processes_StepBranchOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepBranchOutcomes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepRuns_SelectedBranchOutcomeId",
                table: "Processes_StepRuns",
                column: "SelectedBranchOutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDefinitions_DecisionRoleRequirementId",
                table: "Processes_StepDefinitions",
                column: "DecisionRoleRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDefinitions_DependsOnBranchOutcomeId",
                table: "Processes_StepDefinitions",
                column: "DependsOnBranchOutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_DecisionRecords_BranchOutcomeId",
                table: "Processes_DecisionRecords",
                column: "BranchOutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepBranchOutcomes_StepDefinitionId_DisplayOrder",
                table: "Processes_StepBranchOutcomes",
                columns: new[] { "StepDefinitionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepBranchOutcomes_StepDefinitionId_Key",
                table: "Processes_StepBranchOutcomes",
                columns: new[] { "StepDefinitionId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Processes_StepBranchOutcomes");

            migrationBuilder.DropIndex(
                name: "IX_Processes_StepRuns_SelectedBranchOutcomeId",
                table: "Processes_StepRuns");

            migrationBuilder.DropIndex(
                name: "IX_Processes_StepDefinitions_DecisionRoleRequirementId",
                table: "Processes_StepDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Processes_StepDefinitions_DependsOnBranchOutcomeId",
                table: "Processes_StepDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Processes_DecisionRecords_BranchOutcomeId",
                table: "Processes_DecisionRecords");

            migrationBuilder.DropColumn(
                name: "SelectedBranchOutcomeId",
                table: "Processes_StepRuns");

            migrationBuilder.DropColumn(
                name: "SelectedBranchOutcomeTitle",
                table: "Processes_StepRuns");

            migrationBuilder.DropColumn(
                name: "DecisionRoleRequirementId",
                table: "Processes_StepDefinitions");

            migrationBuilder.DropColumn(
                name: "DependsOnBranchOutcomeId",
                table: "Processes_StepDefinitions");

            migrationBuilder.DropColumn(
                name: "BranchOutcomeId",
                table: "Processes_DecisionRecords");

            migrationBuilder.DropColumn(
                name: "BranchOutcomeTitle",
                table: "Processes_DecisionRecords");
        }
    }
}
