using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessCanvasPositionsAndStepDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BranchCanvasX",
                table: "Processes_StepDefinitions",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "BranchCanvasY",
                table: "Processes_StepDefinitions",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "CanvasX",
                table: "Processes_RoleRequirements",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "CanvasY",
                table: "Processes_RoleRequirements",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "Processes_StepDependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DependsOnStepId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DependsOnBranchOutcomeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepDependencies", x => x.Id);
                });

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
                name: "IX_Processes_StepDependencies_StepDefinitionId_DependsOnStepId_DependsOnBranchOutcomeId",
                table: "Processes_StepDependencies",
                columns: new[] { "StepDefinitionId", "DependsOnStepId", "DependsOnBranchOutcomeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDependencies_StepDefinitionId_DisplayOrder",
                table: "Processes_StepDependencies",
                columns: new[] { "StepDefinitionId", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Processes_StepDependencies");

            migrationBuilder.DropColumn(
                name: "BranchCanvasX",
                table: "Processes_StepDefinitions");

            migrationBuilder.DropColumn(
                name: "BranchCanvasY",
                table: "Processes_StepDefinitions");

            migrationBuilder.DropColumn(
                name: "CanvasX",
                table: "Processes_RoleRequirements");

            migrationBuilder.DropColumn(
                name: "CanvasY",
                table: "Processes_RoleRequirements");
        }
    }
}
