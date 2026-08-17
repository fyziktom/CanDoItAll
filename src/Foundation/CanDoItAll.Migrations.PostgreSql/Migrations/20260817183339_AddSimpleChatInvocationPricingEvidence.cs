using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddSimpleChatInvocationPricingEvidence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CalculatedCostUsd",
                table: "LlmChats_InvocationRecords",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingProfileHash",
                table: "LlmChats_InvocationRecords",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PricingStatus",
                table: "LlmChats_InvocationRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PricingVersion",
                table: "LlmChats_InvocationRecords",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ProviderCostUsd",
                table: "LlmChats_InvocationRecords",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsageStatus",
                table: "LlmChats_InvocationRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalculatedCostUsd",
                table: "LlmChats_InvocationRecords");

            migrationBuilder.DropColumn(
                name: "PricingProfileHash",
                table: "LlmChats_InvocationRecords");

            migrationBuilder.DropColumn(
                name: "PricingStatus",
                table: "LlmChats_InvocationRecords");

            migrationBuilder.DropColumn(
                name: "PricingVersion",
                table: "LlmChats_InvocationRecords");

            migrationBuilder.DropColumn(
                name: "ProviderCostUsd",
                table: "LlmChats_InvocationRecords");

            migrationBuilder.DropColumn(
                name: "UsageStatus",
                table: "LlmChats_InvocationRecords");
        }
    }
}
