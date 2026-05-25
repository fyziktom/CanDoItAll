using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260525184500_ProcessRuntimeGovernanceV5")]
    public partial class ProcessRuntimeGovernanceV5 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlockReasonCode",
                table: "Processes_StepRuns",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "RecoveryOptionsJson",
                table: "Processes_StepRuns",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ProjectionIdentityHash",
                table: "Processes_ArtifactRecords",
                type: "character varying(95)",
                maxLength: 95,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ArtifactRecords_ProcessRunId_ProjectionIdentityHash",
                table: "Processes_ArtifactRecords",
                columns: new[] { "ProcessRunId", "ProjectionIdentityHash" },
                unique: true,
                filter: "\"ProjectionIdentityHash\" <> ''");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Processes_ArtifactRecords_ProcessRunId_ProjectionIdentityHash",
                table: "Processes_ArtifactRecords");

            migrationBuilder.DropColumn(
                name: "BlockReasonCode",
                table: "Processes_StepRuns");

            migrationBuilder.DropColumn(
                name: "RecoveryOptionsJson",
                table: "Processes_StepRuns");

            migrationBuilder.DropColumn(
                name: "ProjectionIdentityHash",
                table: "Processes_ArtifactRecords");
        }
    }
}
