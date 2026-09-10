using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddLlmChatDefinitionCreateReceipts : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "LlmChats_DefinitionCreateReceipts",
                columns: table => new {
                    Producer = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Actor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    HistoryNamespace = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IntentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SemanticVersion = table.Column<int>(type: "integer", nullable: false),
                    SemanticFingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionRevision = table.Column<int>(type: "integer", nullable: false),
                    OriginalConcurrencyToken = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_LlmChats_DefinitionCreateReceipts", x => new { x.Producer, x.Actor, x.HistoryNamespace, x.IntentId });
                    table.ForeignKey(
                        name: "FK_LlmChats_CreateReceipt_OriginalRevision",
                        columns: x => new { x.DefinitionId, x.DefinitionRevision },
                        principalTable: "LlmChats_DefinitionRevisions",
                        principalColumns: new[] { "DefinitionId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_DefinitionCreateReceipts_DefinitionId_DefinitionRe~",
                table: "LlmChats_DefinitionCreateReceipts",
                columns: new[] { "DefinitionId", "DefinitionRevision" });
            migrationBuilder.Sql("""
                ALTER TABLE "LlmChats_DefinitionCreateReceipts"
                ALTER CONSTRAINT "FK_LlmChats_CreateReceipt_OriginalRevision" DEFERRABLE INITIALLY DEFERRED
                """);
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "LlmChats_DefinitionCreateReceipts") THEN
                        RAISE EXCEPTION 'Cannot remove retained Simple Chats creation receipts. Keep this schema or restore a reviewed pre-admission backup without replaying effects.';
                    END IF;
                END
                $$;
                """);
            migrationBuilder.DropTable(
                name: "LlmChats_DefinitionCreateReceipts");
        }
    }
}
