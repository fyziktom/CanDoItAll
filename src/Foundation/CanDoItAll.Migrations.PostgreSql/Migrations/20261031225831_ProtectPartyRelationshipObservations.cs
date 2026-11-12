using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class ProtectPartyRelationshipObservations : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "CrmHr_PartyRelationships",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "CrmHr_PartyRelationships");
        }
    }
}
