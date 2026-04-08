using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationRuntimeHardeningPhase13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Workspace_ConnectorCommands_Status_ApprovalState_NextAttemptAtUtc",
                table: "Workspace_ConnectorCommands");

            migrationBuilder.DropIndex(
                name: "IX_Automation_EnvelopeDeliveries_State_AvailableAtUtc",
                table: "Automation_EnvelopeDeliveries");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresAtUtc",
                table: "Workspace_ConnectorCommands",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeaseToken",
                table: "Workspace_ConnectorCommands",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ConnectorCommands_Status_ApprovalState_NextAttemptAtUtc_LeaseExpiresAtUtc",
                table: "Workspace_ConnectorCommands",
                columns: new[] { "Status", "ApprovalState", "NextAttemptAtUtc", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Automation_EnvelopeDeliveries_State_AvailableAtUtc_LockedAtUtc",
                table: "Automation_EnvelopeDeliveries",
                columns: new[] { "State", "AvailableAtUtc", "LockedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Workspace_ConnectorCommands_Status_ApprovalState_NextAttemptAtUtc_LeaseExpiresAtUtc",
                table: "Workspace_ConnectorCommands");

            migrationBuilder.DropIndex(
                name: "IX_Automation_EnvelopeDeliveries_State_AvailableAtUtc_LockedAtUtc",
                table: "Automation_EnvelopeDeliveries");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAtUtc",
                table: "Workspace_ConnectorCommands");

            migrationBuilder.DropColumn(
                name: "LeaseToken",
                table: "Workspace_ConnectorCommands");

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ConnectorCommands_Status_ApprovalState_NextAttemptAtUtc",
                table: "Workspace_ConnectorCommands",
                columns: new[] { "Status", "ApprovalState", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Automation_EnvelopeDeliveries_State_AvailableAtUtc",
                table: "Automation_EnvelopeDeliveries",
                columns: new[] { "State", "AvailableAtUtc" });
        }
    }
}
