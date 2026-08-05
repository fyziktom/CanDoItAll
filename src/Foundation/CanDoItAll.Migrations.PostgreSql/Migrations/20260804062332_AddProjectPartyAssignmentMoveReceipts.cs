using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddProjectPartyAssignmentMoveReceipts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrmHr_ProjectPartyAssignmentMoveReceipts",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeSetFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_ProjectPartyAssignmentMoveReceipts", x => x.OperationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_ProjectPartyAssignmentMoveReceipts_SourceProjectId_Ta~",
                table: "CrmHr_ProjectPartyAssignmentMoveReceipts",
                columns: new[] { "SourceProjectId", "TargetProjectId", "CompletedAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrmHr_ProjectPartyAssignmentMoveReceipts");
        }
    }
}
