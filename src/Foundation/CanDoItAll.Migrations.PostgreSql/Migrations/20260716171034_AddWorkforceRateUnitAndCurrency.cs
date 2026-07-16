using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddWorkforceRateUnitAndCurrency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RateCurrencyCode",
                table: "CrmHr_WorkforceProfiles",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "RateUnit",
                table: "CrmHr_WorkforceProfiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Hour");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RateCurrencyCode",
                table: "CrmHr_WorkforceProfiles");

            migrationBuilder.DropColumn(
                name: "RateUnit",
                table: "CrmHr_WorkforceProfiles");
        }
    }
}
