using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class ProcessStepAutomationDispatchClaims : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AutomationDispatchAttemptCount",
                table: "Processes_StepRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AutomationDispatchClaimToken",
                table: "Processes_StepRuns",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AutomationDispatchClaimedAtUtc",
                table: "Processes_StepRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutomationDispatchClaimedBy",
                table: "Processes_StepRuns",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AutomationDispatchLeaseExpiresAtUtc",
                table: "Processes_StepRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepRuns_ProcessRunId_AutomationDispatchLeaseExpi~",
                table: "Processes_StepRuns",
                columns: new[] { "ProcessRunId", "AutomationDispatchLeaseExpiresAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Processes_StepRuns_ProcessRunId_AutomationDispatchLeaseExpi~",
                table: "Processes_StepRuns");

            migrationBuilder.DropColumn(
                name: "AutomationDispatchAttemptCount",
                table: "Processes_StepRuns");

            migrationBuilder.DropColumn(
                name: "AutomationDispatchClaimToken",
                table: "Processes_StepRuns");

            migrationBuilder.DropColumn(
                name: "AutomationDispatchClaimedAtUtc",
                table: "Processes_StepRuns");

            migrationBuilder.DropColumn(
                name: "AutomationDispatchClaimedBy",
                table: "Processes_StepRuns");

            migrationBuilder.DropColumn(
                name: "AutomationDispatchLeaseExpiresAtUtc",
                table: "Processes_StepRuns");
        }
    }
}
