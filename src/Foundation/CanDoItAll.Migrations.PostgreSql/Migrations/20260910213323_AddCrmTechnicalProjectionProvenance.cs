using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddCrmTechnicalProjectionProvenance : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<string>(
                name: "ProjectedDisplayName",
                table: "CrmHr_AiResourceBindings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProjectedLifecycleStatus",
                table: "CrmHr_AiResourceBindings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectedSummary",
                table: "CrmHr_AiResourceBindings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProjectionAvailability",
                table: "CrmHr_AiResourceBindings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<long>(
                name: "SourceCatalogRevision",
                table: "CrmHr_AiResourceBindings",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceDatabaseProfileId",
                table: "CrmHr_AiResourceBindings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceScopeKey",
                table: "CrmHr_AiResourceBindings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceScopeKind",
                table: "CrmHr_AiResourceBindings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CrmHr_AiTechnicalProjectionCursors",
                columns: table => new {
                    DatabaseProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceScopeKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceScopeKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CatalogRevision = table.Column<long>(type: "bigint", nullable: false),
                    ProjectionSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_CrmHr_AiTechnicalProjectionCursors", x => new { x.DatabaseProfileId, x.SourceScopeKind, x.SourceScopeKey });
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AiResourceBindings_SourceDatabaseProfileId_SourceScop~",
                table: "CrmHr_AiResourceBindings",
                columns: new[] { "SourceDatabaseProfileId", "SourceScopeKind", "SourceScopeKey" });
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                LOCK TABLE "CrmHr_AiResourceBindings", "CrmHr_AiTechnicalProjectionCursors" IN ACCESS EXCLUSIVE MODE;
                DO $rollback$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "CrmHr_AiTechnicalProjectionCursors") OR
                       EXISTS (SELECT 1 FROM "CrmHr_AiResourceBindings"
                           WHERE "SourceDatabaseProfileId" IS NOT NULL OR "SourceScopeKind" IS NOT NULL OR
                                 "SourceCatalogRevision" IS NOT NULL OR "SourceScopeKey" <> '' OR
                                 "ProjectionAvailability" <> 'Unknown' OR "ProjectedDisplayName" <> '' OR
                                 "ProjectedSummary" <> '' OR "ProjectedLifecycleStatus" IS NOT NULL) THEN
                        RAISE EXCEPTION 'CRM technical projection provenance is retained; keep the schema or restore a reviewed consistent catalog and database backup without replaying effects.';
                    END IF;
                END
                $rollback$;
                """);

            migrationBuilder.DropTable(
                name: "CrmHr_AiTechnicalProjectionCursors");

            migrationBuilder.DropIndex(
                name: "IX_CrmHr_AiResourceBindings_SourceDatabaseProfileId_SourceScop~",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.DropColumn(
                name: "ProjectedDisplayName",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.DropColumn(
                name: "ProjectedLifecycleStatus",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.DropColumn(
                name: "ProjectedSummary",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.DropColumn(
                name: "ProjectionAvailability",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.DropColumn(
                name: "SourceCatalogRevision",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.DropColumn(
                name: "SourceDatabaseProfileId",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.DropColumn(
                name: "SourceScopeKey",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.DropColumn(
                name: "SourceScopeKind",
                table: "CrmHr_AiResourceBindings");
        }
    }
}
