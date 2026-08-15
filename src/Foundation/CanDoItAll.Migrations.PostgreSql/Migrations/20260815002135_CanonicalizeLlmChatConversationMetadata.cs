using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class CanonicalizeLlmChatConversationMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "LlmChats_Conversations" AS conversation
                        INNER JOIN "LlmChats_Transcripts" AS transcript
                            ON conversation."Id" = transcript."ConversationId"
                        WHERE conversation."Title" IS DISTINCT FROM transcript."Title") THEN
                        RAISE EXCEPTION 'Cannot canonicalize LLM Chat titles while conversation and transcript titles differ.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_LlmChats_Transcripts_UpdatedAtUtc",
                table: "LlmChats_Transcripts");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "LlmChats_Transcripts");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "LlmChats_Transcripts");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "LlmChats_Transcripts");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "LlmChats_Transcripts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "LlmChats_Transcripts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "LlmChats_Transcripts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.Sql(
                """
                UPDATE "LlmChats_Transcripts" AS transcript
                SET "Title" = conversation."Title",
                    "CreatedAtUtc" = conversation."CreatedAtUtc",
                    "UpdatedAtUtc" = conversation."UpdatedAtUtc"
                FROM "LlmChats_Conversations" AS conversation
                WHERE conversation."Id" = transcript."ConversationId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_LlmChats_Transcripts_UpdatedAtUtc",
                table: "LlmChats_Transcripts",
                column: "UpdatedAtUtc");
        }
    }
}
