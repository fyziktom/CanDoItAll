using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProviderHistoryCallerAttribution : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<string>(
                name: "CallerIdentity",
                table: "Workspace_SharedProviderInvocations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FinalizationRecovered",
                table: "Workspace_SharedProviderInvocations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PriceSourceRevision",
                table: "ProviderHistory_Entries",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropColumn(
                name: "CallerIdentity",
                table: "Workspace_SharedProviderInvocations");

            migrationBuilder.DropColumn(
                name: "FinalizationRecovered",
                table: "Workspace_SharedProviderInvocations");

            migrationBuilder.DropColumn(
                name: "PriceSourceRevision",
                table: "ProviderHistory_Entries");
        }
    }
}
