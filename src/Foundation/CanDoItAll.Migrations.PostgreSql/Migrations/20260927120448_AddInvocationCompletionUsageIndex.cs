using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddInvocationCompletionUsageIndex : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_InvocationRecords_CompletedAtUtc",
                table: "LlmChats_InvocationRecords",
                column: "CompletedAtUtc");
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropIndex(
                name: "IX_LlmChats_InvocationRecords_CompletedAtUtc",
                table: "LlmChats_InvocationRecords");
        }
    }
}
