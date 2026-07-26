using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowRunIdempotencyEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CanonicalInputHash",
                table: "AgentFramework_WorkflowLaunchIdempotency",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastReplayedAtUtc",
                table: "AgentFramework_WorkflowLaunchIdempotency",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplayCount",
                table: "AgentFramework_WorkflowLaunchIdempotency",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "UX_AF_WorkflowLaunchIdempotency_ApiKey",
                table: "AgentFramework_WorkflowLaunchIdempotency",
                column: "CallerKey",
                unique: true,
                filter: "\"OriginKind\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_AF_WorkflowLaunchIdempotency_ApiKey",
                table: "AgentFramework_WorkflowLaunchIdempotency");

            migrationBuilder.DropColumn(
                name: "CanonicalInputHash",
                table: "AgentFramework_WorkflowLaunchIdempotency");

            migrationBuilder.DropColumn(
                name: "LastReplayedAtUtc",
                table: "AgentFramework_WorkflowLaunchIdempotency");

            migrationBuilder.DropColumn(
                name: "ReplayCount",
                table: "AgentFramework_WorkflowLaunchIdempotency");
        }
    }
}
