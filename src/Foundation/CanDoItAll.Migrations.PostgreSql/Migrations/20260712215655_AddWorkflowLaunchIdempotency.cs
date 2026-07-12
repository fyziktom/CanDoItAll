using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddWorkflowLaunchIdempotency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowLaunchIdempotency",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CallerKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectionKind = table.Column<int>(type: "integer", nullable: false),
                    RequestedVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    OriginKind = table.Column<int>(type: "integer", nullable: false),
                    OriginScopeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ClaimToken = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservedRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletionJson = table.Column<string>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowLaunchIdempotency", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AF_WorkflowLaunchIdempotency_Lease",
                table: "AgentFramework_WorkflowLaunchIdempotency",
                columns: new[] { "State", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_AF_WorkflowLaunchIdempotency_Run",
                table: "AgentFramework_WorkflowLaunchIdempotency",
                column: "ReservedRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AF_WorkflowLaunchIdempotency_Scope",
                table: "AgentFramework_WorkflowLaunchIdempotency",
                columns: new[] { "CallerKey", "WorkflowId", "SelectionKind", "RequestedVersionId", "Mode", "OriginKind", "OriginScopeKey" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowLaunchIdempotency");
        }
    }
}
