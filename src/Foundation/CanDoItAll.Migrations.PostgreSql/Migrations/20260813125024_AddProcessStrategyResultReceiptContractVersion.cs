using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddProcessStrategyResultReceiptContractVersion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContractVersion",
                table: "process_strategy_result_receipts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "LegacyV1");

            migrationBuilder.AlterColumn<string>(
                name: "ContractVersion",
                table: "process_strategy_result_receipts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "BoundedV2",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "LegacyV1");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContractVersion",
                table: "process_strategy_result_receipts");
        }
    }
}
