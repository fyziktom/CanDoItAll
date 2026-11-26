using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddRemoteTemplatePublications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "AvatarImageUrl",
                table: "LlmChats_Definitions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048);

            migrationBuilder.AlterColumn<string>(
                name: "AvatarImageUrl",
                table: "LlmChats_DefinitionRevisions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048);

            migrationBuilder.CreateTable(
                name: "Workspace_RemoteTemplatePublications",
                columns: table => new
                {
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtcTicks = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_RemoteTemplatePublications", x => new { x.Kind, x.DefinitionId, x.CreatedUtcTicks });
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Workspace_RemoteTemplatePublications");

            migrationBuilder.AlterColumn<string>(
                name: "AvatarImageUrl",
                table: "LlmChats_Definitions",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "AvatarImageUrl",
                table: "LlmChats_DefinitionRevisions",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
