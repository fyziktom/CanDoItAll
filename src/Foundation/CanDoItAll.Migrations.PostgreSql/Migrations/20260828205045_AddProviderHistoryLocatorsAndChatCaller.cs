using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddProviderHistoryLocatorsAndChatCaller : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HistoryCaller",
                table: "LlmChats_Operations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentFramework_HistoryLocators",
                columns: table => new
                {
                    PartitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeKind = table.Column<int>(type: "integer", nullable: false),
                    ScopeKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceVersion = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_HistoryLocators", x => new { x.PartitionId, x.EvidenceId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_HistoryLocators_PartitionId_ProjectId_IsDele~",
                table: "AgentFramework_HistoryLocators",
                columns: new[] { "PartitionId", "ProjectId", "IsDeleted" });
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentFramework_HistoryLocators");

            migrationBuilder.DropColumn(
                name: "HistoryCaller",
                table: "LlmChats_Operations");
        }
    }
}
