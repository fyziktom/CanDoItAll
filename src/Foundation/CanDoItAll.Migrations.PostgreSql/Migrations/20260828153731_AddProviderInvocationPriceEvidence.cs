using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProviderInvocationPriceEvidence : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "Workspace_SharedProviderInvocations",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,12)",
                oldPrecision: 28,
                oldScale: 12,
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CacheWriteTokenCount",
                table: "Workspace_SharedProviderInvocations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CachedInputTokenCount",
                table: "Workspace_SharedProviderInvocations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceEvidence",
                table: "Workspace_SharedProviderInvocations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingSnapshot",
                table: "Workspace_SharedProviderInvocations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ReasoningTokenCount",
                table: "Workspace_SharedProviderInvocations",
                type: "bigint",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropColumn(
                name: "CacheWriteTokenCount",
                table: "Workspace_SharedProviderInvocations");

            migrationBuilder.DropColumn(
                name: "CachedInputTokenCount",
                table: "Workspace_SharedProviderInvocations");

            migrationBuilder.DropColumn(
                name: "PriceEvidence",
                table: "Workspace_SharedProviderInvocations");

            migrationBuilder.DropColumn(
                name: "PricingSnapshot",
                table: "Workspace_SharedProviderInvocations");

            migrationBuilder.DropColumn(
                name: "ReasoningTokenCount",
                table: "Workspace_SharedProviderInvocations");

            migrationBuilder.AlterColumn<decimal>(
                name: "Price",
                table: "Workspace_SharedProviderInvocations",
                type: "numeric(28,12)",
                precision: 28,
                scale: 12,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);
        }
    }
}
