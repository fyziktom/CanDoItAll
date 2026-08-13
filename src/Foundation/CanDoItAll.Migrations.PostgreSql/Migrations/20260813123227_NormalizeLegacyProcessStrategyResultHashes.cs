using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class NormalizeLegacyProcessStrategyResultHashes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE process_strategy_result_receipts
                SET "ResultHash" = 'sha256:' || "ResultHash"
                WHERE "ResultHash" ~ '^[0-9a-f]{64}$';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
