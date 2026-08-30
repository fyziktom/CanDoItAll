using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddProviderHistoryExternalReference : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessContextReferenceType",
                table: "Workspace_SharedProviderInvocations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalReferenceType",
                table: "ProviderHistory_Entries",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalReferenceValue",
                table: "ProviderHistory_Entries",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Workspace_SharedProviderInvocations"
                SET "HistoryVersion" = "HistoryVersion" + 1
                WHERE "AccessContextReference" IS NOT NULL;

                UPDATE "ProviderHistory_Entries" AS history
                SET "ExternalReferenceValue" = invocation."AccessContextReference",
                    "Version" = invocation."HistoryVersion"
                FROM "Workspace_SharedProviderInvocations" AS invocation
                WHERE history."Id" = invocation."Id"
                    AND invocation."AccessContextReference" IS NOT NULL;

                UPDATE "ProviderHistory_Checkpoints"
                SET "Cursor" = NULL,
                    "Coverage" = 0,
                    "IndexedThroughUtc" = NULL,
                    "FailureCode" = NULL,
                    "LeaseOwner" = NULL,
                    "LeaseUntilUtc" = NULL
                WHERE "SourceKind" = 5;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderHistory_Entries_PartitionId_ExternalReferenceValue_~",
                table: "ProviderHistory_Entries",
                columns: new[] { "PartitionId", "ExternalReferenceValue", "ExternalReferenceType", "SortAtUtc", "Id" },
                descending: new[] { false, false, false, true, true },
                filter: "\"ExternalReferenceValue\" IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProviderHistory_Entries_PartitionId_ExternalReferenceValue_~",
                table: "ProviderHistory_Entries");

            migrationBuilder.DropColumn(
                name: "AccessContextReferenceType",
                table: "Workspace_SharedProviderInvocations");

            migrationBuilder.DropColumn(
                name: "ExternalReferenceType",
                table: "ProviderHistory_Entries");

            migrationBuilder.DropColumn(
                name: "ExternalReferenceValue",
                table: "ProviderHistory_Entries");
        }
    }
}
