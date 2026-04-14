using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessRuntimeForeignKeysAndDependencyUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Processes_StepDependencies_StepDefinitionId_DependsOnStepId~",
                table: "Processes_StepDependencies");

            migrationBuilder.DropIndex(
                name: "IX_Processes_StepDefinitions_DependsOnBranchOutcomeId",
                table: "Processes_StepDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Processes_StepDefinitions_DependsOnStepId",
                table: "Processes_StepDefinitions");

            migrationBuilder.DropColumn(
                name: "DependsOnBranchOutcomeId",
                table: "Processes_StepDefinitions");

            migrationBuilder.DropColumn(
                name: "DependsOnStepId",
                table: "Processes_StepDefinitions");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Processes_DefinitionVersions_ProcessDefinitionId_Id",
                table: "Processes_DefinitionVersions",
                columns: new[] { "ProcessDefinitionId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepRoleRequirements_RoleRequirementId",
                table: "Processes_StepRoleRequirements",
                column: "RoleRequirementId");

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
                name: "IX_Processes_Runs_ProcessDefinitionId_ProcessDefinitionVersion~",
                table: "Processes_Runs",
                columns: new[] { "ProcessDefinitionId", "ProcessDefinitionVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RunAssignments_RoleRequirementId",
                table: "Processes_RunAssignments",
                column: "RoleRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RunAssignments_StepDefinitionId",
                table: "Processes_RunAssignments",
                column: "StepDefinitionId");

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
                name: "FK_Processes_ImprovementCandidates_Processes_Definitions_Proce~",
                table: "Processes_ImprovementCandidates",
                column: "ProcessDefinitionId",
                principalTable: "Processes_Definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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
                name: "FK_Processes_RunAssignments_Processes_RoleRequirements_RoleReq~",
                table: "Processes_RunAssignments",
                column: "RoleRequirementId",
                principalTable: "Processes_RoleRequirements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_RunAssignments_Processes_Runs_ProcessRunId",
                table: "Processes_RunAssignments",
                column: "ProcessRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_RunAssignments_Processes_StepDefinitions_StepDefi~",
                table: "Processes_RunAssignments",
                column: "StepDefinitionId",
                principalTable: "Processes_StepDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_Runs_Processes_DefinitionVersions_ProcessDefiniti~",
                table: "Processes_Runs",
                columns: new[] { "ProcessDefinitionId", "ProcessDefinitionVersionId" },
                principalTable: "Processes_DefinitionVersions",
                principalColumns: new[] { "ProcessDefinitionId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_Runs_Processes_Definitions_ProcessDefinitionId",
                table: "Processes_Runs",
                column: "ProcessDefinitionId",
                principalTable: "Processes_Definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_StepArtifactInputs_Processes_ArtifactExpectations~",
                table: "Processes_StepArtifactInputs",
                column: "ArtifactExpectationId",
                principalTable: "Processes_ArtifactExpectations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_StepArtifactInputs_Processes_StepDefinitions_Step~",
                table: "Processes_StepArtifactInputs",
                column: "StepDefinitionId",
                principalTable: "Processes_StepDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_StepBranchOutcomes_Processes_StepDefinitions_Step~",
                table: "Processes_StepBranchOutcomes",
                column: "StepDefinitionId",
                principalTable: "Processes_StepDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_StepDependencies_Processes_StepBranchOutcomes_Dep~",
                table: "Processes_StepDependencies",
                column: "DependsOnBranchOutcomeId",
                principalTable: "Processes_StepBranchOutcomes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_StepDependencies_Processes_StepDefinitions_Depend~",
                table: "Processes_StepDependencies",
                column: "DependsOnStepId",
                principalTable: "Processes_StepDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_StepDependencies_Processes_StepDefinitions_StepDe~",
                table: "Processes_StepDependencies",
                column: "StepDefinitionId",
                principalTable: "Processes_StepDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_StepRoleRequirements_Processes_RoleRequirements_R~",
                table: "Processes_StepRoleRequirements",
                column: "RoleRequirementId",
                principalTable: "Processes_RoleRequirements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_StepRoleRequirements_Processes_StepDefinitions_St~",
                table: "Processes_StepRoleRequirements",
                column: "StepDefinitionId",
                principalTable: "Processes_StepDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_StepRuns_Processes_Runs_ProcessRunId",
                table: "Processes_StepRuns",
                column: "ProcessRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_StepRuns_Processes_StepBranchOutcomes_SelectedBra~",
                table: "Processes_StepRuns",
                column: "SelectedBranchOutcomeId",
                principalTable: "Processes_StepBranchOutcomes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_StepRuns_Processes_StepDefinitions_StepDefinition~",
                table: "Processes_StepRuns",
                column: "StepDefinitionId",
                principalTable: "Processes_StepDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_WorkBriefs_Processes_Runs_ProcessRunId",
                table: "Processes_WorkBriefs",
                column: "ProcessRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_WorkBriefs_Processes_StepRuns_StepRunId",
                table: "Processes_WorkBriefs",
                column: "StepRunId",
                principalTable: "Processes_StepRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Processes_ArtifactExpectations_Processes_StepDefinitions_St~",
                table: "Processes_ArtifactExpectations");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_ArtifactRecords_Processes_Runs_ProcessRunId",
                table: "Processes_ArtifactRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_ArtifactRecords_Processes_StepRuns_StepRunId",
                table: "Processes_ArtifactRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_ConformanceObservations_Processes_Runs_ProcessRun~",
                table: "Processes_ConformanceObservations");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_ConformanceObservations_Processes_StepRuns_StepRu~",
                table: "Processes_ConformanceObservations");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_DecisionRecords_Processes_Runs_ProcessRunId",
                table: "Processes_DecisionRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_DecisionRecords_Processes_StepBranchOutcomes_Bran~",
                table: "Processes_DecisionRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_DecisionRecords_Processes_StepRuns_StepRunId",
                table: "Processes_DecisionRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_ImprovementCandidates_Processes_Definitions_Proce~",
                table: "Processes_ImprovementCandidates");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_ImprovementCandidates_Processes_Runs_ProcessRunId",
                table: "Processes_ImprovementCandidates");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_JournalEntries_Processes_Runs_ProcessRunId",
                table: "Processes_JournalEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_JournalEntries_Processes_StepRuns_StepRunId",
                table: "Processes_JournalEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_RunAssignments_Processes_RoleRequirements_RoleReq~",
                table: "Processes_RunAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_RunAssignments_Processes_Runs_ProcessRunId",
                table: "Processes_RunAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_RunAssignments_Processes_StepDefinitions_StepDefi~",
                table: "Processes_RunAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_Runs_Processes_DefinitionVersions_ProcessDefiniti~",
                table: "Processes_Runs");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_Runs_Processes_Definitions_ProcessDefinitionId",
                table: "Processes_Runs");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepArtifactInputs_Processes_ArtifactExpectations~",
                table: "Processes_StepArtifactInputs");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepArtifactInputs_Processes_StepDefinitions_Step~",
                table: "Processes_StepArtifactInputs");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepBranchOutcomes_Processes_StepDefinitions_Step~",
                table: "Processes_StepBranchOutcomes");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepDependencies_Processes_StepBranchOutcomes_Dep~",
                table: "Processes_StepDependencies");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepDependencies_Processes_StepDefinitions_Depend~",
                table: "Processes_StepDependencies");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepDependencies_Processes_StepDefinitions_StepDe~",
                table: "Processes_StepDependencies");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepRoleRequirements_Processes_RoleRequirements_R~",
                table: "Processes_StepRoleRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepRoleRequirements_Processes_StepDefinitions_St~",
                table: "Processes_StepRoleRequirements");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepRuns_Processes_Runs_ProcessRunId",
                table: "Processes_StepRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepRuns_Processes_StepBranchOutcomes_SelectedBra~",
                table: "Processes_StepRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepRuns_Processes_StepDefinitions_StepDefinition~",
                table: "Processes_StepRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_WorkBriefs_Processes_Runs_ProcessRunId",
                table: "Processes_WorkBriefs");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_WorkBriefs_Processes_StepRuns_StepRunId",
                table: "Processes_WorkBriefs");

            migrationBuilder.DropIndex(
                name: "IX_Processes_StepRoleRequirements_RoleRequirementId",
                table: "Processes_StepRoleRequirements");

            migrationBuilder.DropIndex(
                name: "UX_ProcessStepDeps_Conditional",
                table: "Processes_StepDependencies");

            migrationBuilder.DropIndex(
                name: "UX_ProcessStepDeps_Unconditional",
                table: "Processes_StepDependencies");

            migrationBuilder.DropIndex(
                name: "IX_Processes_Runs_ProcessDefinitionId_ProcessDefinitionVersion~",
                table: "Processes_Runs");

            migrationBuilder.DropIndex(
                name: "IX_Processes_RunAssignments_RoleRequirementId",
                table: "Processes_RunAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Processes_RunAssignments_StepDefinitionId",
                table: "Processes_RunAssignments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Processes_DefinitionVersions_ProcessDefinitionId_Id",
                table: "Processes_DefinitionVersions");

            migrationBuilder.AddColumn<Guid>(
                name: "DependsOnBranchOutcomeId",
                table: "Processes_StepDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DependsOnStepId",
                table: "Processes_StepDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDependencies_StepDefinitionId_DependsOnStepId~",
                table: "Processes_StepDependencies",
                columns: new[] { "StepDefinitionId", "DependsOnStepId", "DependsOnBranchOutcomeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDefinitions_DependsOnBranchOutcomeId",
                table: "Processes_StepDefinitions",
                column: "DependsOnBranchOutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDefinitions_DependsOnStepId",
                table: "Processes_StepDefinitions",
                column: "DependsOnStepId");
        }
    }
}
