using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmChatExecutionLeases : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClaimedAtUtc",
                table: "LlmChats_Operations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DispatchPhase",
                table: "LlmChats_Operations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "ExecutionEpoch",
                table: "LlmChats_Operations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "ExecutionOwnerId",
                table: "LlmChats_Operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HeartbeatAtUtc",
                table: "LlmChats_Operations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresAtUtc",
                table: "LlmChats_Operations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_Operations_Status_LeaseExpiresAtUtc_StartedAtUtc",
                table: "LlmChats_Operations",
                columns: new[] { "Status", "LeaseExpiresAtUtc", "StartedAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LlmChats_Operations_Status_LeaseExpiresAtUtc_StartedAtUtc",
                table: "LlmChats_Operations");

            migrationBuilder.DropColumn(
                name: "ClaimedAtUtc",
                table: "LlmChats_Operations");

            migrationBuilder.DropColumn(
                name: "DispatchPhase",
                table: "LlmChats_Operations");

            migrationBuilder.DropColumn(
                name: "ExecutionEpoch",
                table: "LlmChats_Operations");

            migrationBuilder.DropColumn(
                name: "ExecutionOwnerId",
                table: "LlmChats_Operations");

            migrationBuilder.DropColumn(
                name: "HeartbeatAtUtc",
                table: "LlmChats_Operations");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAtUtc",
                table: "LlmChats_Operations");
        }
    }
}
