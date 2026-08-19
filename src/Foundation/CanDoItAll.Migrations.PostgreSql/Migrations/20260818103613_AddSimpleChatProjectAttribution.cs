using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddSimpleChatProjectAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttributionScopeKey",
                table: "LlmChats_Operations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AttributionScopeKind",
                table: "LlmChats_Operations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_Operations_AttributionScopeKind_AttributionScopeKe~",
                table: "LlmChats_Operations",
                columns: new[] { "AttributionScopeKind", "AttributionScopeKey", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LlmChats_Operations_AttributionScopeKind_AttributionScopeKe~",
                table: "LlmChats_Operations");

            migrationBuilder.DropColumn(
                name: "AttributionScopeKey",
                table: "LlmChats_Operations");

            migrationBuilder.DropColumn(
                name: "AttributionScopeKind",
                table: "LlmChats_Operations");
        }
    }
}
