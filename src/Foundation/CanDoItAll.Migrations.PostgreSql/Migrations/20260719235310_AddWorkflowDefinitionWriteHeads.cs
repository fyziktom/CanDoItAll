using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddWorkflowDefinitionWriteHeads : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "AgentFramework_WorkflowDefinitions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowDefinitionHeads",
                columns: table => new
                {
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowDefinitionHeads", x => x.WorkflowId);
                    table.ForeignKey(
                        name: "FK_AgentFramework_WorkflowDefinitionHeads_AgentFramework_Workf~",
                        column: x => x.VersionId,
                        principalTable: "AgentFramework_WorkflowDefinitions",
                        principalColumn: "VersionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT
                        "VersionId",
                        ROW_NUMBER() OVER (
                            PARTITION BY "WorkflowId"
                            ORDER BY "UpdatedAtUtc", "CreatedAtUtc", "VersionId") AS "Revision"
                    FROM "AgentFramework_WorkflowDefinitions"
                )
                UPDATE "AgentFramework_WorkflowDefinitions" AS definition
                SET "Revision" = ranked."Revision"
                FROM ranked
                WHERE definition."VersionId" = ranked."VersionId";
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "AgentFramework_WorkflowDefinitionHeads" ("WorkflowId", "VersionId")
                SELECT DISTINCT ON ("WorkflowId")
                    "WorkflowId",
                    "VersionId"
                FROM "AgentFramework_WorkflowDefinitions"
                ORDER BY "WorkflowId", "UpdatedAtUtc" DESC, "CreatedAtUtc" DESC, "VersionId" DESC;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "AgentFramework_WorkflowDefinitions"
                ALTER COLUMN "Revision" DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_WorkflowId_Revision",
                table: "AgentFramework_WorkflowDefinitions",
                columns: new[] { "WorkflowId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowDefinitionHeads_VersionId",
                table: "AgentFramework_WorkflowDefinitionHeads",
                column: "VersionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowDefinitionHeads");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_WorkflowId_Revision",
                table: "AgentFramework_WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "AgentFramework_WorkflowDefinitions");
        }
    }
}
