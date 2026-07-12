using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class ProcessStrategyResultReceiptLineage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiagnosticsJson",
                table: "process_strategy_result_receipts",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ProducedArtifactsJson",
                table: "process_strategy_result_receipts",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "RecoveryDecisionJson",
                table: "process_strategy_result_receipts",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiagnosticsJson",
                table: "process_strategy_result_receipts");

            migrationBuilder.DropColumn(
                name: "ProducedArtifactsJson",
                table: "process_strategy_result_receipts");

            migrationBuilder.DropColumn(
                name: "RecoveryDecisionJson",
                table: "process_strategy_result_receipts");
        }
    }
}
