using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddProcessSubprocessStepsAndManagers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubprocessDefinitionId",
                table: "Processes_StepDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubprocessDefinitionSnapshotName",
                table: "Processes_StepDefinitions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "HierarchyDepth",
                table: "Processes_Runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ManagerAgentId",
                table: "Processes_Runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerAgentName",
                table: "Processes_Runs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentRunId",
                table: "Processes_Runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentStepRunId",
                table: "Processes_Runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RootRunId",
                table: "Processes_Runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ManagerAgentOverrideId",
                table: "Processes_DefinitionVersions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerAgentOverrideName",
                table: "Processes_DefinitionVersions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDefinitions_SubprocessDefinitionId",
                table: "Processes_StepDefinitions",
                column: "SubprocessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_ManagerAgentId",
                table: "Processes_Runs",
                column: "ManagerAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_ParentRunId",
                table: "Processes_Runs",
                column: "ParentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_RootRunId",
                table: "Processes_Runs",
                column: "RootRunId");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessRuns_ParentStepRun",
                table: "Processes_Runs",
                column: "ParentStepRunId",
                unique: true,
                filter: "\"ParentStepRunId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_DefinitionVersions_ManagerAgentOverrideId",
                table: "Processes_DefinitionVersions",
                column: "ManagerAgentOverrideId");

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_Runs_Processes_Runs_ParentRunId",
                table: "Processes_Runs",
                column: "ParentRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_Runs_Processes_StepRuns_ParentStepRunId",
                table: "Processes_Runs",
                column: "ParentStepRunId",
                principalTable: "Processes_StepRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_StepDefinitions_Processes_Definitions_SubprocessD~",
                table: "Processes_StepDefinitions",
                column: "SubprocessDefinitionId",
                principalTable: "Processes_Definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Processes_Runs_Processes_Runs_ParentRunId",
                table: "Processes_Runs");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_Runs_Processes_StepRuns_ParentStepRunId",
                table: "Processes_Runs");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepDefinitions_Processes_Definitions_SubprocessD~",
                table: "Processes_StepDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Processes_StepDefinitions_SubprocessDefinitionId",
                table: "Processes_StepDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Processes_Runs_ManagerAgentId",
                table: "Processes_Runs");

            migrationBuilder.DropIndex(
                name: "IX_Processes_Runs_ParentRunId",
                table: "Processes_Runs");

            migrationBuilder.DropIndex(
                name: "IX_Processes_Runs_RootRunId",
                table: "Processes_Runs");

            migrationBuilder.DropIndex(
                name: "UX_ProcessRuns_ParentStepRun",
                table: "Processes_Runs");

            migrationBuilder.DropIndex(
                name: "IX_Processes_DefinitionVersions_ManagerAgentOverrideId",
                table: "Processes_DefinitionVersions");

            migrationBuilder.DropColumn(
                name: "SubprocessDefinitionId",
                table: "Processes_StepDefinitions");

            migrationBuilder.DropColumn(
                name: "SubprocessDefinitionSnapshotName",
                table: "Processes_StepDefinitions");

            migrationBuilder.DropColumn(
                name: "HierarchyDepth",
                table: "Processes_Runs");

            migrationBuilder.DropColumn(
                name: "ManagerAgentId",
                table: "Processes_Runs");

            migrationBuilder.DropColumn(
                name: "ManagerAgentName",
                table: "Processes_Runs");

            migrationBuilder.DropColumn(
                name: "ParentRunId",
                table: "Processes_Runs");

            migrationBuilder.DropColumn(
                name: "ParentStepRunId",
                table: "Processes_Runs");

            migrationBuilder.DropColumn(
                name: "RootRunId",
                table: "Processes_Runs");

            migrationBuilder.DropColumn(
                name: "ManagerAgentOverrideId",
                table: "Processes_DefinitionVersions");

            migrationBuilder.DropColumn(
                name: "ManagerAgentOverrideName",
                table: "Processes_DefinitionVersions");
        }
    }
}
