using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddLlmChatOperationEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LlmChats_OperationEvents",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: true),
                    AttemptOrdinal = table.Column<int>(type: "integer", nullable: true),
                    InvocationOutcome = table.Column<int>(type: "integer", nullable: true),
                    DeliveryMode = table.Column<int>(type: "integer", nullable: true),
                    Text = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: false),
                    Model = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    CachedInputTokens = table.Column<int>(type: "integer", nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmChats_OperationEvents", x => new { x.OperationId, x.Sequence });
                    table.ForeignKey(
                        name: "FK_LlmChats_OperationEvents_LlmChats_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "LlmChats_Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_Operations_Status_CompletedAtUtc",
                table: "LlmChats_Operations",
                columns: new[] { "Status", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_OperationEvents_OccurredAtUtc",
                table: "LlmChats_OperationEvents",
                column: "OccurredAtUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LlmChats_OperationEvents");

            migrationBuilder.DropIndex(
                name: "IX_LlmChats_Operations_Status_CompletedAtUtc",
                table: "LlmChats_Operations");
        }
    }
}
