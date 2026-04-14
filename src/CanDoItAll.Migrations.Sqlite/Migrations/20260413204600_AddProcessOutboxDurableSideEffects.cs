using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessOutboxDurableSideEffects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Activity_Entries",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Processes_Outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProcessDefinitionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProcessRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CommandKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: false),
                    LeaseToken = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_Outbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activity_Entries_IdempotencyKey",
                table: "Activity_Entries",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Outbox_ProcessDefinitionId_CreatedAtUtc",
                table: "Processes_Outbox",
                columns: new[] { "ProcessDefinitionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Outbox_ProcessRunId_CreatedAtUtc",
                table: "Processes_Outbox",
                columns: new[] { "ProcessRunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Outbox_ProjectId_CreatedAtUtc",
                table: "Processes_Outbox",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Outbox_Status_NextAttemptAtUtc_LeaseExpiresAtUtc",
                table: "Processes_Outbox",
                columns: new[] { "Status", "NextAttemptAtUtc", "LeaseExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Processes_Outbox");

            migrationBuilder.DropIndex(
                name: "IX_Activity_Entries_IdempotencyKey",
                table: "Activity_Entries");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Activity_Entries");
        }
    }
}
