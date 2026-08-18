using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class CompleteLlmChatOperationEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LastEventSequence",
                table: "LlmChats_Operations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "FinishReason",
                table: "LlmChats_OperationEvents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DeliveryMode",
                table: "LlmChats_InvocationRecords",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "FinishReason",
                table: "LlmChats_InvocationRecords",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE "LlmChats_InvocationRecords" AS invocation
                SET "DeliveryMode" = COALESCE(
                        (
                            SELECT started."DeliveryMode"
                            FROM "LlmChats_OperationEvents" AS started
                            WHERE started."OperationId" = invocation."OperationId"
                              AND started."Kind" = 1
                              AND started."AttemptOrdinal" = invocation."Ordinal"
                            ORDER BY started."Sequence"
                            LIMIT 1
                        ),
                        1),
                    "FinishReason" = CASE
                        WHEN invocation."Outcome" = 0 THEN 'completed'
                        ELSE ''
                    END;

                UPDATE "LlmChats_OperationEvents" AS finished
                SET "Model" = invocation."Model",
                    "DeliveryMode" = invocation."DeliveryMode",
                    "FinishReason" = invocation."FinishReason"
                FROM "LlmChats_InvocationRecords" AS invocation
                WHERE finished."OperationId" = invocation."OperationId"
                  AND finished."Kind" = 2
                  AND finished."AttemptOrdinal" = invocation."Ordinal";

                UPDATE "LlmChats_Operations" AS operation
                SET "LastEventSequence" = COALESCE(
                    (
                        SELECT MAX(event."Sequence")
                        FROM "LlmChats_OperationEvents" AS event
                        WHERE event."OperationId" = operation."Id"
                    ),
                    0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastEventSequence",
                table: "LlmChats_Operations");

            migrationBuilder.DropColumn(
                name: "FinishReason",
                table: "LlmChats_OperationEvents");

            migrationBuilder.DropColumn(
                name: "DeliveryMode",
                table: "LlmChats_InvocationRecords");

            migrationBuilder.DropColumn(
                name: "FinishReason",
                table: "LlmChats_InvocationRecords");
        }
    }
}
